using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Xml;

namespace Bannerlord.UIExtenderEx.Analyzers.Game;

/// <summary>Represents a Gauntlet movie loaded by the game and its bound ViewModel type, corresponding to records in <c>movies.json</c>.</summary>
internal sealed record MovieEntry(string Movie, string? ViewModel, bool Paired, string? OverrideView, string? GameStateScreen, string? Module);

/// <summary>Represents a vanilla game prefab entry from the package index, containing its parsed DOM tree and constituent XML tag names.</summary>
internal sealed record GamePrefabEntry(GameTree Tree, IReadOnlyCollection<string> Tags);

/// <summary>
/// Represents a serialized prefab element tree (<c>{ n, a, c }</c>) sourced from either a per-build package file or a bundle root node.
/// Document trees shared across versions are cached and reconstructed once.
/// </summary>
internal abstract class GameTree
{
    public abstract IReadOnlyDictionary<string, object?>? Root(CancellationToken cancellation);
}

/// <summary>Represents a standalone prefab tree file from a per-build GUI package.</summary>
internal sealed class FileTree(AdditionalText file) : GameTree
{
    public override IReadOnlyDictionary<string, object?>? Root(CancellationToken cancellation) =>
        Json.Parse(file.GetText(cancellation)?.ToString() ?? "") is IReadOnlyDictionary<string, object?> tree
        && tree.TryGetValue("root", out var root) ? root as IReadOnlyDictionary<string, object?> : null;
}

/// <summary>Represents a prefab tree entry within a multi-version GUI bundle resolved via its root node index.</summary>
internal sealed class BundleTree(GuiBundle bundle, int root) : GameTree
{
    public override IReadOnlyDictionary<string, object?>? Root(CancellationToken cancellation) => bundle.Tree(root);
}

/// <summary>Encapsulates a game prefab definition, its contributing module, and its lazily reconstructed XML document.</summary>
internal sealed class GamePrefab
{
    private readonly GamePrefabEntry _entry;
    private readonly DocumentCache _documents;

    public string Name { get; }
    public string Module { get; }

    /// <summary>Gets all element tag names referenced in the prefab index, identifying potential child prefab instantiations.</summary>
    public IReadOnlyCollection<string> Tags => _entry.Tags;

    public GamePrefab(string name, string module, GamePrefabEntry entry, DocumentCache documents)
    {
        Name = name;
        Module = module;
        _entry = entry;
        _documents = documents;
    }

    /// <summary>Reconstructs the target <see cref="XmlDocument"/> representing the prefab XML, or returns <see langword="null"/> if tree deserialization fails.</summary>
    public XmlDocument? Document(CancellationToken cancellation) => _documents.Get(_entry.Tree, cancellation);
}

/// <summary>
/// Caches reconstructed XML documents during an analysis pass. Caches are scoped per analysis because <see cref="XmlDocument"/>
/// instances are not thread-safe and concurrent IDE compilations may evaluate simultaneously.
/// <para>
/// Element trees reflect the exact structure loaded by <c>WidgetPrefab.LoadFrom</c> (excluding comments and whitespace-only text),
/// ensuring that XPath selections match runtime behavior identically.
/// </para>
/// </summary>
internal sealed class DocumentCache
{
    private readonly Dictionary<GameTree, XmlDocument?> _documents = new();

    public XmlDocument? Get(GameTree tree, CancellationToken cancellation)
    {
        if (_documents.TryGetValue(tree, out var cached))
            return cached;
        XmlDocument? document;
        try
        {
            document = tree.Root(cancellation) is { } node ? Build(node) : null;
        }
        catch (Exception exception) when (exception is FormatException or XmlException or InvalidCastException or ArgumentException)
        {
            document = null;
        }
        _documents[tree] = document;
        return document;
    }

    private static XmlDocument Build(IReadOnlyDictionary<string, object?> root)
    {
        var document = new XmlDocument();
        document.AppendChild(Element(document, root));
        return document;
    }

    /// <summary>Constructs an <see cref="XmlElement"/> from a serialized node dictionary containing name (<c>n</c>), attributes (<c>a</c>), and children (<c>c</c>).</summary>
    private static XmlElement Element(XmlDocument document, IReadOnlyDictionary<string, object?> node)
    {
        var element = document.CreateElement(node.String("n") ?? throw new FormatException("A node has no name"));
        if (node.TryGetValue("a", out var a) && a is IReadOnlyDictionary<string, object?> attributes)
        {
            foreach (var pair in attributes)
                element.SetAttribute(pair.Key, pair.Value as string ?? "");
        }
        if (node.TryGetValue("c", out var c) && c is List<object?> children)
        {
            foreach (var child in children)
            {
                element.AppendChild(child switch
                {
                    string text => document.CreateTextNode(text),
                    IReadOnlyDictionary<string, object?> childNode => Element(document, childNode),
                    _ => throw new FormatException("A child is neither a node nor a text"),
                });
            }
        }
        return element;
    }
}

/// <summary>
/// Represents a format 3 game GUI package (base game modules or DLC) supplied via <c>AdditionalFiles</c>.
/// Encapsulates serialized prefab trees, movie mappings, ViewModels, and widget metadata.
/// </summary>
internal sealed class GamePackage
{
    public string Id { get; }
    public bool IsDlc { get; }

    /// <summary>Gets the target game version (e.g. <c>v1.4.8</c>) declared in the package manifest, or <see langword="null"/> if unspecified.</summary>
    public string? GameVersion { get; }

    /// <summary>Gets the contributing module folders ordered by load precedence.</summary>
    public IReadOnlyList<string> Modules { get; }

    /// <summary>Gets prefab entries grouped by module folder and indexed by prefab name.</summary>
    public IReadOnlyDictionary<string, Dictionary<string, GamePrefabEntry>> Prefabs { get; }

    public IReadOnlyList<MovieEntry> Movies { get; }

    /// <summary>Gets ViewModel type definitions indexed by fully qualified type name.</summary>
    public IReadOnlyDictionary<string, GameViewModel> ViewModels { get; }

    /// <summary>
    /// Gets property change notifications announced by widget methods, indexed by widget type name and property name.
    /// Returns <see langword="null"/> if the package metadata omits announcement records.
    /// </summary>
    public IReadOnlyDictionary<string, Dictionary<string, List<string>>>? Announcements { get; }

    /// <summary>
    /// Gets the names of all widget classes scanned from game assemblies. Any element tag matching a known widget
    /// name instantiates that widget directly via Gauntlet's <c>WidgetFactory</c>.
    /// </summary>
    public IReadOnlyCollection<string> WidgetNames { get; }

    /// <summary>Gets widget class definitions and their declared properties.</summary>
    public IReadOnlyList<GameWidget> Widgets { get; }

    /// <summary>Gets valid enum member names indexed by enum type name for widget properties.</summary>
    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> Enums { get; }

    /// <summary>
    /// Gets the metadata type names supported by <c>WidgetExtensions.ConvertObject</c> string conversions.
    /// Returns <see langword="null"/> if omitted by the package.
    /// </summary>
    public IReadOnlyCollection<string>? StringConversions { get; }

    public GamePackage(string id, bool isDlc, string? gameVersion, IReadOnlyList<string> modules, IReadOnlyDictionary<string, Dictionary<string, GamePrefabEntry>> prefabs,
        IReadOnlyList<MovieEntry> movies, IReadOnlyDictionary<string, GameViewModel> viewModels,
        IReadOnlyDictionary<string, Dictionary<string, List<string>>>? announcements, IReadOnlyCollection<string> widgetNames,
        IReadOnlyList<GameWidget> widgets, IReadOnlyDictionary<string, IReadOnlyCollection<string>> enums, IReadOnlyCollection<string>? stringConversions)
    {
        WidgetNames = widgetNames;
        Id = id;
        IsDlc = isDlc;
        GameVersion = gameVersion;
        Modules = modules;
        Prefabs = prefabs;
        Movies = movies;
        ViewModels = viewModels;
        Announcements = announcements;
        Widgets = widgets;
        Enums = enums;
        StringConversions = stringConversions;
    }
}

/// <summary>
/// Represents a ViewModel type recorded in <c>types.json</c>, including declared properties, accessibility,
/// methods, and inheritance hierarchy.
/// </summary>
internal sealed record GameViewModel(string Type, string? BaseType, bool Abstract, IReadOnlyDictionary<string, string> Properties,
    IReadOnlyCollection<string> PrivateProperties, IReadOnlyCollection<string> Methods);

/// <summary>
/// Represents a widget class recorded in <c>types.json</c>, including its tag name, type name, base class,
/// and declared properties.
/// </summary>
internal sealed record GameWidget(string Name, string Type, string? BaseType, IReadOnlyDictionary<string, string>? Properties);

/// <summary>
/// Represents a specific game configuration (base game alone, or base game plus an active DLC).
/// Patches must remain valid across configurations since DLC content may override prefabs and screens.
/// </summary>
internal sealed class GameConfiguration
{
    private readonly Dictionary<string, GamePrefab> _prefabs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<MovieEntry>> _movies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GameViewModel> _viewModels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, List<string>>>? _announcements;
    private readonly HashSet<string> _widgetNames = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GameWidget> _widgetsByName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GameWidget> _widgetsByType = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyCollection<string>> _enums = new(StringComparer.Ordinal);
    private readonly HashSet<string>? _stringConversions;
    private Dictionary<string, List<GameViewModel>>? _derived;

    /// <summary>Gets the DLC module name for diagnostic messages, or an empty string for the base game.</summary>
    public string Dlc { get; }

    /// <summary>Gets the normalized game version string (e.g. <c>v1.4.8</c>), or an empty string if unspecified.</summary>
    public string Version { get; }

    public IEnumerable<GamePrefab> Prefabs => _prefabs.Values;

    public GameConfiguration(GamePackage basePackage, GamePackage? dlc, DocumentCache documents)
    {
        Dlc = dlc is null ? "" : string.Join(", ", dlc.Modules);
        Version = GameVersions.Normalize(basePackage.GameVersion) ?? "";
        foreach (var package in dlc is null ? [basePackage] : new[] { basePackage, dlc })
        {
            // Prefabs are keyed by filename across all loaded modules; modules loaded later take precedence.
            foreach (var module in package.Modules)
            {
                if (!package.Prefabs.TryGetValue(module, out var files))
                    continue;
                foreach (var pair in files)
                    _prefabs[pair.Key] = new GamePrefab(pair.Key, module, pair.Value, documents);
            }
            foreach (var pair in package.ViewModels)
                _viewModels[pair.Key] = pair.Value;
            _widgetNames.UnionWith(package.WidgetNames);
            foreach (var widget in package.Widgets)
            {
                _widgetsByName[widget.Name] = widget;
                _widgetsByType[widget.Type] = widget;
            }
            foreach (var pair in package.Enums)
                _enums[pair.Key] = pair.Value;
            // String conversion rules belong to the engine loader in the base package.
            if (package.StringConversions is { } conversions)
                (_stringConversions ??= new HashSet<string>(StringComparer.Ordinal)).UnionWith(conversions);
        }
        // Widget announcements are tracked only when all contributing packages provide them.
        if ((dlc is null ? [basePackage] : new[] { basePackage, dlc }).All(x => x.Announcements is not null))
        {
            _announcements = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal);
            foreach (var package in dlc is null ? [basePackage] : new[] { basePackage, dlc })
            {
                foreach (var pair in package.Announcements!)
                    _announcements[pair.Key] = pair.Value;
            }
        }

        foreach (var entry in basePackage.Movies)
            Add(entry);
        if (dlc is null)
            return;
        // DLC view/screen overrides replace base movie registrations; supplemental ViewModels from base modules are merged.
        foreach (var entry in dlc.Movies.Where(x => x.Module is not null && dlc.Modules.Contains(x.Module)))
        {
            foreach (var list in _movies.Values)
            {
                list.RemoveAll(x => (entry.OverrideView is not null && x.OverrideView == entry.OverrideView)
                                    || (entry.GameStateScreen is not null && x.GameStateScreen == entry.GameStateScreen));
            }
        }
        foreach (var entry in dlc.Movies)
            Add(entry);
    }

    private void Add(MovieEntry entry)
    {
        if (!_movies.TryGetValue(entry.Movie, out var list))
            _movies[entry.Movie] = list = [];
        if (!list.Contains(entry))
            list.Add(entry);
    }

    public GamePrefab? Prefab(string name) => _prefabs.TryGetValue(name, out var prefab) ? prefab : null;

    public IReadOnlyList<MovieEntry> MoviesNamed(string name) => _movies.TryGetValue(name, out var list) ? list : [];

    public GameViewModel? ViewModel(string type) => _viewModels.TryGetValue(type, out var vm) ? vm : null;

    /// <summary>
    /// Resolves base ViewModel definitions from generic metadata representations (e.g. matching <c>Ns.ListVM&lt;Ns.ItemVM&gt;</c>
    /// to <c>Ns.ListVM&lt;T&gt;</c> by name and arity).
    /// </summary>
    private GameViewModel? BaseViewModel(string type)
    {
        if (ViewModel(type) is { } exact)
            return exact;
        var open = type.IndexOf('<');
        if (open < 0)
            return null;
        var arity = Arity(type, open);
        return _viewModels.Values.FirstOrDefault(x => x.Type.Length > open && x.Type[open] == '<'
                                                      && string.CompareOrdinal(x.Type, 0, type, 0, open) == 0 && Arity(x.Type, open) == arity);

        static int Arity(string name, int open)
        {
            var count = 1;
            for (int i = open + 1, depth = 0; i < name.Length; i++)
            {
                switch (name[i])
                {
                    case '<': depth++; break;
                    case '>': depth--; break;
                    case ',' when depth == 0: count++; break;
                }
            }
            return count;
        }
    }

    /// <summary>Enumerates the specified ViewModel and its inherited base types, ordered from most derived to base.</summary>
    private IEnumerable<GameViewModel> SelfAndBases(GameViewModel viewModel)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var type = viewModel; type is not null && seen.Add(type.Type); type = type.BaseType is { } b ? BaseViewModel(b) : null)
            yield return type;
    }

    /// <summary>
    /// Determines whether the specified ViewModel type (or any instantiable derived subtype) declares a property or command method
    /// matching <paramref name="name"/>. Returns <see langword="null"/> if the type is unrecorded.
    /// </summary>
    public bool? Answers(string type, string name, bool command)
    {
        if (ViewModel(type) is not { } viewModel)
            return null;
        if (Has(viewModel))
            return true;
        return DerivedFrom(viewModel.Type).Any(x => !x.Abstract && Has(x));

        bool Has(GameViewModel self) => SelfAndBases(self).Any(x => command
            ? x.Methods.Contains(name)
            : x.Properties.ContainsKey(name) && (ReferenceEquals(x, self) || !x.PrivateProperties.Contains(name)));
    }

    /// <summary>Enumerates all accessible property or method names available on the ViewModel and its subtypes, used for suggesting corrections.</summary>
    public IEnumerable<string> Names(string type, bool command)
    {
        if (ViewModel(type) is not { } viewModel)
            return [];
        return DerivedFrom(viewModel.Type).Prepend(viewModel)
            .SelectMany(SelfAndBases)
            .SelectMany(x => command ? x.Methods : x.Properties.Keys)
            .Distinct(StringComparer.Ordinal);
    }

    /// <summary>Finds all recorded ViewModel types deriving from <paramref name="type"/>.</summary>
    private IReadOnlyList<GameViewModel> DerivedFrom(string type)
    {
        if (_derived is null)
        {
            _derived = new Dictionary<string, List<GameViewModel>>(StringComparer.Ordinal);
            foreach (var viewModel in _viewModels.Values)
            {
                foreach (var ancestor in SelfAndBases(viewModel).Skip(1))
                {
                    if (!_derived.TryGetValue(ancestor.Type, out var list))
                        _derived[ancestor.Type] = list = [];
                    list.Add(viewModel);
                }
            }
        }
        return _derived.TryGetValue(type, out var derived) ? derived : [];
    }

    /// <summary>Resolves a widget definition by its XML tag name.</summary>
    public GameWidget? Widget(string name) => _widgetsByName.TryGetValue(name, out var widget) ? widget : null;

    /// <summary>Resolves a widget definition by its fully qualified type name.</summary>
    public GameWidget? WidgetOfType(string type) => _widgetsByType.TryGetValue(type, out var widget) ? widget : null;

    /// <summary>Returns the valid member names of an enum type used by widget properties, or <see langword="null"/> if unrecorded.</summary>
    public IReadOnlyCollection<string>? EnumMembers(string type) => _enums.TryGetValue(type, out var members) ? members : null;

    /// <summary>Gets the metadata type names supported by <c>ConvertObject</c> string conversions, or <see langword="null"/> if unrecorded.</summary>
    public IReadOnlyCollection<string>? StringConversions => _stringConversions;

    /// <summary>Determines whether <paramref name="name"/> matches a known widget class name.</summary>
    public bool HasWidget(string name) => _widgetNames.Contains(name);

    /// <summary>Indicates whether widget class definitions are available in the loaded packages.</summary>
    public bool KnowsWidgets => _widgetNames.Count > 0;

    /// <summary>Indicates whether widget property change announcements are available in the loaded packages.</summary>
    public bool HasAnnouncements => _announcements is not null;

    /// <summary>
    /// Returns the property types announced under <paramref name="name"/> across the specified widget type hierarchy.
    /// Returns <see langword="null"/> if announcements are not recorded.
    /// </summary>
    public IReadOnlyCollection<string>? Announced(IEnumerable<string> widgetTypes, string name)
    {
        if (_announcements is null)
            return null;
        var types = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var type in widgetTypes)
        {
            if (_announcements.TryGetValue(type, out var byName) && byName.TryGetValue(name, out var announced))
                types.UnionWith(announced);
        }
        return types;
    }

    public string Describe(bool withDlcName) => Dlc.Length == 0 ? "without DLC" : withDlcName ? $"with {Dlc}" : "with DLC";
}

/// <summary>
/// Loads and organizes game GUI packages provided via MSBuild <c>AdditionalFiles</c>.
/// Enables compile-time verification of prefab hierarchies, movie-to-ViewModel pairings, widget properties, and announcements.
/// <para>
/// All game-specific metadata originates from <c>Bannerlord.ReferenceAssemblies.GUI.v3</c> packages (or the multi-version
/// <c>GUI.v3.All</c> bundle), ensuring reliable verification in standalone builds and CI environments.
/// </para>
/// </summary>
internal static class GameGui
{
    /// <summary>The MSBuild metadata attribute identifying the source GUI package ID on <c>AdditionalFiles</c> items.</summary>
    public const string PackageMetadata = "build_metadata.AdditionalFiles.UIExtenderExGamePackage";

    /// <summary>Separates incoming <c>AdditionalFiles</c> into game GUI package files and mod-authored files.</summary>
    public static (Dictionary<string, List<AdditionalText>> Game, ImmutableArray<AdditionalText> Mod) Split(AnalyzerOptions options)
    {
        var game = new Dictionary<string, List<AdditionalText>>(StringComparer.OrdinalIgnoreCase);
        var mod = ImmutableArray.CreateBuilder<AdditionalText>();
        foreach (var file in options.AdditionalFiles)
        {
            if (options.AnalyzerConfigOptionsProvider.GetOptions(file).TryGetValue(PackageMetadata, out var package) && !string.IsNullOrEmpty(package))
            {
                if (!game.TryGetValue(package, out var list))
                    game[package] = list = [];
                list.Add(file);
            }
            else
            {
                mod.Add(file);
            }
        }
        return (game, mod.ToImmutable());
    }

    /// <summary>
    /// Loads game configurations across all targeted game versions and DLC combinations, resolving per-build packages
    /// and multi-version bundles (<c>GUI.v3.All</c>).
    /// </summary>
    public static GameSet Load(Dictionary<string, List<AdditionalText>> files, GameVersions versions, CancellationToken cancellation)
    {
        var perBuild = new List<GamePackage>();
        var bundled = new List<(string Version, List<GamePackage> Packages)>();
        foreach (var pair in files.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var byPath = ByPath(pair.Value);
            if (byPath.ContainsKey(GuiBundle.IndexFile))
            {
                if (GuiBundle.Open(byPath, cancellation) is not { } bundle)
                    continue;
                foreach (var version in bundle.Versions)
                {
                    var packages = version.Packages
                        .Select(x => Read(x.Id, path => bundle.Document(x, path), Trees(bundle, x)))
                        .OfType<GamePackage>()
                        .ToList();
                    bundled.Add((version.GameVersion, packages));
                }
            }
            else if (Read(pair.Key, name => byPath.TryGetValue(name, out var file) ? ParseJson(file, cancellation) : null,
                         path => byPath.TryGetValue(path, out var file) ? new FileTree(file) : null) is { } package)
            {
                perBuild.Add(package);
            }
        }

        // Packages by version: the per-build ones first, so a bundle does not replace what the project references
        var byVersion = new Dictionary<string, List<GamePackage>>(StringComparer.OrdinalIgnoreCase);
        foreach (var package in perBuild)
        {
            var version = GameVersions.Normalize(package.GameVersion) ?? "";
            if (!byVersion.TryGetValue(version, out var list))
                byVersion[version] = list = [];
            list.Add(package);
        }
        var referenced = byVersion.Keys.ToList();
        var fromBundle = new List<string>();
        foreach (var (version, packages) in bundled)
        {
            if (GameVersions.Normalize(version) is { } normalized && !byVersion.ContainsKey(normalized))
            {
                byVersion[normalized] = packages;
                fromBundle.Add(normalized);
            }
        }

        // The versions to check: the ones referenced, and those of the bundle the mod supports - its list, else the
        // version it builds against, else the bundle's newest
        IReadOnlyList<string> wanted = versions.Supported.Count > 0 ? versions.Supported
            : versions.Current is { } current ? [current]
            : referenced.Count == 0 && fromBundle.Count > 0 ? [fromBundle.OrderBy(x => x, GameVersions.Comparer).Last()]
            : [];
        var checkedVersions = referenced
            .Concat(fromBundle.Where(x => wanted.Contains(x, StringComparer.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, GameVersions.Comparer)
            .ToList();

        var documents = new DocumentCache();
        var configurations = new List<GameConfiguration>();
        foreach (var version in checkedVersions)
        {
            var packages = byVersion[version];
            if (packages.FirstOrDefault(x => !x.IsDlc) is not { } basePackage)
                continue;
            configurations.Add(new GameConfiguration(basePackage, null, documents));
            foreach (var dlc in packages.Where(x => x.IsDlc))
                configurations.Add(new GameConfiguration(basePackage, dlc, documents));
        }

        // With a bundle, a version the mod wants that neither it nor a per-build package has is checked against nothing
        var notInBundle = bundled.Count == 0
            ? []
            : wanted.Where(x => !byVersion.ContainsKey(x)).OrderBy(x => x, GameVersions.Comparer).ToList();
        var newestBundled = bundled.Select(x => GameVersions.Normalize(x.Version)).OfType<string>().OrderBy(x => x, GameVersions.Comparer).LastOrDefault();
        return new GameSet(configurations, versions, notInBundle, newestBundled);
    }

    /// <summary>Creates a factory delegate mapping prefab relative paths to <see cref="GameTree"/> instances within a bundle package.</summary>
    private static Func<string, GameTree?> Trees(GuiBundle bundle, BundlePackage package)
    {
        var byFile = new Dictionary<string, GameTree>(StringComparer.Ordinal);
        var entries = bundle.Document(package, "prefabs.json")?.Objects("prefabs").ToList() ?? [];
        for (var i = 0; i < entries.Count && i < package.Trees.Count; i++)
        {
            if (entries[i].String("file") is { } file)
                byFile[file] = bundle.TreeFor(package.Trees[i]);
        }
        return path => byFile.TryGetValue(path, out var tree) ? tree : null;
    }

    private static Dictionary<string, AdditionalText> ByPath(List<AdditionalText> files)
    {
        // Every file by its path under gui/; the data files sit at its root, the prefab trees below it
        var byPath = new Dictionary<string, AdditionalText>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            if (RelativePath(file.Path) is { } relative)
                byPath[relative] = file;
        }
        return byPath;
    }

    /// <summary>
    /// Determines whether the package format is supported. Only format 3 (<c>GUI.v3</c>) is supported;
    /// earlier formats lack widget announcement metadata required by UIX0025.
    /// </summary>
    public static bool ReadsFormat(int version) => version == 3;

    /// <summary>Constructs a <see cref="GamePackage"/> from format 3 manifest and metadata documents.</summary>
    private static GamePackage? Read(string id, Func<string, IReadOnlyDictionary<string, object?>?> data, Func<string, GameTree?> trees)
    {
        // A package of another format is left out whole: its prefabs would be read wrong
        if (data("manifest.json") is not { } manifest || !manifest.TryGetValue("formatVersion", out var version) || version is not double number || !ReadsFormat((int) number))
            return null;
        var movies = data("movies.json");
        var types = data("types.json");

        var prefabs = new Dictionary<string, Dictionary<string, GamePrefabEntry>>(StringComparer.Ordinal);
        foreach (var entry in data("prefabs.json")?.Objects("prefabs") ?? [])
        {
            if (entry.String("name") is not { } name || entry.String("module") is not { } module || entry.String("file") is not { } path
                || trees(path) is not { } tree)
                continue;
            var tags = new HashSet<string>(StringComparer.Ordinal);
            if (entry.TryGetValue("tags", out var list) && list is List<object?> values)
                tags.UnionWith(values.OfType<string>());
            if (!prefabs.TryGetValue(module, out var byName))
                prefabs[module] = byName = new Dictionary<string, GamePrefabEntry>(StringComparer.Ordinal);
            byName[name] = new GamePrefabEntry(tree, tags);
        }

        var modules = manifest.Objects("modules").Select(x => x.String("folder")).OfType<string>().ToList();
        var isDlc = manifest.Objects("modules").Any(x => x.Bool("dlc", false));

        var movieEntries = movies?.Objects("calls")
            .Where(x => x.String("movie") is not null)
            .Select(x => new MovieEntry(x.String("movie")!, x.String("viewModel"), x.Bool("paired", true), x.String("overrideView"), x.String("gameStateScreen"), x.String("module")))
            .ToList() ?? [];

        var viewModels = new Dictionary<string, GameViewModel>(StringComparer.Ordinal);
        foreach (var vm in types?.Objects("viewModels") ?? [])
        {
            if (vm.String("type") is not { } type)
                continue;
            var properties = new Dictionary<string, string>(StringComparer.Ordinal);
            var privateProperties = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in vm.Objects("properties"))
            {
                if (property.String("name") is not { } name || property.String("type") is not { } propertyType || property.Bool("static", false))
                    continue;
                properties[name] = propertyType;
                if (property.String("accessibility") == "private")
                    privateProperties.Add(name);
            }
            var methods = new HashSet<string>(vm.Objects("methods").Select(x => x.String("name")).OfType<string>(), StringComparer.Ordinal);
            viewModels[type] = new GameViewModel(type, vm.String("baseType"), vm.Bool("abstract", false), properties, privateProperties, methods);
        }

        // A package generated before announcements were recorded has widget records without the field: no data, not none
        Dictionary<string, Dictionary<string, List<string>>>? announcements = null;
        var widgetNames = new HashSet<string>(StringComparer.Ordinal);
        var widgets = new List<GameWidget>();
        foreach (var widget in types?.Objects("widgets") ?? [])
        {
            if (widget.String("name") is { } widgetName)
                widgetNames.Add(widgetName);
            if (widget.String("name") is { } recordedName && widget.String("type") is { } recordedType)
            {
                Dictionary<string, string>? properties = null;
                if (widget.ContainsKey("properties"))
                {
                    properties = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var property in widget.Objects("properties"))
                    {
                        if (property.String("name") is { } name && property.String("type") is { } propertyType)
                            properties[name] = propertyType;
                    }
                }
                widgets.Add(new GameWidget(recordedName, recordedType, widget.String("baseType"), properties));
            }
            if (widget.String("type") is not { } type || !widget.TryGetValue("announcements", out var value) || value is not List<object?> list)
                continue;
            announcements ??= new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal);
            var byName = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var entry in list.OfType<IReadOnlyDictionary<string, object?>>())
            {
                if (entry.String("name") is { } name && entry.TryGetValue("types", out var typesValue) && typesValue is List<object?> announcedTypes)
                    byName[name] = announcedTypes.OfType<string>().ToList();
            }
            announcements[type] = byName;
        }

        var enums = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
        foreach (var entry in types?.Objects("enums") ?? [])
        {
            if (entry.String("type") is { } type && entry.TryGetValue("members", out var value) && value is List<object?> members)
                enums[type] = new HashSet<string>(members.OfType<string>(), StringComparer.Ordinal);
        }

        // Absent from a package that does not record them: no data, not no conversions
        IReadOnlyCollection<string>? stringConversions = types is not null && types.TryGetValue("stringConversions", out var recorded) && recorded is List<object?> converted
            ? new HashSet<string>(converted.OfType<string>(), StringComparer.Ordinal)
            : null;

        return new GamePackage(id, isDlc, manifest.String("gameVersion"), modules, prefabs, movieEntries, viewModels, announcements, widgetNames,
            widgets, enums, stringConversions);
    }

    /// <summary>Extracts the path of a file relative to the package's <c>gui/</c> directory root.</summary>
    private static string? RelativePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        var index = normalized.LastIndexOf("/gui/", StringComparison.Ordinal);
        return index < 0 ? null : normalized.Substring(index + "/gui/".Length);
    }

    private static IReadOnlyDictionary<string, object?>? ParseJson(AdditionalText file, CancellationToken cancellation)
    {
        try
        {
            return Json.Parse(file.GetText(cancellation)?.ToString() ?? "") as IReadOnlyDictionary<string, object?>;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}

/// <summary>
/// Encapsulates the active game configurations evaluated during analysis, including supported versions and bundle coverage.
/// </summary>
internal sealed class GameSet(IReadOnlyList<GameConfiguration> configurations, GameVersions versions,
    IReadOnlyList<string>? notInBundle = null, string? newestBundled = null)
{
    public IReadOnlyList<GameConfiguration> Configurations { get; } = configurations;

    public GameVersions Versions { get; } = versions;

    /// <summary>
    /// Gets the list of supported game versions requested by the mod that are absent from referenced GUI bundles (reported via UIX0030).
    /// </summary>
    public IReadOnlyList<string> NotInBundle { get; } = notInBundle ?? [];

    /// <summary>Gets the newest game version present in the referenced GUI bundle, or <see langword="null"/> if no bundle is loaded.</summary>
    public string? NewestBundled { get; } = newestBundled;

    public static GameSet Empty(GameVersions versions) => new([], versions);

    /// <summary>
    /// Emits a version-aware diagnostic: returns the raw diagnostic if the issue occurs across all evaluated configurations,
    /// or wraps it in UIX0024 identifying the subset of affected versions and DLC configurations.
    /// </summary>
    public Diagnostic? ForConfigurations(Diagnostic finding, IReadOnlyCollection<GameConfiguration> present, IReadOnlyCollection<GameConfiguration> holds)
    {
        var holdVersions = VersionsOf(holds);
        if (holds.Count == 0 || !Versions.Reports(holdVersions))
            return null;
        if (holds.Count == present.Count)
            return finding;

        var presentVersions = VersionsOf(present);
        string Part(string version)
        {
            var inVersion = present.Count(x => Same(x.Version, version));
            var holdsIn = holds.Where(x => Same(x.Version, version)).ToList();
            return holdsIn.Count == inVersion ? version : $"{version} {string.Join(", ", holdsIn.Select(x => x.Describe(withDlcName: true)))}".Trim();
        }
        return Diagnostic.Create(Descriptors.HoldsForSomeVersions, finding.Location, finding.AdditionalLocations, finding.Properties.SetItem(FixData.Rule, finding.Id),
            GamePatchChecker.Join(holdVersions, presentVersions, Part), finding.GetMessage(CultureInfo.InvariantCulture));
    }

    private static List<string> VersionsOf(IEnumerable<GameConfiguration> configurations) =>
        configurations.Select(x => x.Version).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, GameVersions.Comparer).ToList();

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
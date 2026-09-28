using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Xml;

namespace Bannerlord.UIExtenderEx.Analyzers.Game;

/// <summary>A movie the game loads, with the ViewModel it binds; see movies.json in the GUI package contract.</summary>
internal sealed record MovieEntry(string Movie, string? ViewModel, bool Paired, string? OverrideView, string? GameStateScreen, string? Module);

/// <summary>A prefab of the game as the package's index lists it: its tree and the element names it uses.</summary>
internal sealed record GamePrefabEntry(GameTree Tree, IReadOnlyCollection<string> Tags);

/// <summary>
/// A prefab tree, <c>{ n, a, c }</c>: the file of a per-build package, or a root node of a bundle. Documents are cached
/// by the tree, so a bundle's tree that several versions share is rebuilt once.
/// </summary>
internal abstract class GameTree
{
    public abstract IReadOnlyDictionary<string, object?>? Root(CancellationToken cancellation);
}

/// <summary>A tree file of a per-build package: <c>{ formatVersion, name, module, root }</c>.</summary>
internal sealed class FileTree(AdditionalText file) : GameTree
{
    public override IReadOnlyDictionary<string, object?>? Root(CancellationToken cancellation) =>
        Json.Parse(file.GetText(cancellation)?.ToString() ?? "") is IReadOnlyDictionary<string, object?> tree
        && tree.TryGetValue("root", out var root) ? root as IReadOnlyDictionary<string, object?> : null;
}

/// <summary>A tree of a bundle, by its root node.</summary>
internal sealed class BundleTree(GuiBundle bundle, int root) : GameTree
{
    public override IReadOnlyDictionary<string, object?>? Root(CancellationToken cancellation) => bundle.Tree(root);
}

/// <summary>A game prefab, the module that ships it, and its document once rebuilt.</summary>
internal sealed class GamePrefab
{
    private readonly GamePrefabEntry _entry;
    private readonly DocumentCache _documents;

    public string Name { get; }
    public string Module { get; }

    /// <summary>Every element name in the prefab, from the index: where it could use another prefab by tag.</summary>
    public IReadOnlyCollection<string> Tags => _entry.Tags;

    public GamePrefab(string name, string module, GamePrefabEntry entry, DocumentCache documents)
    {
        Name = name;
        Module = module;
        _entry = entry;
        _documents = documents;
    }

    /// <summary>The document UIExtenderEx applies patches to, rebuilt from the prefab's tree; null when the tree is broken.</summary>
    public XmlDocument? Document(CancellationToken cancellation) => _documents.Get(_entry.Tree, cancellation);
}

/// <summary>
/// Game documents rebuilt during one analysis, shared by its configurations. Not shared across analyses: an
/// <see cref="XmlDocument"/> is not safe to read from two threads, and the IDE can analyse two compilations at once.
/// <para>
/// A tree holds what <c>WidgetPrefab.LoadFrom</c> loads: every element, attribute and text, without comments
/// (<c>IgnoreComments</c>) or whitespace-only text (a default <see cref="XmlDocument"/> drops it). So the rebuilt document
/// is the one the patches run against, and an XPath selects the same nodes in both.
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

    /// <summary>A node: <c>n</c> its name, <c>a</c> its attributes, <c>c</c> its children, each a node or a text.</summary>
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
/// One GUI package: the game's own modules, or one DLC. Its files come in as additional files that the analyzer targets
/// tag with the package id. Only format 2 is read, which carries no game file: prefabs as trees, with an index.
/// </summary>
internal sealed class GamePackage
{
    public string Id { get; }
    public bool IsDlc { get; }

    /// <summary>The game version of the build, <c>v1.4.8</c>, from the manifest; null when it names none.</summary>
    public string? GameVersion { get; }

    /// <summary>Module folders in the manifest's order, which is load order.</summary>
    public IReadOnlyList<string> Modules { get; }

    /// <summary>Prefabs by module folder, then by prefab name.</summary>
    public IReadOnlyDictionary<string, Dictionary<string, GamePrefabEntry>> Prefabs { get; }

    public IReadOnlyList<MovieEntry> Movies { get; }

    /// <summary>ViewModels by type name: base type and property types.</summary>
    public IReadOnlyDictionary<string, GameViewModel> ViewModels { get; }

    public GamePackage(string id, bool isDlc, string? gameVersion, IReadOnlyList<string> modules, IReadOnlyDictionary<string, Dictionary<string, GamePrefabEntry>> prefabs,
        IReadOnlyList<MovieEntry> movies, IReadOnlyDictionary<string, GameViewModel> viewModels)
    {
        Id = id;
        IsDlc = isDlc;
        GameVersion = gameVersion;
        Modules = modules;
        Prefabs = prefabs;
        Movies = movies;
        ViewModels = viewModels;
    }
}

internal sealed record GameViewModel(string Type, string? BaseType, IReadOnlyDictionary<string, string> Properties);

/// <summary>
/// The game as a mod runs in it: the base package alone, or with one DLC. A patch has to hold in each, because a
/// player may or may not own the DLC, and a DLC replaces prefabs by name and screens by their view or game state.
/// </summary>
internal sealed class GameConfiguration
{
    private readonly Dictionary<string, GamePrefab> _prefabs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<MovieEntry>> _movies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GameViewModel> _viewModels = new(StringComparer.Ordinal);

    /// <summary>Empty for the base game; otherwise the DLC package's module, for messages.</summary>
    public string Dlc { get; }

    /// <summary>The game version, <c>v1.4.8</c>; empty when the package names none.</summary>
    public string Version { get; }

    public IEnumerable<GamePrefab> Prefabs => _prefabs.Values;

    public GameConfiguration(GamePackage basePackage, GamePackage? dlc, DocumentCache documents)
    {
        Dlc = dlc is null ? "" : string.Join(", ", dlc.Modules);
        Version = GameVersions.Normalize(basePackage.GameVersion) ?? "";
        foreach (var package in dlc is null ? [basePackage] : new[] { basePackage, dlc })
        {
            // Prefabs are keyed by file name across every loaded module, and the module loaded later wins
            foreach (var module in package.Modules)
            {
                if (!package.Prefabs.TryGetValue(module, out var files))
                    continue;
                foreach (var pair in files)
                    _prefabs[pair.Key] = new GamePrefab(pair.Key, module, pair.Value, documents);
            }
            foreach (var pair in package.ViewModels)
                _viewModels[pair.Key] = pair.Value;
        }

        foreach (var entry in basePackage.Movies)
            Add(entry);
        if (dlc is null)
            return;
        // A DLC's own class replaces the base entries of the view or game state it stands in for. An entry in the DLC's
        // package whose class is a base module's adds to them instead: the DLC supplies a ViewModel that a base screen
        // loads, as War Sails' NavalSettlementMenuOverlayVM reaches the base game menu overlay.
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

    public string Describe(bool withDlcName) => Dlc.Length == 0 ? "without DLC" : withDlcName ? $"with {Dlc}" : "with DLC";
}

/// <summary>
/// The GUI packages a mod references, read from the additional files the analyzer targets add for them. No packages,
/// no layer 3: the rules that need the game stay silent.
/// </summary>
internal static class GameGui
{
    /// <summary>The item metadata the analyzer targets put on each game file: the package id.</summary>
    public const string PackageMetadata = "build_metadata.AdditionalFiles.UIExtenderExGamePackage";

    /// <summary>Splits the additional files into the game's, by package id, and the mod's own.</summary>
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
    /// The configurations to check against, for every game version checked: the base game, and the base game with
    /// each DLC. A per-build package (<c>GUI.v2</c>, <c>GUI.v2.&lt;Dlc&gt;</c>) is always checked: the project references
    /// it. A bundle (<c>GUI.v2.All</c>) adds the versions the mod supports; for a version both have, the per-build
    /// package is used, being the build the project compiles against.
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

        // The version the compilation's reference assemblies are: the rules that read them run against it alone
        var present = configurations.Select(x => x.Version).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var primary = versions.Current is { } own && present.Contains(own, StringComparer.OrdinalIgnoreCase) ? own
            : referenced.FirstOrDefault(x => present.Contains(x, StringComparer.OrdinalIgnoreCase))
            ?? present.OrderBy(x => x, GameVersions.Comparer).LastOrDefault();
        return new GameSet(configurations, primary, versions);
    }

    /// <summary>A bundle package's trees by the file its prefabs.json entry names, in the entries' order.</summary>
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

    /// <summary>The package format this analyzer reads.</summary>
    private const int FormatVersion = 2;

    /// <summary>A package from its format 2 documents, by path under <c>gui/</c>, and its prefab trees, by the file the index names.</summary>
    private static GamePackage? Read(string id, Func<string, IReadOnlyDictionary<string, object?>?> data, Func<string, GameTree?> trees)
    {
        // A package of another format is left out whole: its prefabs would be read wrong
        if (data("manifest.json") is not { } manifest || !manifest.TryGetValue("formatVersion", out var version) || version is not double number || (int) number != FormatVersion)
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
            foreach (var property in vm.Objects("properties"))
            {
                if (property.String("name") is { } name && property.String("type") is { } propertyType && !property.Bool("static", false))
                    properties[name] = propertyType;
            }
            viewModels[type] = new GameViewModel(type, vm.String("baseType"), properties);
        }

        return new GamePackage(id, isDlc, manifest.String("gameVersion"), modules, prefabs, movieEntries, viewModels);
    }

    /// <summary>
    /// A file's path under the package's <c>gui/</c> folder, as <c>prefabs.json</c> names it: <c>movies.json</c>, or
    /// <c>Native/GUI/Prefabs/…/Options.json</c>. The folder is lower case; a module's own is <c>GUI</c>.
    /// </summary>
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
/// What the game is checked against: the configurations of every version checked, oldest first, and the version the
/// compilation builds against, whose reference assemblies the rules that read types use.
/// </summary>
internal sealed class GameSet(IReadOnlyList<GameConfiguration> configurations, string? primary, GameVersions versions)
{
    public IReadOnlyList<GameConfiguration> Configurations { get; } = configurations;

    /// <summary>The version the compilation builds against, or the one standing in for it; null without packages.</summary>
    public string? Primary { get; } = primary;

    public GameVersions Versions { get; } = versions;

    public bool IsPrimary(GameConfiguration configuration) => string.Equals(configuration.Version, Primary, StringComparison.OrdinalIgnoreCase);

    public static GameSet Empty(GameVersions versions) => new([], null, versions);
}

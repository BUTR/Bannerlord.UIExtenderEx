using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;

namespace Bannerlord.UIExtenderEx.Analyzers.Game;

/// <summary>A movie the game loads, with the ViewModel it binds; see movies.json in the GUI package contract.</summary>
internal sealed record MovieEntry(string Movie, string? ViewModel, bool Paired, string? OverrideView, string? GameStateScreen);

/// <summary>A game prefab file, the module that ships it, and its document once read.</summary>
internal sealed class GamePrefab
{
    private readonly AdditionalText _file;
    private readonly DocumentCache _documents;

    public string Name { get; }
    public string Module { get; }
    public string Path => _file.Path;

    public GamePrefab(string name, string module, AdditionalText file, DocumentCache documents)
    {
        Name = name;
        Module = module;
        _file = file;
        _documents = documents;
    }

    public string Text(CancellationToken cancellation) => _file.GetText(cancellation)?.ToString() ?? "";

    /// <summary>The document, as UIExtenderEx loads it before applying a patch; null when it is not well-formed.</summary>
    public XmlDocument? Document(CancellationToken cancellation) => _documents.Get(_file, cancellation);
}

/// <summary>
/// Game documents parsed during one analysis, shared by its configurations. Not shared across analyses: an
/// <see cref="XmlDocument"/> is not safe to read from two threads, and the IDE can analyse two compilations at once.
/// </summary>
internal sealed class DocumentCache
{
    private readonly Dictionary<AdditionalText, XmlDocument?> _documents = new();

    public XmlDocument? Get(AdditionalText file, CancellationToken cancellation)
    {
        if (_documents.TryGetValue(file, out var cached))
            return cached;
        XmlDocument? document = new();
        try
        {
            document.LoadXml(file.GetText(cancellation)?.ToString() ?? "");
        }
        catch (XmlException)
        {
            document = null;
        }
        _documents[file] = document;
        return document;
    }
}

/// <summary>
/// One GUI package: <c>Bannerlord.ReferenceAssemblies.GUI</c> for the game's own modules, or one per DLC. Its files come
/// in as additional files that the analyzer targets tag with the package id.
/// </summary>
internal sealed class GamePackage
{
    public string Id { get; }
    public bool IsDlc { get; }

    /// <summary>Module folders in the manifest's order, which is load order.</summary>
    public IReadOnlyList<string> Modules { get; }

    /// <summary>Prefab files by module folder, then by prefab name.</summary>
    public IReadOnlyDictionary<string, Dictionary<string, AdditionalText>> Prefabs { get; }

    public IReadOnlyList<MovieEntry> Movies { get; }

    /// <summary>ViewModels by type name: base type and property types.</summary>
    public IReadOnlyDictionary<string, GameViewModel> ViewModels { get; }

    public GamePackage(string id, bool isDlc, IReadOnlyList<string> modules, IReadOnlyDictionary<string, Dictionary<string, AdditionalText>> prefabs,
        IReadOnlyList<MovieEntry> movies, IReadOnlyDictionary<string, GameViewModel> viewModels)
    {
        Id = id;
        IsDlc = isDlc;
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

    public IEnumerable<GamePrefab> Prefabs => _prefabs.Values;

    public GameConfiguration(GamePackage basePackage, GamePackage? dlc, DocumentCache documents)
    {
        Dlc = dlc is null ? "" : string.Join(", ", dlc.Modules);
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
        // A DLC entry replaces the base entries of the view or game state it stands in for
        foreach (var entry in dlc.Movies)
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

    /// <summary>The configurations to check against: the base game, and the base game with each DLC referenced.</summary>
    public static IReadOnlyList<GameConfiguration> Configurations(Dictionary<string, List<AdditionalText>> files, CancellationToken cancellation)
    {
        var packages = new List<GamePackage>();
        foreach (var pair in files.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (Read(pair.Key, pair.Value, cancellation) is { } package)
                packages.Add(package);
        }
        var basePackage = packages.FirstOrDefault(x => !x.IsDlc);
        if (basePackage is null)
            return [];
        var documents = new DocumentCache();
        var result = new List<GameConfiguration> { new(basePackage, null, documents) };
        foreach (var dlc in packages.Where(x => x.IsDlc))
            result.Add(new GameConfiguration(basePackage, dlc, documents));
        return result;
    }

    private static GamePackage? Read(string id, List<AdditionalText> files, CancellationToken cancellation)
    {
        IReadOnlyDictionary<string, object?>? manifest = null, movies = null, types = null;
        var prefabs = new Dictionary<string, Dictionary<string, AdditionalText>>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var (module, isData) = Locate(file.Path);
            if (isData)
            {
                var parsed = ParseJson(file, cancellation);
                switch (Path.GetFileName(file.Path).ToLowerInvariant())
                {
                    case "manifest.json": manifest = parsed; break;
                    case "movies.json": movies = parsed; break;
                    case "types.json": types = parsed; break;
                }
                continue;
            }
            // Only prefabs: the brush files next to them are not prefabs a patch or a tag can name
            if (module is null || !file.Path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || !IsPrefabPath(file.Path))
                continue;
            if (!prefabs.TryGetValue(module, out var byName))
                prefabs[module] = byName = new Dictionary<string, AdditionalText>(StringComparer.Ordinal);
            byName[Path.GetFileNameWithoutExtension(file.Path)] = file;
        }
        if (manifest is null)
            return null;

        var modules = manifest.Objects("modules").Select(x => x.String("folder")).OfType<string>().ToList();
        var isDlc = manifest.Objects("modules").Any(x => x.Bool("dlc", false));

        var movieEntries = movies?.Objects("calls")
            .Where(x => x.String("movie") is not null)
            .Select(x => new MovieEntry(x.String("movie")!, x.String("viewModel"), x.Bool("paired", true), x.String("overrideView"), x.String("gameStateScreen")))
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

        return new GamePackage(id, isDlc, modules, prefabs, movieEntries, viewModels);
    }

    /// <summary>
    /// Where a file sits in the package: <c>…/gui/&lt;Module&gt;/GUI/…</c> for XML, <c>…/gui/&lt;name&gt;.json</c> for data.
    /// </summary>
    private static (string? Module, bool IsData) Locate(string path)
    {
        var segments = path.Replace('\\', '/').Split('/');
        for (var i = segments.Length - 2; i >= 0; i--)
        {
            if (segments[i] != "gui")
                continue;
            if (i == segments.Length - 2)
                return (null, segments[i + 1].EndsWith(".json", StringComparison.OrdinalIgnoreCase));
            if (i + 2 < segments.Length && segments[i + 2] == "GUI")
                return (segments[i + 1], false);
        }
        return (null, false);
    }

    private static bool IsPrefabPath(string path) => path.Replace('\\', '/').IndexOf("/GUI/Prefabs/", StringComparison.Ordinal) >= 0;

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

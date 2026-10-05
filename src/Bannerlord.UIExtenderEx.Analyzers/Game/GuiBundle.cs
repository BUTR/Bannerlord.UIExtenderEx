using Microsoft.CodeAnalysis;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Bannerlord.UIExtenderEx.Analyzers.Game;

/// <summary>
/// Reads deduplicated multi-version GUI bundles (<c>Bannerlord.ReferenceAssemblies.GUI.v3.All</c>).
/// Reconstructs format 3 package documents for each supported game version by indexing records from shared tables.
/// <para>
/// The index manifest (<c>bundle.json</c>) maps file entries across versions to line ranges in tabular data files,
/// while <c>nodes.jsonl</c> stores deduplicated XML prefab DOM nodes.
/// </para>
/// </summary>
internal sealed class GuiBundle
{
    public const string IndexFile = "bundle.json";

    private const int Layout = 1;

    /// <summary>
    /// Parsed data from the most recently accessed bundle, cached across compilation passes.
    /// Keyed by content hash to ensure cache invalidation when package contents change.
    /// </summary>
    private static Shared? _last;

    private readonly Dictionary<string, AdditionalText> _files;
    private readonly Shared _shared;
    private readonly CancellationToken _cancellation;

    public IReadOnlyList<BundleVersion> Versions { get; }

    private GuiBundle(Dictionary<string, AdditionalText> files, Shared shared, IReadOnlyList<BundleVersion> versions, CancellationToken cancellation)
    {
        _files = files;
        _shared = shared;
        Versions = versions;
        _cancellation = cancellation;
    }

    /// <summary>Opens a <see cref="GuiBundle"/> from package files indexed under <c>gui/</c>, or returns <see langword="null"/> if the bundle format or layout is unsupported.</summary>
    public static GuiBundle? Open(Dictionary<string, AdditionalText> files, CancellationToken cancellation)
    {
        if (!files.TryGetValue(IndexFile, out var indexFile)
            || Parse(indexFile.GetText(cancellation)?.ToString() ?? "") is not IReadOnlyDictionary<string, object?> index
            || Number(index, "formatVersion") is not { } format || !GameGui.ReadsFormat(format) || Number(index, "layout") != Layout)
            return null;

        var hash = index.String("contentHash") ?? "";
        var shared = _last is { } last && last.ContentHash == hash && hash.Length > 0 ? last : new Shared(hash);
        _last = shared;

        var versions = new List<BundleVersion>();
        foreach (var version in index.Objects("versions"))
        {
            if (version.String("gameVersion") is not { } gameVersion)
                continue;
            var packages = new List<BundlePackage>();
            foreach (var package in version.Objects("packages"))
            {
                if (package.String("packageId") is not { } id)
                    continue;
                var documents = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>(StringComparer.Ordinal);
                foreach (var file in package.Objects("files"))
                {
                    if (file.String("path") is { } path)
                        documents[path] = file.Objects("properties").ToList();
                }
                var trees = package.TryGetValue("trees", out var t) && t is List<object?> list ? list.OfType<double>().Select(x => (int) x).ToList() : [];
                packages.Add(new BundlePackage(id, documents, trees));
            }
            versions.Add(new BundleVersion(gameVersion, packages));
        }
        return new GuiBundle(files, shared, versions, cancellation);
    }

    /// <summary>Reconstructs a format 3 document from package file properties and shared tabular records.</summary>
    public IReadOnlyDictionary<string, object?>? Document(BundlePackage package, string path)
    {
        if (!package.Files.TryGetValue(path, out var properties))
            return null;
        var document = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in properties)
        {
            if (property.String("name") is not { } name)
                continue;
            if (property.TryGetValue("value", out var value))
            {
                document[name] = value;
                continue;
            }
            if (property.String("table") is not { } table || !property.TryGetValue("runs", out var r) || r is not List<object?> runs)
                continue;
            var items = new List<object?>();
            foreach (var run in runs.OfType<List<object?>>())
            {
                if (run.Count != 2 || run[0] is not double first || run[1] is not double last)
                    continue;
                for (var line = (int) first; line <= (int) last; line++)
                    items.Add(Record(table, line));
            }
            document[name] = items;
        }
        return document;
    }

    private readonly Dictionary<int, BundleTree> _trees = new();

    /// <summary>Returns a cached <see cref="BundleTree"/> for the specified root node index, ensuring shared prefab trees are instantiated once.</summary>
    public BundleTree TreeFor(int root)
    {
        if (!_trees.TryGetValue(root, out var tree))
            _trees[root] = tree = new BundleTree(this, root);
        return tree;
    }

    /// <summary>Reconstructs a prefab node tree (<c>{ n, a, c }</c>) beginning from its root node index.</summary>
    public IReadOnlyDictionary<string, object?>? Tree(int root) => Node(root, int.MaxValue);

    private IReadOnlyDictionary<string, object?>? Node(int line, int parent)
    {
        // Child nodes precede parent nodes in topological order; lower line numbers ensure acyclic references.
        if (line < 0 || line >= parent)
            return null;
        if (_shared.Nodes.TryGetValue(line, out var cached))
            return cached;
        _cancellation.ThrowIfCancellationRequested();
        if (Record("nodes.jsonl", line) is not IReadOnlyDictionary<string, object?> stored)
            return null;
        var node = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in stored)
        {
            if (pair.Key == "c" && pair.Value is List<object?> children)
            {
                var nested = new List<object?>();
                foreach (var child in children)
                    nested.Add(child is double index ? Node((int) index, line) ?? throw new FormatException($"Node {line} names a broken child") : child);
                node["c"] = nested;
            }
            else
            {
                node[pair.Key] = pair.Value;
            }
        }
        return _shared.Nodes.GetOrAdd(line, node);
    }

    /// <summary>Retrieves and parses a single line record from a shared table file, caching parsed results.</summary>
    private object? Record(string table, int line)
    {
        if (_shared.Records.TryGetValue((table, line), out var cached))
            return cached;
        var lines = _shared.Lines.GetOrAdd(table, name => _files.TryGetValue(name, out var file)
            ? (file.GetText(_cancellation)?.ToString() ?? "").Split('\n')
            : []);
        var record = line >= 0 && line < lines.Length ? Parse(lines[line]) : null;
        return _shared.Records.GetOrAdd((table, line), record);
    }

    private static object? Parse(string text)
    {
        try
        {
            return Json.Parse(text);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static int? Number(IReadOnlyDictionary<string, object?> obj, string key) =>
        obj.TryGetValue(key, out var value) && value is double number ? (int) number : null;

    /// <summary>Thread-safe cache holding parsed table lines, records, and nodes shared across analyses of the same bundle.</summary>
    private sealed class Shared(string contentHash)
    {
        public string ContentHash { get; } = contentHash;
        public ConcurrentDictionary<string, string[]> Lines { get; } = new(StringComparer.Ordinal);
        public ConcurrentDictionary<(string Table, int Line), object?> Records { get; } = new();
        public ConcurrentDictionary<int, IReadOnlyDictionary<string, object?>> Nodes { get; } = new();
    }
}

/// <summary>Represents a game version entry within a bundle, ordered with base packages preceding DLC packages.</summary>
internal sealed record BundleVersion(string GameVersion, IReadOnlyList<BundlePackage> Packages);

/// <summary>Represents a package definition within a bundled version, mapping relative file paths to table records and prefab root nodes.</summary>
internal sealed record BundlePackage(string Id, IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>> Files, IReadOnlyList<int> Trees);
using Microsoft.CodeAnalysis;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Bannerlord.UIExtenderEx.Analyzers.Game;

/// <summary>
/// A <c>Bannerlord.ReferenceAssemblies.GUI.v2.All</c> package: the newest build of every release game version, each
/// distinct format 2 record stored once. <c>bundle.json</c> says, for every version and package, which lines of which
/// table each format 2 file is made of; <c>nodes.jsonl</c> holds every distinct prefab node, children by line.
/// <para>
/// It is read back into the format 2 documents of each version's packages, as parsed JSON, so the rest of the analyzer
/// reads a version of the bundle as it reads a per-build package. Only the lines of the versions checked are parsed.
/// </para>
/// </summary>
internal sealed class GuiBundle
{
    public const string IndexFile = "bundle.json";

    private const int FormatVersion = 2;
    private const int Layout = 1;

    /// <summary>
    /// What was parsed of the last bundle, shared across compilations: the IDE analyses the same package again on every
    /// change, and the tables hold every version. Keyed by the content hash, so another package version starts over.
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

    /// <summary>The bundle whose files these are, keyed by their path under <c>gui/</c>; null when it is not one this reads.</summary>
    public static GuiBundle? Open(Dictionary<string, AdditionalText> files, CancellationToken cancellation)
    {
        if (!files.TryGetValue(IndexFile, out var indexFile)
            || Parse(indexFile.GetText(cancellation)?.ToString() ?? "") is not IReadOnlyDictionary<string, object?> index
            || Number(index, "formatVersion") != FormatVersion || Number(index, "layout") != Layout)
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

    /// <summary>A format 2 file of a package, as parsed JSON: its inline properties, and its arrays from their tables.</summary>
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

    /// <summary>The tree of a root node, one per node, so a prefab several versions share is one document.</summary>
    public BundleTree TreeFor(int root)
    {
        if (!_trees.TryGetValue(root, out var tree))
            _trees[root] = tree = new BundleTree(this, root);
        return tree;
    }

    /// <summary>A prefab tree, from its root node: the format 2 <c>{ n, a, c }</c> node with its children nested.</summary>
    public IReadOnlyDictionary<string, object?>? Tree(int root) => Node(root, int.MaxValue);

    private IReadOnlyDictionary<string, object?>? Node(int line, int parent)
    {
        // Children come before their parents, so a child's line is always the lower; a cycle would be a broken bundle
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

    /// <summary>One line of a table, parsed once per bundle.</summary>
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

    /// <summary>What is parsed of one bundle, safe to share between analyses: nothing in it changes once added.</summary>
    private sealed class Shared(string contentHash)
    {
        public string ContentHash { get; } = contentHash;
        public ConcurrentDictionary<string, string[]> Lines { get; } = new(StringComparer.Ordinal);
        public ConcurrentDictionary<(string Table, int Line), object?> Records { get; } = new();
        public ConcurrentDictionary<int, IReadOnlyDictionary<string, object?>> Nodes { get; } = new();
    }
}

/// <summary>One game version in a bundle, its packages base first.</summary>
internal sealed record BundleVersion(string GameVersion, IReadOnlyList<BundlePackage> Packages);

/// <summary>One package of a version: its files by path under <c>gui/</c>, and the root node of each prefabs.json entry.</summary>
internal sealed record BundlePackage(string Id, IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>> Files, IReadOnlyList<int> Trees);

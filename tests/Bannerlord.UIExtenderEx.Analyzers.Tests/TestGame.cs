using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers.Tests;

/// <summary>GUI data as the analyzer targets hand it to the compiler: each file's path, text, and the package it is tagged with.</summary>
internal interface ITestPackage
{
    IEnumerable<(string Path, string Text, string Package)> Files();
}

/// <summary>A format 2 file as a list of top-level properties: an inline JSON value, or an array of records.</summary>
internal sealed class TestDocument(string path, IReadOnlyList<(string Name, string? Value, IReadOnlyList<string>? Items)> properties)
{
    public string Path { get; } = path;
    public IReadOnlyList<(string Name, string? Value, IReadOnlyList<string>? Items)> Properties { get; } = properties;
}

/// <summary>
/// A GUI package in format 2: manifest.json, movies.json, types.json, and each prefab as a tree under
/// <c>gui/&lt;Module&gt;/GUI/Prefabs</c> listed in prefabs.json. Tests give the prefabs as XML; they are converted the way
/// the package generator converts the game's, without comments or whitespace-only text. Several can be passed
/// together: a base package and DLC packages. <see cref="TestBundle"/> packs several into a <c>GUI.v2.All</c>.
/// </summary>
internal sealed class TestGame : ITestPackage
{
    private readonly bool _dlc;
    private readonly string[] _modules;
    private readonly List<string> _movies = [];
    private readonly List<string> _viewModels = [];
    private readonly List<(string Module, string Name, string Xml)> _prefabs = [];
    private int _formatVersion = 2;

    public string Package { get; }
    public bool IsDlc => _dlc;
    public string GameVersion { get; private set; } = "v1.4.8";
    public int ChangeSet { get; private set; } = 119303;

    public TestGame(string package, bool dlc, params string[] modules)
    {
        Package = package;
        _dlc = dlc;
        _modules = modules;
    }

    public static TestGame Base() => new("Bannerlord.ReferenceAssemblies.GUI.v2", false, "Native", "SandBox");

    public static TestGame NavalDlc() => new("Bannerlord.ReferenceAssemblies.GUI.v2.NavalDLC", true, "NavalDLC");

    /// <summary>Writes another format version into the manifest, for the tests of packages the analyzer does not read.</summary>
    public TestGame Format(int version)
    {
        _formatVersion = version;
        return this;
    }

    /// <summary>The game build the package is of; <c>v1.4.8</c> by default.</summary>
    public TestGame Version(string gameVersion, int changeSet = 1)
    {
        GameVersion = gameVersion;
        ChangeSet = changeSet;
        return this;
    }

    /// <summary>A movies.json entry; <paramref name="module"/> is the module of its class, this package's first by default.</summary>
    public TestGame Movie(string movie, string? viewModel, string? gameStateScreen = null, bool paired = true, string? overrideView = null, string? module = null)
    {
        _movies.Add($$"""{"movie":{{Quote(movie)}},"viewModel":{{Quote(viewModel)}},"module":{{Quote(module ?? _modules[0])}},"overrideView":{{Quote(overrideView)}},"gameStateScreen":{{Quote(gameStateScreen)}},"paired":{{(paired ? "true" : "false")}}}""");
        return this;
    }

    public TestGame ViewModel(string type, string? baseType, params (string Name, string Type)[] properties)
    {
        var props = string.Join(",", properties.Select(p => $$"""{"name":{{Quote(p.Name)}},"type":{{Quote(p.Type)}},"accessibility":"public","static":false}"""));
        _viewModels.Add($$"""{"type":{{Quote(type)}},"baseType":{{Quote(baseType ?? "TaleWorlds.Library.ViewModel")}},"properties":[{{props}}],"methods":[]}""");
        return this;
    }

    public TestGame Prefab(string name, string xml, string? module = null)
    {
        _prefabs.Add((module ?? _modules[0], name, xml));
        return this;
    }

    /// <summary>The package's files other than the prefab trees, each as its top-level properties.</summary>
    public IReadOnlyList<TestDocument> Documents()
    {
        static (string, string?, IReadOnlyList<string>?) Value(string name, string value) => (name, value, null);
        static (string, string?, IReadOnlyList<string>?) Items(string name, IReadOnlyList<string> items) => (name, null, items);
        var format = Value("formatVersion", _formatVersion.ToString());
        var modules = _modules.Select(m => $$"""{"folder":{{Quote(m)}},"id":{{Quote(m)}},"version":{{Quote(GameVersion)}},"moduleType":"Official","dependedModules":[],"dlc":{{(_dlc ? "true" : "false")}}}""").ToList();
        return
        [
            new("manifest.json", [format, Value("package", Quote(Package)), Value("gameVersion", Quote(GameVersion)), Value("changeSet", ChangeSet.ToString()), Value("buildId", "1"), Items("modules", modules)]),
            new("movies.json", [format, Items("calls", _movies), Items("unresolved", [])]),
            new("prefabs.json", [format, Items("prefabs", Trees().Select(t => t.Index).ToList())]),
            new("types.json", [format, Items("widgets", []), Items("viewModels", _viewModels), Items("enums", [])]),
        ];
    }

    /// <summary>The prefab trees in prefabs.json order: each tree's name, module, file, root element and index entry.</summary>
    public IReadOnlyList<(string Name, string Module, string File, XElement Root, string Index)> Trees() => _prefabs.Select(p =>
    {
        var file = $"{p.Module}/GUI/Prefabs/{p.Name}.json";
        var element = XDocument.Parse(p.Xml).Root!;
        var tags = element.DescendantsAndSelf().Select(e => e.Name.LocalName).Distinct().OrderBy(t => t, System.StringComparer.Ordinal);
        var window = element.Name.LocalName == "Window" ? element : element.Element("Window");
        var rootTag = window?.Elements().FirstOrDefault()?.Name.LocalName;
        var index = $$"""{"name":{{Quote(p.Name)}},"module":{{Quote(p.Module)}},"file":{{Quote(file)}},"rootTag":{{Quote(rootTag)}},"tags":[{{string.Join(",", tags.Select(t => Quote(t)))}}],"parameters":[]}""";
        return (p.Name, p.Module, file, element, index);
    }).ToList();

    /// <summary>The files as the package's props file lists them: path, text, and the package they are tagged with.</summary>
    public IEnumerable<(string Path, string Text, string Package)> Files()
    {
        var root = $"/packages/{Package.ToLowerInvariant()}/{GameVersion.Substring(1)}.{ChangeSet}/gui";
        foreach (var document in Documents())
        {
            var properties = document.Properties.Select(p => $"{Quote(p.Name)}:{p.Value ?? "[" + string.Join(",", p.Items!) + "]"}");
            yield return ($"{root}/{document.Path}", "{" + string.Join(",", properties) + "}", Package);
        }
        foreach (var tree in Trees())
            yield return ($"{root}/{tree.File}", $$"""{"formatVersion":{{_formatVersion}},"name":{{Quote(tree.Name)}},"module":{{Quote(tree.Module)}},"root":{{Tree(tree.Root)}}}""", Package);
    }

    /// <summary>A node of the tree: <c>n</c>, <c>a</c> in document order, <c>c</c> with nodes and non-whitespace texts.</summary>
    private static string Tree(XElement element) => Node(element, Tree);

    /// <summary>
    /// A node, <c>{ n, a, c }</c>, with each child element written by <paramref name="child"/>: nested, in a tree file, or
    /// as its line in a bundle's node table.
    /// </summary>
    public static string Node(XElement element, System.Func<XElement, string> child)
    {
        var builder = new StringBuilder("{\"n\":").Append(Quote(element.Name.LocalName));
        if (element.HasAttributes)
            builder.Append(",\"a\":{").Append(string.Join(",", element.Attributes().Select(a => $"{Quote(a.Name.LocalName)}:{Quote(a.Value)}"))).Append('}');
        var children = element.Nodes()
            .Select(n => n switch
            {
                XElement e => child(e),
                XText text when !string.IsNullOrWhiteSpace(text.Value) => Quote(text.Value),
                _ => null,
            })
            .OfType<string>()
            .ToList();
        if (children.Count > 0)
            builder.Append(",\"c\":[").Append(string.Join(",", children)).Append(']');
        return builder.Append('}').ToString();
    }

    public static string Quote(string? value)
    {
        if (value is null)
            return "null";
        var builder = new StringBuilder("\"");
        foreach (var c in value)
        {
            builder.Append(c switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ => c.ToString(),
            });
        }
        return builder.Append('"').ToString();
    }
}

/// <summary>
/// A <c>Bannerlord.ReferenceAssemblies.GUI.v2.All</c> package packed from format 2 packages as the <c>bundle-gui</c>
/// verb packs them: versions ascending, base package first; every distinct record once in a table per file and array,
/// every distinct prefab node once in <c>nodes.jsonl</c>, children first; <c>bundle.json</c> naming each file's lines.
/// </summary>
internal sealed class TestBundle : ITestPackage
{
    public const string Id = "Bannerlord.ReferenceAssemblies.GUI.v2.All";

    private readonly TestGame[] _games;
    private int _layout = 1;

    public TestBundle(params TestGame[] games) => _games = games;

    /// <summary>Writes another layout into bundle.json, for the tests of bundles the analyzer does not read.</summary>
    public TestBundle Layout(int layout)
    {
        _layout = layout;
        return this;
    }

    public IEnumerable<(string Path, string Text, string Package)> Files()
    {
        var tables = new SortedDictionary<string, Table>(System.StringComparer.Ordinal);
        var nodes = new Table();
        var versions = new List<string>();
        foreach (var version in _games.GroupBy(g => g.GameVersion).OrderBy(g => g.Key, new VersionOrder()))
        {
            var packages = new List<string>();
            foreach (var game in version.OrderBy(g => g.IsDlc).ThenBy(g => g.Package, System.StringComparer.Ordinal))
            {
                var files = new List<string>();
                foreach (var document in game.Documents().OrderBy(d => d.Path, System.StringComparer.Ordinal))
                {
                    var properties = new List<string>();
                    foreach (var (name, value, items) in document.Properties)
                    {
                        if (items is null || items.Count == 0)
                        {
                            properties.Add($$"""{"name":{{TestGame.Quote(name)}},"value":{{value ?? "[]"}}}""");
                            continue;
                        }
                        var tableName = $"records.{document.Path.Substring(0, document.Path.Length - ".json".Length)}.{name}.jsonl";
                        if (!tables.TryGetValue(tableName, out var table))
                            tables[tableName] = table = new Table();
                        var lines = items.Select(table.Add).ToList();
                        properties.Add($$"""{"name":{{TestGame.Quote(name)}},"table":{{TestGame.Quote(tableName)}},"runs":[{{string.Join(",", Runs(lines))}}]}""");
                    }
                    files.Add($$"""{"path":{{TestGame.Quote(document.Path)}},"properties":[{{string.Join(",", properties)}}]}""");
                }
                var trees = game.Trees().Select(t => Intern(t.Root, nodes)).ToList();
                packages.Add($$"""{"packageId":{{TestGame.Quote(game.Package)}},"packageVersion":{{TestGame.Quote($"{game.GameVersion.Substring(1)}.{game.ChangeSet}")}},"buildId":1,"changeSet":{{game.ChangeSet}},"date":"2026-09-28T00:00:00Z","branches":["public"],"files":[{{string.Join(",", files)}}],"trees":[{{string.Join(",", trees)}}]}""");
            }
            versions.Add($$"""{"gameVersion":{{TestGame.Quote(version.Key)}},"packages":[{{string.Join(",", packages)}}]}""");
        }

        var contents = tables.ToDictionary(t => t.Key, t => t.Value.Text());
        contents["nodes.jsonl"] = nodes.Text();
        var body = string.Join(",", versions);
        var hash = Hash(string.Join("\n", contents.OrderBy(c => c.Key, System.StringComparer.Ordinal).Select(c => c.Key + "\n" + c.Value)) + body);
        const string root = "/packages/bannerlord.referenceassemblies.gui.v2.all/2026.9.28.1/gui";
        yield return ($"{root}/bundle.json", $$"""{"formatVersion":2,"layout":{{_layout}},"contentHash":{{TestGame.Quote(hash)}},"versions":[{{body}}]}""", Id);
        foreach (var content in contents)
            yield return ($"{root}/{content.Key}", content.Value, Id);
    }

    /// <summary>An element's line in the node table, its children written first.</summary>
    private static int Intern(XElement element, Table nodes) => nodes.Add(TestGame.Node(element, child => Intern(child, nodes).ToString()));

    private static IEnumerable<string> Runs(List<int> lines)
    {
        for (var i = 0; i < lines.Count;)
        {
            var j = i;
            while (j + 1 < lines.Count && lines[j + 1] == lines[j] + 1)
                j++;
            yield return $"[{lines[i]},{lines[j]}]";
            i = j + 1;
        }
    }

    private static string Hash(string text)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text)).Take(16).Select(b => b.ToString("x2")));
    }

    /// <summary>Distinct lines, each numbered from 0 in the order first added.</summary>
    private sealed class Table
    {
        private readonly List<string> _lines = [];
        private readonly Dictionary<string, int> _index = new(System.StringComparer.Ordinal);

        public int Add(string line)
        {
            if (!_index.TryGetValue(line, out var index))
            {
                _index[line] = index = _lines.Count;
                _lines.Add(line);
            }
            return index;
        }

        public string Text() => string.Join("\n", _lines) + "\n";
    }

    private sealed class VersionOrder : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            var a = (x ?? "").TrimStart('v').Split('.').Select(int.Parse).ToArray();
            var b = (y ?? "").TrimStart('v').Split('.').Select(int.Parse).ToArray();
            for (var i = 0; i < System.Math.Min(a.Length, b.Length); i++)
            {
                if (a[i] != b[i])
                    return a[i].CompareTo(b[i]);
            }
            return a.Length.CompareTo(b.Length);
        }
    }
}

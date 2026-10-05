using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers.Tests;

/// <summary>Represents mock GUI package data supplied to the compiler, providing virtual file paths, text content, and package identity metadata.</summary>
internal interface ITestPackage
{
    IEnumerable<(string Path, string Text, string Package)> Files();
}

/// <summary>Represents a format 3 document composed of top-level JSON properties and record arrays.</summary>
internal sealed class TestDocument(string path, IReadOnlyList<(string Name, string? Value, IReadOnlyList<string>? Items)> properties)
{
    public string Path { get; } = path;
    public IReadOnlyList<(string Name, string? Value, IReadOnlyList<string>? Items)> Properties { get; } = properties;
}

/// <summary>
/// Constructs an in-memory GUI package conforming to format 3 (<c>manifest.json</c>, <c>movies.json</c>, <c>types.json</c>,
/// and JSON prefab trees under <c>gui/&lt;Module&gt;/GUI/Prefabs</c> referenced by <c>prefabs.json</c>).
/// Automatically converts XML prefab definitions to JSON representation without comments or extraneous whitespace.
/// Supports combining base modules and DLC packages, or packing into a unified <see cref="TestBundle"/>.
/// </summary>
internal sealed class TestGame : ITestPackage
{
    private readonly bool _dlc;
    private readonly string[] _modules;
    private readonly List<string> _movies = [];
    private readonly List<string> _viewModels = [];
    private readonly List<string> _widgets = [];
    private readonly List<string> _enums = [];
    private List<string>? _stringConversions;
    private readonly List<(string Module, string Name, string Xml)> _prefabs = [];
    private int _formatVersion = 3;

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

    public static TestGame Base() => new("Bannerlord.ReferenceAssemblies.GUI.v3", false, "Native", "SandBox");

    public static TestGame NavalDlc() => new("Bannerlord.ReferenceAssemblies.GUI.v3.NavalDLC", true, "NavalDLC");

    /// <summary>Sets a custom format version in the manifest to test compatibility handling for unsupported bundle formats.</summary>
    public TestGame Format(int version)
    {
        _formatVersion = version;
        return this;
    }

    /// <summary>Configures the game version and changeset for the package (defaults to <c>v1.4.8</c>).</summary>
    public TestGame Version(string gameVersion, int changeSet = 1)
    {
        GameVersion = gameVersion;
        ChangeSet = changeSet;
        return this;
    }

    /// <summary>Registers a movie entry in <c>movies.json</c>, associating a Gauntlet movie name with its ViewModel.</summary>
    public TestGame Movie(string movie, string? viewModel, string? gameStateScreen = null, bool paired = true, string? overrideView = null, string? module = null)
    {
        _movies.Add($$"""{"movie":{{Quote(movie)}},"viewModel":{{Quote(viewModel)}},"module":{{Quote(module ?? _modules[0])}},"overrideView":{{Quote(overrideView)}},"gameStateScreen":{{Quote(gameStateScreen)}},"paired":{{(paired ? "true" : "false")}}}""");
        return this;
    }

    public TestGame ViewModel(string type, string? baseType, params (string Name, string Type)[] properties) =>
        ViewModel(type, baseType, [], properties);

    /// <summary>Registers a ViewModel type definition with properties and command methods in <c>types.json</c>.</summary>
    public TestGame ViewModel(string type, string? baseType, IReadOnlyList<string> methods, params (string Name, string Type)[] properties)
    {
        var props = string.Join(",", properties.Select(p => $$"""{"name":{{Quote(p.Name)}},"type":{{Quote(p.Type)}},"accessibility":"public","static":false}"""));
        var methodRecords = string.Join(",", methods.Select(m => $$"""{"name":{{Quote(m)}},"accessibility":"public","returnType":"System.Void","parameters":[]}"""));
        _viewModels.Add($$"""{"type":{{Quote(type)}},"baseType":{{Quote(baseType ?? "TaleWorlds.Library.ViewModel")}},"properties":[{{props}}],"methods":[{{methodRecords}}]}""");
        return this;
    }

    /// <summary>
    /// Registers a widget type with announced property types under <c>widgets[].announcements</c> without explicit property definitions.
    /// </summary>
    public TestGame Widget(string type, string? baseType, params (string Name, string[] Types)[] announcements) =>
        Widget(type, baseType, null, announcements);

    /// <summary>Registers a widget type definition with public properties and announced types.</summary>
    public TestGame Widget(string type, string? baseType, IReadOnlyList<(string Name, string Type)>? properties, params (string Name, string[] Types)[] announcements)
    {
        var name = type.Substring(type.LastIndexOf('.') + 1);
        var announced = string.Join(",", announcements.OrderBy(a => a.Name, System.StringComparer.Ordinal)
            .Select(a => $$"""{"name":{{Quote(a.Name)}},"types":[{{string.Join(",", a.Types.Select(t => Quote(t)))}}]}"""));
        var props = properties is null
            ? ""
            : ",\"properties\":[" + string.Join(",", properties.OrderBy(p => p.Name, System.StringComparer.Ordinal)
                .Select(p => $$"""{"name":{{Quote(p.Name)}},"type":{{Quote(p.Type)}},"canRead":true,"canWrite":true}""")) + "]";
        _widgets.Add($$"""{"name":{{Quote(name)}},"type":{{Quote(type)}},"baseType":{{Quote(baseType)}},"module":null,"assembly":"TaleWorlds.GauntletUI.dll","abstract":false,"events":[],"unresolvedEvents":[],"announcements":[{{announced}}],"unresolvedAnnouncements":[]{{props}}}""");
        return this;
    }

    /// <summary>Registers an enum type definition and its member names in <c>types.json</c>.</summary>
    public TestGame Enum(string type, params string[] members)
    {
        _enums.Add($$"""{"type":{{Quote(type)}},"members":[{{string.Join(",", members.OrderBy(m => m, System.StringComparer.Ordinal).Select(m => Quote(m)))}}]}""");
        return this;
    }

    /// <summary>Registers supported string conversion target types recognized by <c>ConvertObject</c> in <c>types.json</c>.</summary>
    public TestGame StringConversions(params string[] types)
    {
        _stringConversions = types.OrderBy(t => t, System.StringComparer.Ordinal).Select(t => Quote(t)).ToList();
        return this;
    }

    public TestGame Prefab(string name, string xml, string? module = null)
    {
        _prefabs.Add((module ?? _modules[0], name, xml));
        return this;
    }

    /// <summary>Generates document models for metadata files (<c>manifest.json</c>, <c>movies.json</c>, <c>prefabs.json</c>, and <c>types.json</c>).</summary>
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
            new("types.json", _stringConversions is null
                ? [format, Items("widgets", _widgets), Items("viewModels", _viewModels), Items("enums", _enums)]
                : [format, Items("widgets", _widgets), Items("viewModels", _viewModels), Items("enums", _enums), Items("stringConversions", _stringConversions)]),
        ];
    }

    /// <summary>Generates JSON prefab trees and corresponding index metadata entries for <c>prefabs.json</c>.</summary>
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

    /// <summary>Yields all mock files within the package directory structure tagged with their package identity.</summary>
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

    /// <summary>Serializes an XML element into JSON prefab format (<c>n</c> for name, <c>a</c> for attributes, <c>c</c> for children).</summary>
    private static string Tree(XElement element) => Node(element, Tree);

    /// <summary>
    /// Serializes an XML element into node object format using the specified child serializer callback (inline or indexed intern reference).
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
/// Packs multiple <see cref="TestGame"/> packages into a unified <c>Bannerlord.ReferenceAssemblies.GUI.v3.All</c> bundle,
/// matching the structure produced by the <c>bundle-gui</c> tool: deduplicated record tables, an interned <c>nodes.jsonl</c> table,
/// and a master <c>bundle.json</c> manifest.
/// </summary>
internal sealed class TestBundle : ITestPackage
{
    public const string Id = "Bannerlord.ReferenceAssemblies.GUI.v3.All";

    private readonly TestGame[] _games;
    private int _layout = 1;

    public TestBundle(params TestGame[] games) => _games = games;

    /// <summary>Sets a custom bundle layout version in <c>bundle.json</c> to test compatibility handling for unsupported bundle formats.</summary>
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
        yield return ($"{root}/bundle.json", $$"""{"formatVersion":3,"layout":{{_layout}},"contentHash":{{TestGame.Quote(hash)}},"versions":[{{body}}]}""", Id);
        foreach (var content in contents)
            yield return ($"{root}/{content.Key}", content.Value, Id);
    }

    /// <summary>Interns an XML element into the deduplicated node table (bottom-up, children first) and returns its line index.</summary>
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

    /// <summary>Maintains a deduplicated set of text lines mapped to zero-based insertion indices.</summary>
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
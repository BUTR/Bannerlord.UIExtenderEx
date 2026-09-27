using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers.Tests;

/// <summary>
/// A GUI package in format 2: manifest.json, movies.json, types.json, and each prefab as a tree under
/// <c>gui/&lt;Module&gt;/GUI/Prefabs</c> listed in prefabs.json. Tests give the prefabs as XML; they are converted the way
/// the package generator converts the game's, without comments or whitespace-only text. Several can be passed
/// together: a base package and DLC packages.
/// </summary>
internal sealed class TestGame
{
    private readonly string _package;
    private readonly bool _dlc;
    private readonly string[] _modules;
    private readonly List<string> _movies = [];
    private readonly List<string> _viewModels = [];
    private readonly List<(string Module, string Name, string Xml)> _prefabs = [];
    private int _formatVersion = 2;

    public TestGame(string package, bool dlc, params string[] modules)
    {
        _package = package;
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

    /// <summary>The files as the package's props file lists them: path, text, and the package they are tagged with.</summary>
    public IEnumerable<(string Path, string Text, string Package)> Files()
    {
        var root = $"/packages/{_package.ToLowerInvariant()}/1.4.8.119303/gui";
        var modules = string.Join(",", _modules.Select(m => $$"""{"folder":{{Quote(m)}},"id":{{Quote(m)}},"version":"v1.4.8","moduleType":"Official","dependedModules":[],"dlc":{{(_dlc ? "true" : "false")}}}"""));
        yield return ($"{root}/manifest.json", $$"""{"formatVersion":{{_formatVersion}},"package":{{Quote(_package)}},"gameVersion":"v1.4.8","changeSet":119303,"buildId":1,"modules":[{{modules}}]}""", _package);
        yield return ($"{root}/movies.json", $$"""{"formatVersion":{{_formatVersion}},"calls":[{{string.Join(",", _movies)}}],"unresolved":[]}""", _package);
        yield return ($"{root}/types.json", $$"""{"formatVersion":{{_formatVersion}},"widgets":[],"viewModels":[{{string.Join(",", _viewModels)}}],"enums":[]}""", _package);

        var index = new List<string>();
        foreach (var (module, name, xml) in _prefabs)
        {
            var file = $"{module}/GUI/Prefabs/{name}.json";
            var element = XDocument.Parse(xml).Root!;
            var tags = element.DescendantsAndSelf().Select(e => e.Name.LocalName).Distinct().OrderBy(t => t, System.StringComparer.Ordinal);
            var window = element.Name.LocalName == "Window" ? element : element.Element("Window");
            var rootTag = window?.Elements().FirstOrDefault()?.Name.LocalName;
            index.Add($$"""{"name":{{Quote(name)}},"module":{{Quote(module)}},"file":{{Quote(file)}},"rootTag":{{Quote(rootTag)}},"tags":[{{string.Join(",", tags.Select(t => Quote(t)))}}],"parameters":[]}""");
            yield return ($"{root}/{file}", $$"""{"formatVersion":{{_formatVersion}},"name":{{Quote(name)}},"module":{{Quote(module)}},"root":{{Tree(element)}}}""", _package);
        }
        yield return ($"{root}/prefabs.json", $$"""{"formatVersion":{{_formatVersion}},"prefabs":[{{string.Join(",", index)}}]}""", _package);
    }

    /// <summary>A node of the tree: <c>n</c>, <c>a</c> in document order, <c>c</c> with nodes and non-whitespace texts.</summary>
    private static string Tree(XElement element)
    {
        var builder = new StringBuilder("{\"n\":").Append(Quote(element.Name.LocalName));
        if (element.HasAttributes)
            builder.Append(",\"a\":{").Append(string.Join(",", element.Attributes().Select(a => $"{Quote(a.Name.LocalName)}:{Quote(a.Value)}"))).Append('}');
        var children = element.Nodes()
            .Select(n => n switch
            {
                XElement child => Tree(child),
                XText text when !string.IsNullOrWhiteSpace(text.Value) => Quote(text.Value),
                _ => null,
            })
            .OfType<string>()
            .ToList();
        if (children.Count > 0)
            builder.Append(",\"c\":[").Append(string.Join(",", children)).Append(']');
        return builder.Append('}').ToString();
    }

    private static string Quote(string? value)
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

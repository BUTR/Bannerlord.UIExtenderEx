using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bannerlord.UIExtenderEx.Analyzers.Tests;

/// <summary>
/// A GUI package in the layout of <c>Bannerlord.ReferenceAssemblies.GUI</c>: manifest.json, movies.json, types.json and
/// prefab XML under <c>gui/&lt;Module&gt;/GUI/Prefabs</c>. Several can be passed together: a base package and DLC packages.
/// </summary>
internal sealed class TestGame
{
    private readonly string _package;
    private readonly bool _dlc;
    private readonly string[] _modules;
    private readonly List<string> _movies = [];
    private readonly List<string> _viewModels = [];
    private readonly List<(string Module, string Name, string Xml)> _prefabs = [];

    public TestGame(string package, bool dlc, params string[] modules)
    {
        _package = package;
        _dlc = dlc;
        _modules = modules;
    }

    public static TestGame Base() => new("Bannerlord.ReferenceAssemblies.GUI", false, "Native", "SandBox");

    public static TestGame NavalDlc() => new("Bannerlord.ReferenceAssemblies.GUI.NavalDLC", true, "NavalDLC");

    public TestGame Movie(string movie, string? viewModel, string? gameStateScreen = null, bool paired = true)
    {
        _movies.Add($$"""{"movie":{{Quote(movie)}},"viewModel":{{Quote(viewModel)}},"module":{{Quote(_modules[0])}},"overrideView":null,"gameStateScreen":{{Quote(gameStateScreen)}},"paired":{{(paired ? "true" : "false")}}}""");
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
        yield return ($"{root}/manifest.json", $$"""{"formatVersion":1,"package":{{Quote(_package)}},"gameVersion":"v1.4.8","changeSet":119303,"buildId":1,"modules":[{{modules}}]}""", _package);
        yield return ($"{root}/movies.json", $$"""{"formatVersion":1,"calls":[{{string.Join(",", _movies)}}],"unresolved":[]}""", _package);
        yield return ($"{root}/types.json", $$"""{"formatVersion":1,"widgets":[],"viewModels":[{{string.Join(",", _viewModels)}}],"enums":[]}""", _package);
        foreach (var (module, name, xml) in _prefabs)
            yield return ($"{root}/{module}/GUI/Prefabs/{name}.xml", xml, _package);
    }

    private static string Quote(string? value)
    {
        if (value is null)
            return "null";
        var builder = new StringBuilder("\"");
        foreach (var c in value)
            builder.Append(c switch { '"' => "\\\"", '\\' => "\\\\", _ => c.ToString() });
        return builder.Append('"').ToString();
    }
}

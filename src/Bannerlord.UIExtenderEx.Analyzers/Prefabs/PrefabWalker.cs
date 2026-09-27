using Microsoft.CodeAnalysis;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers.Prefabs;

/// <summary>
/// Walks prefab XML the way the game's loader reads it, and reports what the loader would drop, fail on, or bind to
/// nothing.
/// <list type="bullet">
/// <item>Keys (<c>WidgetAttributeContext</c> and <c>PrefabDatabindingExtension</c>): <c>Id</c>, <c>DataSource</c>,
/// <c>Command.*</c>, <c>CommandParameter.*</c>, <c>Parameter.*</c>; anything else is a widget attribute.</item>
/// <item>Values: <c>@</c> binds, <c>{...}</c> is a binding path, <c>!</c> a constant, <c>*</c> a parameter of the prefab;
/// anything else is the literal text.</item>
/// <item>A widget attribute is a public instance property of the widget's class, followed through dotted paths
/// (<c>WidgetExtensions.GetObjectAndProperty</c>); <c>@Name</c> reads the last node of the path from the widget's own
/// ViewModel (<c>GauntletView.BindData</c>).</item>
/// <item>A widget with a <c>DataSource</c> binds its own attributes, and its children, in the new scope; an
/// <c>ItemTemplate</c> binds the elements of the list its widget is bound to.</item>
/// <item>A tag names a widget class by its name, or a prefab; a prefab's attributes land on its root widget, and its
/// parameters reach the attributes inside written <c>*Name</c>.</item>
/// </list>
/// Nothing is checked below a scope the build cannot tell, a tag it cannot resolve, or a value it cannot read.
/// </summary>
internal sealed class PrefabWalker
{
    private const int MaxDepth = 24;

    private readonly PrefabSources _sources;
    private readonly ScopeResolver _resolver;
    private readonly Dictionary<string, INamedTypeSymbol> _widgets;
    private readonly Action<Diagnostic> _report;
    private readonly HashSet<string> _reported = new(StringComparer.Ordinal);
    private readonly HashSet<string> _visited = new(StringComparer.Ordinal);

    public PrefabWalker(PrefabSources sources, ScopeResolver resolver, Hosts hosts, Action<Diagnostic> report)
    {
        _sources = sources;
        _resolver = resolver;
        _report = report;
        _widgets = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
        foreach (var type in hosts.AllTypes)
        {
            if (!_widgets.ContainsKey(type.Name) && Hosts.SelfAndBases(type).Any(t => t.ToDisplayString() == "TaleWorlds.GauntletUI.BaseTypes.Widget"))
                _widgets[type.Name] = type;
        }
    }

    /// <summary>Reports once per rule, place and message, however many ways the same XML is reached.</summary>
    public void Report(DiagnosticDescriptor descriptor, Location location, params object[] arguments) =>
        ReportWith(descriptor, location, null, arguments);

    /// <summary><see cref="Report"/>, with what the rule's code fix needs.</summary>
    public void ReportWith(DiagnosticDescriptor descriptor, Location location, ImmutableDictionary<string, string?>? properties, params object[] arguments)
    {
        var diagnostic = Diagnostic.Create(descriptor, location, properties, arguments);
        if (_reported.Add($"{descriptor.Id}|{location}|{diagnostic.GetMessage(CultureInfo.InvariantCulture)}"))
            _report(diagnostic);
    }

    /// <summary>What a fix replacing <paramref name="replace"/> with one of <paramref name="suggestions"/> needs.</summary>
    public static ImmutableDictionary<string, string?> Replacing(string replace, IEnumerable<string> suggestions, bool inSpan) => FixData.Of(
        (FixData.Suggestions, FixData.Join(suggestions)),
        (FixData.Replace, replace),
        (FixData.Where, inSpan ? FixData.InSpan : FixData.AfterSpan));

    /// <summary>Walks content whose root is the widget itself, or with <paramref name="removeRootNode"/> its root's children.</summary>
    public void WalkContent(PrefabXml xml, bool removeRootNode, Scope scope)
    {
        if (xml.Document?.Root is not { } root)
            return;
        var chain = ImmutableList.Create(scope);
        foreach (var widget in removeRootNode ? root.Elements() : [root])
            Walk(widget, xml, chain, Parameters.Empty, 0);
    }

    /// <summary>Walks a prefab file's root widget: the element under <c>&lt;Window&gt;</c>.</summary>
    public void WalkPrefab(PrefabXml prefab, Scope scope, Parameters parameters)
    {
        if (RootWidget(prefab) is { } root)
            Walk(root, prefab, ImmutableList.Create(scope), WithDefaults(prefab, parameters), 0);
    }

    /// <summary>The binding checks a set-attribute patch's values need: only <c>@Name</c> depends on the scope.</summary>
    public void CheckSetAttribute(string value, Location location, Scope scope)
    {
        // The location is the literal that is the value, not an attribute's name
        if (value.StartsWith("@", StringComparison.Ordinal))
            CheckBinding(scope, LastNode(value.Substring(1)), location, inSpan: true);
    }

    /// <summary>
    /// An attribute a set-attribute patch puts on a node of the game's XML, checked like one written on that node's tag.
    /// Only a tag that names a widget class is checked; a prefab used by tag is not followed to its root widget.
    /// </summary>
    public void CheckTagAttribute(string tag, string key, string value, Location location)
    {
        if (key is "Id" or "DataSource" || key.StartsWith("Command.", StringComparison.Ordinal)
            || key.StartsWith("CommandParameter.", StringComparison.Ordinal) || key.StartsWith("Parameter.", StringComparison.Ordinal))
            return;
        if (!_sources.PrefabsByTag.ContainsKey(tag) && _widgets.TryGetValue(tag, out var widget))
            CheckWidgetAttribute(widget, key, value, literal: !value.StartsWith("*", StringComparison.Ordinal), location);
    }

    /// <summary>The root widget: the first element under <c>&lt;Window&gt;</c>, which is the file's root or sits under <c>&lt;Prefab&gt;</c>.</summary>
    private static XElement? RootWidget(PrefabXml prefab)
    {
        var root = prefab.Document?.Root;
        var window = root?.Name.LocalName == "Window" ? root : root?.Element("Window");
        return window?.Elements().FirstOrDefault();
    }

    /// <summary>
    /// The prefab's declared parameters, as <c>WidgetPrefab.LoadParameters</c> reads them: every child of
    /// <c>&lt;Parameters&gt;</c>, whatever its tag, and only under <c>&lt;Prefab&gt;</c>. The game's own prefabs spell the child
    /// <c>Paramter</c> and <c>Parameters</c> in places, and those parameters work.
    /// </summary>
    private static IEnumerable<XElement> DeclaredParameters(PrefabXml prefab) =>
        prefab.Document?.Root is { Name.LocalName: "Prefab" } root ? root.Elements("Parameters").Elements() : [];

    private void Walk(XElement element, PrefabXml xml, ImmutableList<Scope> chain, Parameters parameters, int depth)
    {
        var tag = element.Name.LocalName;
        _sources.PrefabsByTag.TryGetValue(tag, out var prefab);
        var widget = prefab is not null ? RootWidgetType(prefab, depth) : _widgets.TryGetValue(tag, out var type) ? type : null;

        var scopeHere = chain;
        if (element.Attribute("DataSource") is { } dataSource)
        {
            var (value, location) = parameters.Substitute(dataSource.Value, xml.Locate(dataSource));
            scopeHere = value is not null && value.Length > 2 && value[0] == '{' && value[value.Length - 1] == '}'
                ? Resolve(chain, value.Substring(1, value.Length - 2), location)
                : chain.Add(UnknownScope.Instance);
        }

        var passed = new Dictionary<string, (string Value, Location Location)>(StringComparer.Ordinal);
        foreach (var attribute in element.Attributes())
        {
            var key = attribute.Name.LocalName;
            if (attribute.IsNamespaceDeclaration || key is "Id" or "DataSource" || key.StartsWith("CommandParameter.", StringComparison.Ordinal))
                continue;

            var attributeLocation = xml.Locate(attribute);
            var (value, valueLocation) = parameters.Substitute(attribute.Value, attributeLocation);
            var literal = !attribute.Value.StartsWith("*", StringComparison.Ordinal);

            if (key.StartsWith("Command.", StringComparison.Ordinal))
            {
                if (value is not null && value.IndexOf('\\') < 0)
                    CheckCommand(scopeHere[scopeHere.Count - 1], value, valueLocation);
                continue;
            }
            if (key.StartsWith("Parameter.", StringComparison.Ordinal))
            {
                var name = key.Substring("Parameter.".Length);
                if (prefab is not null && UsedParameters(prefab) is var used && !used.Contains(name))
                    ReportWith(Descriptors.UnknownPrefabParameter, attributeLocation, Replacing(name, Suggestions.Closest(name, used), inSpan: true), name, tag);
                if (value is not null)
                    passed[name] = (value, valueLocation);
                continue;
            }

            if (widget is not null)
                CheckWidgetAttribute(widget, key, attribute.Value, literal, attributeLocation);
            if (value is not null && value.StartsWith("@", StringComparison.Ordinal))
                CheckBinding(scopeHere[scopeHere.Count - 1], LastNode(value.Substring(1)), valueLocation);
        }

        foreach (var child in element.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "Children":
                    foreach (var grandChild in child.Elements())
                        Walk(grandChild, xml, scopeHere, parameters, depth);
                    break;
                case "ItemTemplate":
                    foreach (var template in child.Elements())
                        Walk(template, xml, ItemScope(scopeHere), parameters, depth);
                    break;
                case "ItemTemplates":
                    foreach (var template in child.Elements().SelectMany(x => x.Elements()))
                        Walk(template, xml, ItemScope(scopeHere), parameters, depth);
                    break;
            }
        }

        if (prefab is null || depth >= MaxDepth)
            return;
        var calleeParameters = WithDefaults(prefab, new Parameters(passed));
        var visit = $"{tag}|{string.Join("/", scopeHere.Select(s => s.Key))}|{calleeParameters.Key}";
        if (!_visited.Add(visit) || RootWidget(prefab) is not { } root)
            return;
        Walk(root, prefab, scopeHere, calleeParameters, depth + 1);
    }

    private INamedTypeSymbol? RootWidgetType(PrefabXml prefab, int depth)
    {
        if (depth >= MaxDepth || RootWidget(prefab) is not { } root)
            return null;
        if (_sources.PrefabsByTag.TryGetValue(root.Name.LocalName, out var inner))
            return ReferenceEquals(inner, prefab) ? null : RootWidgetType(inner, depth + 1);
        return _widgets.TryGetValue(root.Name.LocalName, out var type) ? type : null;
    }

    /// <summary>The names a prefab can be handed: those it declares under <c>&lt;Parameters&gt;</c>, and those it reads as <c>*Name</c>.</summary>
    private static HashSet<string> UsedParameters(PrefabXml prefab)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (prefab.Document?.Root is not { } root)
            return names;
        foreach (var parameter in DeclaredParameters(prefab))
        {
            if (parameter.Attribute("Name")?.Value is { } name)
                names.Add(name);
        }
        foreach (var attribute in root.Descendants().Attributes())
        {
            if (attribute.Value.StartsWith("*", StringComparison.Ordinal))
                names.Add(attribute.Value.Substring(1));
        }
        return names;
    }

    private static Parameters WithDefaults(PrefabXml prefab, Parameters passed)
    {
        var values = passed.Values.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        foreach (var parameter in DeclaredParameters(prefab))
        {
            if (parameter.Attribute("Name")?.Value is { } name && !values.ContainsKey(name) && parameter.Attribute("DefaultValue") is { } value)
                values[name] = (value.Value, prefab.Locate(value));
        }
        return new Parameters(values);
    }

    /// <summary>
    /// A widget attribute: its first node a public instance property of the widget's class; for a single node with a
    /// literal value, that value one the loader can convert. Dotted paths are only checked at their first node: the rest
    /// is looked up on the runtime type of what the first returns, which may be more than its declared type.
    /// </summary>
    private void CheckWidgetAttribute(INamedTypeSymbol widget, string key, string rawValue, bool literal, Location location)
    {
        if (key.IndexOf(':') >= 0)
            return;
        var dot = key.IndexOf('.');
        var first = dot < 0 ? key : key.Substring(0, dot);
        var property = Hosts.SelfAndBases(widget)
            .SelectMany(t => t.GetMembers(first).OfType<IPropertySymbol>())
            .FirstOrDefault(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic && !p.IsIndexer);
        if (property is null)
        {
            var names = Hosts.SelfAndBases(widget)
                .SelectMany(t => t.GetMembers().OfType<IPropertySymbol>())
                .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic && !p.IsIndexer)
                .Select(p => p.Name);
            ReportWith(Descriptors.UnknownWidgetAttribute, location, Replacing(first, Suggestions.Closest(first, names), inSpan: true), key, widget.Name);
            return;
        }
        if (dot >= 0 || !literal || rawValue.Length == 0 || rawValue[0] is '@' or '!' or '{')
            return;
        if (WhyValueDoesNotFit(property.Type, rawValue) is var (why, wrong, suggestions))
            ReportWith(Descriptors.InvalidAttributeValue, location, Replacing(wrong, suggestions, inSpan: false), rawValue, key, why);
    }

    /// <summary>Why the loader cannot take the value, the part of it that is wrong, and what could be meant instead.</summary>
    private static (string Why, string Wrong, IEnumerable<string> Suggestions)? WhyValueDoesNotFit(ITypeSymbol type, string value)
    {
        switch (type)
        {
            case INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType:
            {
                var members = new HashSet<string>(enumType.GetMembers().OfType<IFieldSymbol>().Select(f => f.Name), StringComparer.Ordinal);
                foreach (var part in value.Split(','))
                {
                    var trimmed = part.Trim();
                    if (!members.Contains(trimmed) && !long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                        return ($"{enumType.Name} has no member '{trimmed}'", trimmed, Suggestions.Closest(trimmed, members));
                }
                return null;
            }
            case { SpecialType: SpecialType.System_Boolean }:
                if (value is "true" or "false")
                    return null;
                string[] meant = value.Equals("true", StringComparison.OrdinalIgnoreCase) ? ["true"]
                    : value.Equals("false", StringComparison.OrdinalIgnoreCase) ? ["false"]
                    : ["true", "false"];
                return ("only \"true\" reads as true; this reads as false", value, meant);
            case { SpecialType: SpecialType.System_Int32 }:
                return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ? null : ("it is not a whole number", value, []);
            case { SpecialType: SpecialType.System_Single }:
                return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _) ? null : ("it is not a number", value, []);
            default:
                return null;
        }
    }

    private void CheckBinding(Scope scope, string name, Location location, bool inSpan = false)
    {
        switch (scope)
        {
            case ProbeScope probe:
                probe.Names.Add((name, false, location, inSpan));
                break;
            case ViewModelScope viewModel when !_resolver.TryProperty(viewModel.Type, name, out _, out _):
                ReportNotFound(viewModel, name, command: false, location, inSpan);
                break;
        }
    }

    private void CheckCommand(Scope scope, string name, Location location)
    {
        switch (scope)
        {
            case ProbeScope probe:
                probe.Names.Add((name, true, location, false));
                break;
            case ViewModelScope viewModel when !_resolver.TryCommand(viewModel.Type, name, out _):
                ReportNotFound(viewModel, name, command: true, location, inSpan: false);
                break;
        }
    }

    private void ReportNotFound(ViewModelScope viewModel, string name, bool command, Location location, bool inSpan) =>
        ReportWith(Descriptors.BindingNotFound, location,
            Replacing(name, Suggestions.Closest(name, _resolver.Names(viewModel.Type, command)), inSpan),
            name, viewModel.Type.Name, command ? "method" : "property");

    /// <summary>
    /// A <c>DataSource</c> path from the current scope, node by node: <c>..</c> steps back out, a name steps into the
    /// property's value, an index into a list's element.
    /// </summary>
    private ImmutableList<Scope> Resolve(ImmutableList<Scope> chain, string path, Location location)
    {
        foreach (var node in path.Split('\\'))
        {
            if (node.Length == 0)
                continue;
            if (node == "..")
            {
                chain = chain.Count > 1 ? chain.RemoveAt(chain.Count - 1) : ImmutableList.Create<Scope>(UnknownScope.Instance);
                continue;
            }
            var current = chain[chain.Count - 1];
            switch (current)
            {
                case ProbeScope probe:
                    probe.Names.Add((node, false, location, false));
                    chain = chain.Add(UnknownScope.Instance);
                    break;
                case ViewModelScope viewModel:
                    if (_resolver.TryProperty(viewModel.Type, node, out var type, out _))
                    {
                        chain = chain.Add(_resolver.ChildScope(type));
                    }
                    else
                    {
                        ReportNotFound(viewModel, node, command: false, location, inSpan: false);
                        chain = chain.Add(UnknownScope.Instance);
                    }
                    break;
                case ListScope list when int.TryParse(node, out _):
                    chain = chain.Add(_resolver.ChildScope(list.Element));
                    break;
                default:
                    chain = chain.Add(UnknownScope.Instance);
                    break;
            }
        }
        return chain;
    }

    private ImmutableList<Scope> ItemScope(ImmutableList<Scope> chain) =>
        chain.Add(chain[chain.Count - 1] is ListScope list ? _resolver.ChildScope(list.Element) : UnknownScope.Instance);

    private static string LastNode(string path)
    {
        var cut = path.LastIndexOf('\\');
        return cut >= 0 ? path.Substring(cut + 1) : path;
    }
}

/// <summary>What a prefab was handed: each parameter's text, and where that text was written.</summary>
internal sealed class Parameters
{
    public static readonly Parameters Empty = new(new Dictionary<string, (string, Location)>());

    public IReadOnlyDictionary<string, (string Value, Location Location)> Values { get; }

    public Parameters(IReadOnlyDictionary<string, (string Value, Location Location)> values) => Values = values;

    public string Key => string.Join(";", Values.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key + "=" + x.Value.Value));

    /// <summary>
    /// A value as the loader sees it: <c>*Name</c> replaced by what the prefab was handed, with the place that text was
    /// written. Null when it was handed nothing, which the walk cannot read further.
    /// </summary>
    public (string? Value, Location Location) Substitute(string value, Location location)
    {
        if (!value.StartsWith("*", StringComparison.Ordinal))
            return (value, location);
        return Values.TryGetValue(value.Substring(1), out var passed) ? (passed.Value, passed.Location) : (null, location);
    }
}

using Bannerlord.UIExtenderEx.Analyzers.Game;

using Microsoft.CodeAnalysis;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers.Prefabs;

/// <summary>
/// Recursively analyzes prefab XML hierarchies using Gauntlet's runtime evaluation semantics, detecting invalid
/// widget attributes, broken data bindings, unresolved commands, and parameter forwarding errors.
/// <list type="bullet">
/// <item><b>Reserved Keys:</b> <c>Id</c>, <c>DataSource</c>, <c>Command.*</c>, <c>CommandParameter.*</c>, <c>Parameter.*</c>; all other keys represent widget properties.</item>
/// <item><b>Expression Prefixes:</b> <c>@</c> binds to ViewModel properties, <c>{...}</c> defines DataSource navigation paths, <c>!</c> references constants, and <c>*</c> references prefab parameters.</item>
/// <item><b>Widget Attributes:</b> Evaluated against public instance properties on the target Widget class (<c>WidgetExtensions.GetObjectAndProperty</c>).</item>
/// <item><b>DataSource Scoping:</b> Widgets declare new data contexts via <c>DataSource</c>; <c>ItemTemplate</c> and <c>ItemTemplates</c> bind against collection element types.</item>
/// <item><b>Prefab Components:</b> Custom tags instantiate compiled widgets or nested prefabs, forwarding parameters declared via <c>&lt;Parameters&gt;</c>.</item>
/// </list>
/// Validation halts gracefully when encountering unresolved dynamic scopes, unknown tags, or unparseable expressions.
/// </summary>
internal sealed class PrefabWalker
{
    private const int MaxDepth = 24;

    private readonly PrefabSources _sources;
    private readonly ScopeResolver _resolver;
    private readonly Dictionary<string, INamedTypeSymbol> _widgets;
    private readonly GameSet _game;
    private readonly Hosts _hosts;
    private readonly SourceAnnouncements _ownAnnouncements;
    private readonly Action<Diagnostic> _report;
    private readonly HashSet<string> _reported = new(StringComparer.Ordinal);
    private readonly HashSet<string> _visited = new(StringComparer.Ordinal);

    /// <summary>
    /// The target game configurations against which XML is validated. If the XML is part of a patch, only configurations
    /// where the patch successfully applies are evaluated.
    /// </summary>
    private IReadOnlyList<GameConfiguration> _configurations;

    public PrefabWalker(PrefabSources sources, ScopeResolver resolver, Hosts hosts, GameSet game, Action<Diagnostic> report)
    {
        _sources = sources;
        _resolver = resolver;
        _game = game;
        _configurations = game.Configurations;
        _hosts = hosts;
        _ownAnnouncements = new SourceAnnouncements(hosts);
        _report = report;
        _widgets = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
        foreach (var type in hosts.AllTypes)
        {
            if (!_widgets.ContainsKey(type.Name) && Hosts.SelfAndBases(type).Any(t => t.ToDisplayString() == "TaleWorlds.GauntletUI.BaseTypes.Widget"))
                _widgets[type.Name] = type;
        }
    }

    /// <summary>Reports a diagnostic, deduplicating multiple visits to the same XML node across paths.</summary>
    public void Report(DiagnosticDescriptor descriptor, Location location, params object[] arguments) =>
        ReportWith(descriptor, location, null, arguments);

    /// <summary>Reports a diagnostic with custom properties for Roslyn code fix providers.</summary>
    public void ReportWith(DiagnosticDescriptor descriptor, Location location, ImmutableDictionary<string, string?>? properties, params object[] arguments)
    {
        var diagnostic = Diagnostic.Create(descriptor, location, properties, arguments);
        if (_reported.Add($"{descriptor.Id}|{location}|{diagnostic.GetMessage(CultureInfo.InvariantCulture)}"))
            _report(diagnostic);
    }

    /// <summary>Constructs code fix metadata replacing <paramref name="replace"/> with candidate <paramref name="suggestions"/>.</summary>
    public static ImmutableDictionary<string, string?> Replacing(string replace, IEnumerable<string> suggestions, bool inSpan) => FixData.Of(
        (FixData.Suggestions, FixData.Join(suggestions)),
        (FixData.Replace, replace),
        (FixData.Where, inSpan ? FixData.InSpan : FixData.AfterSpan));

    /// <summary>
    /// Traverses XML content starting from the root widget (or its child elements if <paramref name="removeRootNode"/> is true).
    /// </summary>
    public void WalkContent(PrefabXml xml, bool removeRootNode, Scope scope, IReadOnlyList<GameConfiguration>? configurations = null)
    {
        if (xml.Document?.Root is not { } root)
            return;
        var chain = ImmutableList.Create(scope);
        In(configurations, () =>
        {
            foreach (var widget in removeRootNode ? root.Elements() : [root])
                Walk(widget, xml, chain, Parameters.Empty, 0);
        });
    }

    private void In(IReadOnlyList<GameConfiguration>? configurations, Action walk)
    {
        var outer = _configurations;
        _configurations = configurations ?? _game.Configurations;
        try
        {
            walk();
        }
        finally
        {
            _configurations = outer;
        }
    }

    /// <summary>Traverses a standalone prefab file starting from the root widget under <c>&lt;Window&gt;</c>.</summary>
    public void WalkPrefab(PrefabXml prefab, Scope scope, Parameters parameters)
    {
        if (RootWidget(prefab) is { } root)
            Walk(root, prefab, ImmutableList.Create(scope), WithDefaults(prefab, parameters), 0);
    }

    /// <summary>Validates attributes modified by a set-attribute patch, verifying binding expressions against the active scope.</summary>
    public void CheckSetAttribute(string value, Location location, Scope scope, IReadOnlyList<GameConfiguration>? configurations = null)
    {
        // The location corresponds to the literal value string, not the attribute key name
        if (value.StartsWith("@", StringComparison.Ordinal))
            In(configurations, () => CheckBinding(scope, LastNode(value.Substring(1)), location, inSpan: true));
    }

    /// <summary>
    /// Validates an attribute targeted by a set-attribute patch against vanilla widget definitions in <c>types.json</c>
    /// across all targeted game configurations (UIX0012, UIX0013, UIX0024).
    /// Falls back to local compilation types when package definitions are absent.
    /// </summary>
    public void CheckTagAttribute(IReadOnlyList<PatchTarget> targets, string key, string value, Location location)
    {
        if (key is "Id" or "DataSource" || key.StartsWith("Command.", StringComparison.Ordinal)
            || key.StartsWith("CommandParameter.", StringComparison.Ordinal) || key.StartsWith("Parameter.", StringComparison.Ordinal))
            return;
        var widgetTargets = targets.Where(x => !_sources.PrefabsByTag.ContainsKey(x.Tag)).Select(x => (x.Configuration, x.Tag)).ToList();
        if (CheckInConfigurations(widgetTargets, key, value, location))
            return;

        // When no package records the widget type, validate against locally compiled types matching the tag
        foreach (var tag in widgetTargets.Select(x => x.Tag).Distinct(StringComparer.Ordinal))
        {
            if (_widgets.TryGetValue(tag, out var compiled))
                CheckWidgetAttribute(compiled, key, value, !value.StartsWith("*", StringComparison.Ordinal), location);
        }
    }

    /// <summary>
    /// Validates widget attributes across targeted game configurations against <c>types.json</c> (UIX0012, UIX0013, UIX0024).
    /// Returns <see langword="false"/> if no configuration records the widget, allowing compilation fallback.
    /// Suggestions prioritize the newest supported game version while ensuring compatibility across all targeted versions.
    /// </summary>
    private bool CheckInConfigurations(IReadOnlyList<(GameConfiguration Configuration, string Tag)> targets, string key, string value, Location location)
    {
        if (key.IndexOf(':') >= 0)
            return true;
        var dot = key.IndexOf('.');
        var first = dot < 0 ? key : key.Substring(0, dot);
        var literal = !value.StartsWith("*", StringComparison.Ordinal);
        var checksValue = dot < 0 && literal && value.Length > 0 && value[0] is not ('@' or '!' or '{');

        var widgets = new Dictionary<GameConfiguration, GameWidget>();
        var missing = new List<(GameConfiguration Configuration, List<string> Names)>();
        var propertyTypes = new Dictionary<GameConfiguration, string>();
        var misfit = new List<GameConfiguration>();
        foreach (var (configuration, tag) in targets)
        {
            if (configuration.Widget(tag) is not { } widget || WidgetProperty(configuration, widget, first) is not var (found, propertyType, names))
                continue;
            widgets[configuration] = widget;
            if (!found)
            {
                missing.Add((configuration, names));
                continue;
            }
            propertyTypes[configuration] = propertyType!;
            if (checksValue && WhyValueDoesNotFit(configuration, propertyType!, value) is not null)
                misfit.Add(configuration);
        }
        if (widgets.Count == 0)
            return false;

        // The newest version's reason: the one the mod meets from now on
        if (missing.Count > 0)
        {
            var (newest, names) = missing[missing.Count - 1];
            var candidates = missing.Count == widgets.Count ? names : names.Where(name => widgets.All(x => WidgetProperty(x.Key, x.Value, name) is { Found: true }));
            var finding = Diagnostic.Create(Descriptors.UnknownWidgetAttribute, location,
                Replacing(first, Suggestions.Closest(first, candidates.Distinct(StringComparer.Ordinal)), inSpan: true), key, widgets[newest].Name);
            if (_game.ForConfigurations(finding, widgets.Keys.ToList(), missing.Select(x => x.Configuration).ToList()) is { } unknown)
                Report(unknown);
        }
        if (misfit.Count > 0)
        {
            var newest = misfit[misfit.Count - 1];
            var (why, wrong, _) = WhyValueDoesNotFit(newest, propertyTypes[newest], value)!.Value;
            Func<string, bool>? takenEverywhere = misfit.Count == propertyTypes.Count
                ? null
                : meant => propertyTypes.All(x => WhyValueDoesNotFit(x.Key, x.Value, WithPart(value, wrong, meant)) is null);
            var suggestions = WhyValueDoesNotFit(newest, propertyTypes[newest], value, takenEverywhere)!.Value.Suggestions;
            var finding = Diagnostic.Create(Descriptors.InvalidAttributeValue, location, Replacing(wrong, suggestions, inSpan: false), value, key, why);
            if (_game.ForConfigurations(finding, propertyTypes.Keys.ToList(), misfit) is { } invalid)
                Report(invalid);
        }
        return true;
    }

    /// <summary>Replaces a token within a comma-separated attribute value string (e.g. flags enum values) with a suggested replacement.</summary>
    private static string WithPart(string value, string wrong, string replacement) =>
        string.Join(",", value.Split(',').Select(part => part.Trim() == wrong ? replacement : part));

    /// <summary>
    /// Resolves a widget's public property across its inheritance hierarchy using <c>types.json</c> and local compilation symbols.
    /// Returns whether the property was found, its type name, and candidate property names for code fix suggestions.
    /// </summary>
    private (bool Found, string? Type, List<string> Names)? WidgetProperty(GameConfiguration configuration, GameWidget widget, string name)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var current = widget; seen.Add(current.Type);)
        {
            if (current.Properties is not { } properties)
                return null;
            if (properties.TryGetValue(name, out var type))
                return (true, type, names);
            names.AddRange(properties.Keys);
            if (current.BaseType is not { } baseType)
                return (false, null, names);
            if (configuration.WidgetOfType(baseType) is { } next)
            {
                current = next;
                continue;
            }
            if (_hosts.TypeByMetadataName(baseType) is not { } compiled)
                return null;
            foreach (var property in Hosts.SelfAndBases(compiled).SelectMany(t => t.GetMembers().OfType<IPropertySymbol>())
                         .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic && !p.IsIndexer))
            {
                if (property.Name == name)
                    return (true, property.Type is INamedTypeSymbol named ? Hosts.MetadataName(named) : property.Type.ToDisplayString(), names);
                names.Add(property.Name);
            }
            return (false, null, names);
        }
        return (false, null, names);
    }

    /// <summary>Retrieves the root widget element located directly under <c>&lt;Window&gt;</c>.</summary>
    private static XElement? RootWidget(PrefabXml prefab)
    {
        var root = prefab.Document?.Root;
        var window = root?.Name.LocalName == "Window" ? root : root?.Element("Window");
        return window?.Elements().FirstOrDefault();
    }

    /// <summary>
    /// Enumerates parameter definitions declared under <c>&lt;Parameters&gt;</c> within a <c>&lt;Prefab&gt;</c> root element.
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
            if (value is not null && value.Length > 2 && value[0] == '{' && value[value.Length - 1] == '}')
            {
                CheckRefreshedWhenReplaced(chain, value, location);
                CheckIntoAListByIndex(chain, value, location);
            }
        }
        if (prefab is null && widget is null)
            CheckTagKnown(tag, xml.Locate(element));
        CheckPassedChildren(element, xml, tag);

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
                // Parameters referenced by children placed into a LogicalChildrenLocation are marked as used (UIX0027)
                if (prefab is not null && UsedParameters(prefab) is var used && !used.Contains(name) && !PassedChildrenRead(element, tag, name))
                    ReportWith(Descriptors.UnknownPrefabParameter, attributeLocation, Replacing(name, Suggestions.Closest(name, used), inSpan: true), name, tag);
                if (value is not null)
                    passed[name] = (value, valueLocation);
                continue;
            }

            if (widget is not null)
                CheckContentAttribute(prefab is null ? tag : RootWidgetTag(prefab, depth), widget, key, attribute.Value, literal, attributeLocation);
            if (value is not null && value.StartsWith("@", StringComparison.Ordinal))
            {
                CheckBinding(scopeHere[scopeHere.Count - 1], LastNode(value.Substring(1)), valueLocation);
                if (widget is not null)
                    CheckBoundTypes(widget, key, scopeHere[scopeHere.Count - 1], LastNode(value.Substring(1)), valueLocation);
            }
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

    /// <summary>Resolves the tag name of a prefab's root widget, recursively traversing inner prefabs when defined.</summary>
    private string? RootWidgetTag(PrefabXml prefab, int depth)
    {
        if (depth >= MaxDepth || RootWidget(prefab) is not { } root)
            return null;
        if (_sources.PrefabsByTag.TryGetValue(root.Name.LocalName, out var inner))
            return ReferenceEquals(inner, prefab) ? null : RootWidgetTag(inner, depth + 1);
        return root.Name.LocalName;
    }

    /// <summary>
    /// Validates a widget attribute against vanilla package metadata across all active game configurations (reporting
    /// UIX0024 when version-specific), or against locally compiled types when definitions are absent.
    /// </summary>
    private void CheckContentAttribute(string? tag, INamedTypeSymbol widget, string key, string rawValue, bool literal, Location location)
    {
        var own = widget.Locations.Any(x => x.IsInSource);
        if (own || tag is null || !CheckInConfigurations(_configurations.Select(x => (x, tag)).ToList(), key, rawValue, location))
            CheckWidgetAttribute(widget, key, rawValue, literal, location);
    }

    /// <summary>Collects all parameter names declared under <c>&lt;Parameters&gt;</c> or referenced via <c>*Name</c> inside the prefab.</summary>
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
    /// Validates an attribute against a compiled widget type, checking property existence and literal type compatibility.
    /// Dotted paths evaluate their root property against the widget class; nested properties are evaluated dynamically at runtime.
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

    /// <summary>Validates literal attribute values against expected target types, returning error details and code fix suggestions if invalid.</summary>
    private static (string Why, string Wrong, IEnumerable<string> Suggestions)? WhyValueDoesNotFit(ITypeSymbol type, string value) =>
        type is INamedTypeSymbol named
            ? WhyValueDoesNotFit(Hosts.MetadataName(named), named.TypeKind == TypeKind.Enum ? named.GetMembers().OfType<IFieldSymbol>().Select(f => f.Name).ToList() : null, value)
            : null;

    /// <summary>
    /// Validates attribute literals against a metadata type name and optional enum member definitions.
    /// Filters suggestions using the <paramref name="keep"/> predicate when cross-version compatibility is required.
    /// </summary>
    private (string Why, string Wrong, IEnumerable<string> Suggestions)? WhyValueDoesNotFit(GameConfiguration configuration, string type, string value, Func<string, bool>? keep = null) =>
        WhyValueDoesNotFit(type, configuration.EnumMembers(type)
            ?? (_hosts.TypeByMetadataName(type) is { TypeKind: TypeKind.Enum } compiled ? compiled.GetMembers().OfType<IFieldSymbol>().Select(f => f.Name).ToList() : null), value, keep);

    private static (string Why, string Wrong, IEnumerable<string> Suggestions)? WhyValueDoesNotFit(string type, IReadOnlyCollection<string>? enumMembers, string value,
        Func<string, bool>? keep = null)
    {
        keep ??= _ => true;
        if (enumMembers is not null)
        {
            var members = new HashSet<string>(enumMembers, StringComparer.Ordinal);
            var name = type.Substring(Math.Max(type.LastIndexOf('.'), type.LastIndexOf('+')) + 1);
            foreach (var part in value.Split(','))
            {
                var trimmed = part.Trim();
                if (!members.Contains(trimmed) && !long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    return ($"{name} has no member '{trimmed}'", trimmed, Suggestions.Closest(trimmed, members.Where(keep)));
            }
            return null;
        }
        switch (type)
        {
            case "System.Boolean":
                if (value is "true" or "false")
                    return null;
                string[] meant = value.Equals("true", StringComparison.OrdinalIgnoreCase) ? ["true"]
                    : value.Equals("false", StringComparison.OrdinalIgnoreCase) ? ["false"]
                    : ["true", "false"];
                return ("only \"true\" reads as true; this reads as false", value, meant.Where(keep).ToList());
            case "System.Int32":
                return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ? null : ("it is not a whole number", value, []);
            case "System.Single":
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
            case ViewModelScope viewModel:
                CheckName(viewModel, name, command: false, _resolver.TryProperty(viewModel.Type, name, out _, out _), location, inSpan);
                break;
        }
    }

    /// <summary>
    /// Validates data binding type compatibility between widget properties and ViewModel properties (UIX0025, <see cref="BindingTypes"/>).
    /// <list type="bullet">
    /// <item><b>ViewModel to Widget:</b> Validates assignability and supported string conversions (<c>stringConversions</c>).</item>
    /// <item><b>Widget to ViewModel:</b> Validates announced change notification types against target property setters (<see cref="SourceAnnouncements"/>).</item>
    /// </list>
    /// If an incompatibility occurs only in a subset of targeted game versions, reports <see cref="Descriptors.HoldsForSomeVersions"/> (UIX0024).
    /// </summary>
    private void CheckBoundTypes(INamedTypeSymbol widget, string key, Scope scope, string name, Location location)
    {
        if (scope is not ViewModelScope viewModel || key.IndexOf('.') >= 0 || key.IndexOf(':') >= 0)
            return;
        var widgetProperty = Hosts.SelfAndBases(widget)
            .SelectMany(t => t.GetMembers(key).OfType<IPropertySymbol>())
            .FirstOrDefault(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic && !p.IsIndexer);
        if (widgetProperty is null || !_resolver.TryPropertySymbol(viewModel.Type, name, out var viewModelProperty, out _) || viewModelProperty is null)
            return;
        var viewModelType = viewModelProperty.Type;
        var widgetType = widgetProperty.Type;

        // In multi-target SDK builds, validate against the active build's version only.
        // Conditional compilation symbols (#if v103) may produce different property types per build.
        var configurations = _game.Configurations
            .Where(x => !_game.Versions.IsOneOfSeveralBuilds || _game.Versions.Current is not { } own || string.Equals(x.Version, own, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Gauntlet refreshes bindings when the movie opens before subscribing to property change events.
        // String bindings depend on registered runtime converters (or fallback types if metadata is absent).
        if (viewModelProperty.GetMethod is not null && BindingTypes.LoaderHandsWidget(viewModelType, widgetType) is var handed && handed != true)
        {
            var throws = Diagnostic.Create(Descriptors.BindingThrowsBetweenWidgetAndViewModel, location, key, viewModel.Type.Name, name,
                $"the loader hands the widget the {viewModelType.ToDisplayString()} as it is, which its {widgetType.ToDisplayString()} property cannot take, so the movie does not open",
                $"bind a {widgetType.ToDisplayString()} property");
            if (handed == false)
            {
                Report(throws);
                return;
            }
            var widgetTypeName = widgetType is INamedTypeSymbol namedWidgetType ? Hosts.MetadataName(namedWidgetType) : widgetType.ToDisplayString();
            if (configurations.Count == 0)
            {
                if (!BindingTypes.FallbackStringConversions.Contains(widgetTypeName))
                {
                    Report(throws);
                    return;
                }
            }
            else
            {
                var notConverted = configurations.Where(x => !(x.StringConversions ?? BindingTypes.FallbackStringConversions).Contains(widgetTypeName)).ToList();
                if (notConverted.Count > 0)
                {
                    if (_game.ForConfigurations(throws, configurations, notConverted) is { } notHanded)
                        Report(notHanded);
                    return;
                }
            }
        }
        // ViewModel.SetPropertyValue only assigns through public setters
        if (viewModelProperty.SetMethod is not { DeclaredAccessibility: Accessibility.Public })
            return;

        // For custom widget classes, inspect calls to OnPropertyChanged directly from source symbols
        var ownRejected = _ownAnnouncements.Announced(widget, key)
            .Where(x => !BindingTypes.InvokeTakes(x, viewModelType))
            .Select(x => x is INamedTypeSymbol named ? Hosts.MetadataName(named) : x.ToDisplayString())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        if (ownRejected.Count > 0)
        {
            Report(Throws(ownRejected));
            return;
        }

        configurations = configurations.Where(x => x.HasAnnouncements).ToList();
        if (configurations.Count == 0)
            return;
        var widgetTypes = Hosts.SelfAndBases(widget).Select(Hosts.MetadataName).ToList();
        var versions = new List<string>();
        var rejectedIn = new List<(string Version, List<string> Rejected)>();
        foreach (var group in configurations.GroupBy(x => x.Version, StringComparer.OrdinalIgnoreCase).OrderBy(x => x.Key, GameVersions.Comparer))
        {
            versions.Add(group.Key);
            // Fall back to a type unresolved by compilation with the benefit of the doubt
            var rejected = group
                .SelectMany(x => x.Announced(widgetTypes, key) ?? [])
                .Distinct(StringComparer.Ordinal)
                .Where(x => _hosts.TypeByMetadataName(x) is { } announced && !BindingTypes.InvokeTakes(announced, viewModelType))
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();
            if (rejected.Count > 0)
                rejectedIn.Add((group.Key, rejected));
        }
        var holdVersions = rejectedIn.Select(x => x.Version).ToList();
        if (rejectedIn.Count == 0 || !_game.Versions.Reports(holdVersions))
            return;

        // Formulate diagnostic details highlighting the newest affected game version
        var diagnostic = Throws(rejectedIn[rejectedIn.Count - 1].Rejected);
        if (rejectedIn.Count == versions.Count)
        {
            Report(diagnostic);
            return;
        }
        Report(Diagnostic.Create(Descriptors.HoldsForSomeVersions, location, FixData.Of((FixData.Rule, diagnostic.Id)),
            GamePatchChecker.Join(holdVersions, versions, v => v), diagnostic.GetMessage(CultureInfo.InvariantCulture)));

        Diagnostic Throws(IReadOnlyCollection<string> announced)
        {
            var why = $"the widget announces a change as {string.Join(" or ", announced)}, which a {viewModelType.ToDisplayString()} property cannot take, so every change after the movie opens throws out of whatever made it";
            var remedy = announced.Contains("System.String") && widgetType.TypeKind == TypeKind.Enum
                ? "bind an object property that keeps the enum and turns a name it is handed back into it with Enum.Parse"
                : $"bind a property of the type the widget announces, {string.Join(" or ", announced)}";
            return Diagnostic.Create(Descriptors.BindingThrowsBetweenWidgetAndViewModel, location, key, viewModel.Type.Name, name, why, remedy);
        }
    }

    /// <summary>Reports a diagnostic, deduplicating multiple visits to the same XML node across paths.</summary>
    public void Report(Diagnostic diagnostic)
    {
        if (_reported.Add($"{diagnostic.Id}|{diagnostic.Location}|{diagnostic.GetMessage(CultureInfo.InvariantCulture)}"))
            _report(diagnostic);
    }

    /// <summary>
    /// Validates that multi-step DataSource navigation paths ({..\Visual}, {Hint\Child}) are not used on replaced child ViewModels (UIX0026).
    /// In Gauntlet's XML loader, <c>GauntletView.OnPropertyChanged</c> only refreshes direct child views matching the property path;
    /// widgets bound through indirect scopes retain stale references to the previous ViewModel instance.
    /// </summary>
    private void CheckRefreshedWhenReplaced(ImmutableList<Scope> chain, string written, Location location)
    {
        var nodes = written.Substring(1, written.Length - 2).Split('\\').Where(x => x.Length > 0).ToArray();
        if (nodes.Length < 2)
            return;
        var last = nodes[nodes.Length - 1];
        if (last == ".." || int.TryParse(last, out _))
            return;
        var owner = Resolve(chain, string.Join("\\", nodes.Take(nodes.Length - 1)), location);
        if (owner[owner.Count - 1] is not ViewModelScope viewModel
            || !_resolver.TryPropertySymbol(viewModel.Type, last, out var property, out _) || property?.SetMethod is null)
            return;
        Report(Descriptors.DataSourceNotRefreshedWhenReplaced, location, written, last, viewModel.Type.Name);
    }

    /// <summary>
    /// Validates that indexed DataSource paths ({List\0}, {..\1}) are not bound directly in XML (UIX0028).
    /// <c>GauntletView.RefreshBinding</c> evaluates indexed paths only during initial construction or full view refresh.
    /// In-place mutations (<c>ListChanged</c>) are only received by the widget bound to the collection itself.
    /// </summary>
    private void CheckIntoAListByIndex(ImmutableList<Scope> chain, string written, Location location)
    {
        var nodes = written.Substring(1, written.Length - 2).Split('\\').Where(x => x.Length > 0).ToArray();
        for (var i = 1; i < nodes.Length; i++)
        {
            if (!char.IsDigit(nodes[i][0]))
                continue;
            var owner = Resolve(chain, string.Join("\\", nodes.Take(i)), Location.None);
            if (owner[owner.Count - 1] is ViewModelScope)
                continue;
            Report(Descriptors.DataSourceIntoAListByIndex, location, written, nodes[i - 1]);
            return;
        }
    }

    /// <summary>
    /// Validates that an XML element tag corresponds to a known compiled Widget class or registered prefab (UIX0029).
    /// When encountering unknown tags, Gauntlet instantiates a plain base <c>Widget</c> and logs an engine assertion.
    /// </summary>
    private void CheckTagKnown(string tag, Location location)
    {
        if (!_game.Configurations.Any(x => x.KnowsWidgets) || _game.Configurations.Any(x => x.HasWidget(tag) || x.Prefab(tag) is not null))
            return;
        Report(Descriptors.UnknownWidgetTag, location, tag);
    }

    /// <summary>
    /// Validates parameter usage in child widgets passed into a prefab containing a <c>&lt;LogicalChildrenLocation /&gt;</c> (UIX0027).
    /// Unless explicitly forwarded via <c>Parameter.Name="*Name"</c>, parameter expressions evaluate against the receiving prefab's context.
    /// </summary>
    private void CheckPassedChildren(XElement element, PrefabXml xml, string tag)
    {
        var passedChildren = element.Elements("Children").Elements().ToList();
        if (passedChildren.Count == 0 || !HasLogicalChildrenLocation(tag))
            return;
        foreach (var attribute in passedChildren.SelectMany(x => x.DescendantsAndSelf()).Where(x => x.Name.LocalName != "ItemTemplate").SelectMany(x => x.Attributes()))
        {
            if (!attribute.Value.StartsWith("*", StringComparison.Ordinal) || attribute.Value.Length < 2)
                continue;
            var name = attribute.Value.Substring(1);
            if (element.Attribute("Parameter." + name)?.Value == attribute.Value)
                continue;
            Report(Descriptors.ParameterInPassedChildren, xml.Locate(attribute), name, tag);
        }
    }

    /// <summary>Determines whether any child elements passed into a logical children location reference the parameter <c>*name</c>.</summary>
    private bool PassedChildrenRead(XElement element, string tag, string name) =>
        HasLogicalChildrenLocation(tag) && element.Elements("Children").Elements().SelectMany(x => x.DescendantsAndSelf()).SelectMany(x => x.Attributes())
            .Any(x => x.Value == "*" + name);

    private readonly Dictionary<string, bool> _logicalChildrenLocations = new(StringComparer.Ordinal);

    /// <summary>
    /// Determines whether the target prefab contains a <c>&lt;LogicalChildrenLocation /&gt;</c> tag, inspecting local prefabs
    /// or vanilla package documents.
    /// </summary>
    private bool HasLogicalChildrenLocation(string tag)
    {
        if (_logicalChildrenLocations.TryGetValue(tag, out var known))
            return known;
        bool found;
        if (_sources.PrefabsByTag.TryGetValue(tag, out var prefab))
            found = prefab.Document?.Root?.Descendants().Any(x => x.Name.LocalName == "LogicalChildrenLocation") == true;
        else
            found = _game.Configurations.Any(c => c.Prefab(tag)?.Document(default)?.GetElementsByTagName("LogicalChildrenLocation").Count > 0);
        return _logicalChildrenLocations[tag] = found;
    }

    private void CheckCommand(Scope scope, string name, Location location)
    {
        switch (scope)
        {
            case ProbeScope probe:
                probe.Names.Add((name, true, location, false));
                break;
            case ViewModelScope viewModel:
                CheckName(viewModel, name, command: true, _resolver.TryCommand(viewModel.Type, name, out _), location, inSpan: false);
                break;
        }
    }

    /// <summary>
    /// Validates ViewModel property and method bindings (UIX0015).
    /// Checks definitions across all targeted game configurations via <c>types.json</c> (<see cref="GameConfiguration.Answers"/>).
    /// If members are absent only in specific versions, reports <see cref="Descriptors.HoldsForSomeVersions"/> (UIX0024).
    /// </summary>
    private void CheckName(ViewModelScope viewModel, string name, bool command, bool found, Location location, bool inSpan)
    {
        var type = Hosts.MetadataName(viewModel.Type);
        var present = new List<GameConfiguration>();
        var missing = new List<GameConfiguration>();
        foreach (var configuration in _configurations)
        {
            if (configuration.Answers(type, name, command) is not { } answers)
                continue;
            present.Add(configuration);
            if (!answers)
                missing.Add(configuration);
        }
        if (present.Count == 0 ? found : missing.Count == 0 || _resolver.ModAnswers(viewModel.Type, name, command, IsGame))
            return;

        var names = _resolver.Names(viewModel.Type, command).Concat(missing.SelectMany(x => x.Names(type, command)));
        // Where some versions answer the name, suggestions only offer names present across all targeted versions
        if (missing.Count > 0 && missing.Count < present.Count)
            names = names.Where(x => present.All(c => c.Answers(type, x, command) == true) || _resolver.ModAnswers(viewModel.Type, x, command, IsGame));
        var finding = Diagnostic.Create(Descriptors.BindingNotFound, location, Replacing(name, Suggestions.Closest(name, names.Distinct(StringComparer.Ordinal)), inSpan),
            name, viewModel.Type.Name, command ? "method" : "property");
        if (present.Count == 0)
            Report(finding);
        else if (_game.ForConfigurations(finding, present, missing) is { } diagnostic)
            Report(diagnostic);

        bool IsGame(INamedTypeSymbol candidate) => _configurations.Any(x => x.ViewModel(Hosts.MetadataName(candidate)) is not null);
    }

    /// <summary>
    /// Resolves a <c>DataSource</c> path step-by-step: <c>..</c> navigates to the parent scope, property names navigate to child ViewModels,
    /// and indices navigate into collection element scopes.
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
                    var found = _resolver.TryProperty(viewModel.Type, node, out var type, out _);
                    CheckName(viewModel, node, command: false, found, location, inSpan: false);
                    // Child scopes are resolved using compilation types
                    chain = chain.Add(found ? _resolver.ChildScope(type) : UnknownScope.Instance);
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

/// <summary>Represents parameter assignments passed to a prefab instance, mapping parameter names to literal values and source locations.</summary>
internal sealed class Parameters
{
    public static readonly Parameters Empty = new(new Dictionary<string, (string, Location)>());

    public IReadOnlyDictionary<string, (string Value, Location Location)> Values { get; }

    public Parameters(IReadOnlyDictionary<string, (string Value, Location Location)> values) => Values = values;

    public string Key => string.Join(";", Values.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key + "=" + x.Value.Value));

    /// <summary>
    /// Evaluates a parameter expression (<c>*Name</c>), returning the substituted parameter value and its declaration location.
    /// Returns null if the parameter was not provided by the caller.
    /// </summary>
    public (string? Value, Location Location) Substitute(string value, Location location)
    {
        if (!value.StartsWith("*", StringComparison.Ordinal))
            return (value, location);
        return Values.TryGetValue(value.Substring(1), out var passed) ? (passed.Value, passed.Location) : (null, location);
    }
}
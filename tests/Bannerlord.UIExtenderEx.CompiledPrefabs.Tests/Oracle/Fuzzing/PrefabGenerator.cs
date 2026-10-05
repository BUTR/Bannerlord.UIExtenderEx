using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs.Oracle.Fuzzing;

/// <summary>
/// Generates deterministic synthetic prefabs from a random seed using game-independent widget classes.
/// Constructs randomized combinations of attribute values (literals, parameters, constants, and data bindings),
/// nested prefab instances, logical children locations, visual definitions, and data source hierarchies
/// to evaluate XML and compiled loader behavior under diverse scenarios.
/// </summary>
internal sealed class PrefabGenerator
{
    /// <summary>
    /// Identifies the parent context providing the initial scope for a widget: root movie, enclosing prefab instance, or item template.
    /// </summary>
    private enum Host
    {
        None,
        Part,
        Item,
    }

    /// <summary>
    /// Defines widget types instantiable in test environments without requiring game assets.
    /// </summary>
    private static readonly Type[] WidgetTypes =
    [
        typeof(Widget), typeof(ListPanel), typeof(ButtonWidget), typeof(BrushWidget),
        typeof(LabelWidget), typeof(FragileSetterWidget), typeof(CustomElementWidget), typeof(LooseHolderWidget), typeof(ReferenceWidget),
    ];

    /// <summary>
    /// Defines the supported property types mapped to candidate fuzzing value pools.
    /// </summary>
    private static readonly Type[] AttributeTypes = [typeof(int), typeof(float), typeof(bool), typeof(string), typeof(Color), typeof(Widget)];

    private static readonly Dictionary<Type, string[]> Values = new()
    {
        [typeof(int)] = ["0", "5", "-3", "+7", " 7", "5.0", "abc", "", "2147483648", "0x10"],
        [typeof(float)] = ["0", "1.5", "-2", "5.", "1,000", "1e3", ".5", "NaN", "abc", "", "0.1", "3.4028235E+39"],
        [typeof(bool)] = ["true", "false", "True", "TRUE", "1", ""],
        [typeof(string)] = ["text", "", "say \"hi\"", "C:\\temp\\", "line\nbreak", "{=abc}Localized"],
        [typeof(Color)] = ["#FF0000FF", "#00FF00", "#fff", "red", "", "#GGGGGGFF", "#11223344"],
        [typeof(Widget)] = ["A", "B", "..\\A", "A\\B", "..\\..\\C", "Nope", "", ".", ".."],
    };

    private static readonly string[] EnumValues = ["", "1", "Nope", "0", "-1"];
    private static readonly string[] Ids = ["A", "B", "C"];
    private static readonly string[] BindingNames = ["Title", "Count", "Ratio", "IsOn", "Tint", "Label"];
    private static readonly string[] ScopeNames = ["Child", "Other"];
    private static readonly string[] ListNames = ["Items", "Rows"];
    private static readonly string[] CommandNames = ["ExecuteA", "ExecuteB"];
    private static readonly string[] VisualStateAttributes = ["PositionYOffset", "MarginLeft", "SuggestedWidth", "NoSuchAttribute", "GotMarginTop", "TransitionDuration"];

    private readonly Random _random;
    private readonly int _seed;
    private readonly bool _bindings;
    private List<FuzzPrefab> _parts = [];

    /// <summary>
    /// Tracks the nesting depth of children passed into a component's logical children location.
    /// </summary>
    private int _passed;

    private bool Passed => _passed > 0;

    public PrefabGenerator(int seed, bool bindings)
    {
        _seed = seed;
        _random = new Random(seed);
        _bindings = bindings;
    }

    /// <summary>
    /// Indexes settable public properties of supported types or public enumerations for each widget type.
    /// </summary>
    private static readonly Dictionary<Type, PropertyInfo[]> Properties = WidgetTypes.ToDictionary(x => x, x => x
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        // Excludes ParentWidget to prevent cyclic ancestor graphs and unrecoverable infinite recursion during ConnectedToRoot traversal.
        .Where(p => p.CanWrite && p.GetSetMethod() is not null && p.GetIndexParameters().Length == 0 && p.Name is not ("Id" or "ParentWidget"))
        .Where(p => AttributeTypes.Contains(p.PropertyType) || (p.PropertyType.IsEnum && p.PropertyType.IsPublic))
        .GroupBy(p => p.Name).Select(g => g.First())
        .OrderBy(p => p.Name, StringComparer.Ordinal)
        .ToArray());

    public FuzzCase Next(string name)
    {
        _parts = [];
        var partCount = _random.Next(3);
        for (var i = 0; i < partCount; i++)
        {
            var part = NewPrefab($"{name}Part{i}", isPart: true, depth: 1);
            _parts.Add(part);
        }
        var movie = NewPrefab(name, isPart: false, depth: 0);
        return new FuzzCase(_seed, movie, _parts);
    }

    private FuzzPrefab NewPrefab(string name, bool isPart, int depth)
    {
        var prefab = new FuzzPrefab(name, new FuzzWidget("Widget"));
        if (isPart)
        {
            for (var i = _random.Next(3); i > 0; i--)
                prefab.Parameters.Add(new($"P{i}", Pick(Pick(Values.Values.ToArray()))));
        }
        for (var i = _random.Next(4); i > 0; i--)
            prefab.Constants.Add(NewConstant(i));
        for (var i = _random.Next(3); i > 0; i--)
            prefab.VisualDefinitions.Add(NewVisualDefinition(i));
        if (_random.Next(4) == 0)
            prefab.CustomElements.Add(new("Payload", "<Data Kind=\"a\"><Item Index=\"1\" /></Data>"));

        prefab.Root = NewWidget(prefab, depth, isRoot: true, scopeDepth: 0, host: isPart ? Host.Part : Host.None);
        if (isPart && _random.Next(2) == 0)
        {
            // Designates a random child widget as the logical children location for instantiating hosts.
            var widgets = prefab.Root.Descendants().ToList();
            Pick(widgets).LogicalChildrenLocation = true;
        }
        return prefab;
    }

    /// <summary>
    /// Generates a reference from constant <paramref name="index"/> to a preceding constant or a missing identifier,
    /// avoiding cyclic self-references that induce unrecoverable stack overflows.
    /// </summary>
    private string ConstantReference(int index) =>
        index > 1 && _random.Next(5) > 0 ? $"!C{_random.Next(1, index)}" : "!Missing";

    private List<KeyValuePair<string, string>> NewConstant(int index)
    {
        var constant = new List<KeyValuePair<string, string>> { new("Name", $"C{index}") };
        switch (_random.Next(5))
        {
            case 0:
                constant.Add(new("Value", Pick(["5", "1.5", "", "abc", "#FF0000FF", "true"])));
                break;
            case 1:
                constant.Add(new("Value", ConstantReference(index)));
                break;
            case 2:
                constant.Add(new("Value", Pick(["5", "2.5"])));
                constant.Add(new("Additive", Pick(["3", "-1", ConstantReference(index)])));
                break;
            case 3:
                constant.Add(new("Value", "4"));
                constant.Add(new("MultiplyResult", Pick(["2", "0.5"])));
                break;
            default:
                constant.Add(new("BooleanCheck", Pick(["true", "false", "*P1"])));
                constant.Add(new("OnTrue", Pick(["1", ConstantReference(index)])));
                constant.Add(new("OnFalse", Pick(["0", ""])));
                break;
        }
        return constant;
    }

    /// <summary>
    /// Selects an invalid value from <paramref name="bad"/> at low frequency (10%) and a valid value from <paramref name="good"/> otherwise.
    /// </summary>
    private string Rarely(string[] bad, string[] good) => _random.Next(10) == 0 ? Pick(bad) : Pick(good);

    private (string, List<KeyValuePair<string, string>>, List<(string, List<KeyValuePair<string, string>>)>) NewVisualDefinition(int index)
    {
        var name = Pick([$"Tab.V{index}", $"Tab_V{index}", $"V{index}"]);
        var attributes = new List<KeyValuePair<string, string>>();
        if (_random.Next(2) == 0)
            attributes.Add(new("TransitionDuration", Rarely(["abc"], ["0.25", "0"])));
        var states = new List<(string, List<KeyValuePair<string, string>>)>();
        foreach (var state in new[] { "Default", "Pressed" }.Take(1 + _random.Next(2)))
        {
            var stateAttributes = new List<KeyValuePair<string, string>>();
            foreach (var attribute in VisualStateAttributes.Where(_ => _random.Next(3) == 0))
                stateAttributes.Add(new(attribute, Rarely(["", "abc"], ["1", "2.5", "!C1", "*P1"])));
            states.Add((state, stateAttributes));
        }
        return (name, attributes, states);
    }

    /// <param name="host">
    /// The host context providing the initial data source scope: movie root, component instance, or list item template.
    /// </param>
    private FuzzWidget NewWidget(FuzzPrefab prefab, int depth, bool isRoot, int scopeDepth, Host host)
    {
        // Selects among prefabs generated prior to this one, preventing cyclic component instantiation.
        var usableParts = _parts.Where(x => x.Name != prefab.Name).ToList();
        var usesPart = usableParts.Count > 0 && !isRoot && _random.Next(4) == 0;
        var type = usesPart ? null : Pick(WidgetTypes);
        var widget = new FuzzWidget(usesPart ? Pick(usableParts).Name : type!.Name);

        if (_random.Next(3) == 0)
            widget.Attributes.Add(new("Id", Pick<string>(Passed ? [.. Ids, "@Title"] : [.. Ids, "*P1", "@Title"])));

        if (type is not null)
        {
            var properties = Properties[type];
            for (var i = _random.Next(5); i > 0 && properties.Length > 0; i--)
            {
                var property = Pick(properties);
                if (widget.Attributes.Any(x => x.Key == property.Name))
                    continue;
                widget.Attributes.Add(new(property.Name, NewValue(prefab, property.PropertyType)));
            }
            if (_random.Next(6) == 0 && prefab.VisualDefinitions.Count >= 0)
                widget.Attributes.Add(new("VisualDefinition", prefab.VisualDefinitions.Count > 0 && _random.Next(3) > 0 ? Pick(prefab.VisualDefinitions).Name : "Missing"));
            if (type == typeof(LooseHolderWidget) && _random.Next(2) == 0)
                widget.Attributes.Add(new("Holder.Value", NewValue(prefab, typeof(float))));
            if (type == typeof(CustomElementWidget) && _random.Next(2) == 0)
                widget.Attributes.Add(new("Custom", Pick(["Payload", "Missing"])));
        }
        else
        {
            // Configures parameters passed to the component instance via literal values, property bindings, or forwarded parameters.
            var part = _parts.First(x => x.Name == widget.Type);
            foreach (var (name, _) in part.Parameters.Where(_ => _random.Next(3) > 0))
            {
                var value = _random.Next(4) switch
                {
                    0 when _bindings => "@" + Pick(BindingNames),
                    1 when prefab.Parameters.Count > 0 && !Passed => "*" + Pick(prefab.Parameters).Key,
                    _ => Pick(Pick(Values.Values.ToArray())),
                };
                widget.Attributes.Add(new($"Parameter.{name}", value));
            }
        }

        if (_bindings)
            AddBindings(widget, prefab, isRoot, usesPart, host, ref scopeDepth);

        if (depth < 3)
        {
            // Suppresses parameter references under passed children to avoid generating constructs rejected by UIX0027.
            var passes = usesPart && _parts.First(x => x.Name == widget.Type) is { } used && used.Root.Descendants().Any(x => x != used.Root && x.LogicalChildrenLocation);
            if (passes)
                _passed++;
            for (var i = _random.Next(depth == 0 ? 4 : 3); i > 0; i--)
                widget.Children.Add(NewWidget(prefab, depth + 1, isRoot: false, scopeDepth, host));
            if (passes)
                _passed--;
        }
        return widget;
    }

    private void AddBindings(FuzzWidget widget, FuzzPrefab prefab, bool isRoot, bool usesPart, Host host, ref int scopeDepth)
    {
        // Configures a list panel with a list data source and child item template.
        if (widget.Type == nameof(ListPanel) && _random.Next(3) == 0)
        {
            widget.Attributes.Add(new("DataSource", "{" + Pick(ListNames) + "}"));
            widget.ItemTemplate = NewWidget(prefab, depth: 2, isRoot: false, scopeDepth: 0, host: Host.Item);
            return;
        }
        switch (_random.Next(9))
        {
            case 0 when scopeDepth < 2:
                widget.Attributes.Add(new("DataSource", "{" + Pick(ScopeNames) + "}"));
                scopeDepth++;
                break;
            case 1 when scopeDepth > 0:
                widget.Attributes.Add(new("DataSource", "{..\\" + Pick(ScopeNames) + "}"));
                break;
            case 2 when prefab.Parameters.Count > 0 && !Passed:
                widget.Attributes.Add(new("DataSource", "*" + Pick(prefab.Parameters).Key));
                break;
            case 3 when isRoot:
                // Applies root data source, which is ignored at movie root and overridden by host instances.
                widget.Attributes.Add(new("DataSource", "{" + Pick(ScopeNames) + "}"));
                break;
            case 4 when host == Host.Item && scopeDepth == 0 && !isRoot:
                // Navigates above item scope to target the parent list, grandparent container, or sibling item index.
                widget.Attributes.Add(new("DataSource", Pick([@"{..\..}", @"{..\..\Other}", @"{..\0}"])));
                break;
            case 4 when host == Host.Part && scopeDepth == 0 && !isRoot:
                // Navigates above component root into scopes supplied by the host widget.
                widget.Attributes.Add(new("DataSource", Pick(["{..}", @"{..\Child}", @"{..\..}", @"{..\..\Other}"])));
                break;
            case 5 when usesPart:
                // Configures the instance's own data source relative to the surrounding scope.
                widget.Attributes.Add(new("DataSource", Pick(["{..}", @"{..\Child}", @"{..\..}"])));
                if (_random.Next(2) == 0)
                    widget.Attributes.Add(new("IsVisible", "@IsOn"));
                break;
            case 6 when scopeDepth < 2:
                // Indexes directly into an element of a list collection.
                widget.Attributes.Add(new("DataSource", "{" + Pick(ListNames) + "\\" + Pick(["0", "1"]) + "}"));
                scopeDepth++;
                break;
        }
        foreach (var attribute in widget.Attributes.ToList())
        {
            if (attribute.Key.Contains('.') || attribute.Key is "Id" or "DataSource" or "VisualDefinition" || _random.Next(3) > 0)
                continue;
            if (BindingNameFor(widget.Type, attribute.Key) is not { } bindingName)
                continue;
            widget.Attributes.Remove(attribute);
            widget.Attributes.Add(new(attribute.Key, "@" + bindingName));
        }
        if (_random.Next(3) == 0)
        {
            widget.Attributes.Add(new("Command.Click", Pick([.. CommandNames, "Child\\ExecuteA", "..\\ExecuteB"])));
            if (_random.Next(2) == 0)
                widget.Attributes.Add(new("CommandParameter.Click", Pick<string>(Passed ? ["7", "text", ""] : ["7", "text", "", "*P1"])));
        }
    }

    /// <summary>
    /// Maps a widget attribute to an appropriate view model property name matching the attribute data type,
    /// or returns <see langword="null"/> if the type cannot be bound to a view model property.
    /// </summary>
    private static string? BindingNameFor(string widgetType, string attribute)
    {
        if (WidgetTypes.FirstOrDefault(x => x.Name == widgetType) is not { } type
            || Properties[type].FirstOrDefault(x => x.Name == attribute)?.PropertyType is not { } propertyType)
            return null;
        if (propertyType.IsEnum)
            return propertyType.Name + "Value";
        return propertyType == typeof(bool) ? "IsOn"
            : propertyType == typeof(int) ? "Count"
            : propertyType == typeof(float) ? "Ratio"
            : propertyType == typeof(string) ? "Title"
            : propertyType == typeof(Color) ? "Tint"
            : null;
    }

    private string NewValue(FuzzPrefab prefab, Type type)
    {
        switch (_random.Next(10))
        {
            case 0:
                return "*" + (!Passed && prefab.Parameters.Count > 0 && _random.Next(3) > 0 ? Pick(prefab.Parameters).Key : "Undeclared");
            case 1:
                return "!" + (prefab.Constants.Count > 0 && _random.Next(4) > 0 ? prefab.Constants[_random.Next(prefab.Constants.Count)][0].Value : "Missing");
        }
        if (type.IsEnum)
        {
            var names = Enum.GetNames(type);
            return _random.Next(3) > 0 && names.Length > 0 ? Pick(names) : Pick([.. EnumValues, names.Length > 1 ? $"{names[0]}, {names[1]}" : "", names.FirstOrDefault()?.ToLowerInvariant() ?? ""]);
        }
        return Pick(Values[type]);
    }

    private T Pick<T>(IReadOnlyList<T> items) => items[_random.Next(items.Count)];
}

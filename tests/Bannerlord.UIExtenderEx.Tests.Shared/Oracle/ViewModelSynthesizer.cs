using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace Bannerlord.UIExtenderEx.Tests.Oracle;

/// <summary>
/// Synthesizes concrete <see cref="ViewModel"/> classes shaped after the databinding requirements of a prefab.
/// <para>
/// Walks prefab widget templates following GauntletUI scoping rules: <c>DataSource</c> declarations create nested child ViewModel scopes,
/// item templates define <see cref="MBBindingList{T}"/> collections, and embedded prefabs inherit or extend active scope parameters.
/// Emits strongly typed C# ViewModel classes containing properties, collections, commands, and call logging.
/// </para>
/// </summary>
internal sealed class ViewModelSynthesizer
{
    internal sealed class Scope
    {
        public Scope(string className, Scope? parent)
        {
            ClassName = className;
            Parent = parent;
        }

        public string ClassName { get; }
        public Scope? Parent { get; }

        /// <summary>Gets or sets the parent list property identifier when the scope represents list item elements.</summary>
        public string? ListName { get; set; }

        /// <summary>Gets or sets the minimum item count required to satisfy indexed child paths.</summary>
        public int MinItems { get; set; }
        public Dictionary<string, Type> Properties { get; } = new(StringComparer.Ordinal);
        /// <summary>Maps properties declared as <see cref="object"/> to their underlying concrete value types.</summary>
        public Dictionary<string, Type> Held { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, Scope> Children { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, Scope> Lists { get; } = new(StringComparer.Ordinal);

        /// <summary>Maps command method identifiers to a value indicating whether any invocation supplies a command parameter.</summary>
        public Dictionary<string, bool> Commands { get; } = new(StringComparer.Ordinal);

        public bool Declares(string name) => Properties.ContainsKey(name) || Children.ContainsKey(name) || Lists.ContainsKey(name) || Commands.ContainsKey(name);
    }

    private const int MaxPrefabDepth = 12;

    /// <summary>Specifies the maximum re-entrant setter nesting depth allowed before terminating recursive setter loops.</summary>
    private const int MaxSetDepth = 16;

    /// <summary>Identifies reserved ViewModel member names to avoid collisions in synthesized classes.</summary>
    private static readonly HashSet<string> ReservedNames = new(
        typeof(ViewModel).GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Select(x => x.Name),
        StringComparer.Ordinal) { "Log", "Path", "Seed" };

    private readonly WidgetFactory _widgetFactory;
    private readonly bool _objectTyped;
    private int _classes;

    /// <param name="widgetFactory">The widget factory used to resolve widget types and prefabs.</param>
    /// <param name="objectTyped">Specifies whether approximately half of the synthesized properties are declared as <see cref="object"/> to test untyped binding paths.</param>
    public ViewModelSynthesizer(WidgetFactory widgetFactory, bool objectTyped = false)
    {
        _widgetFactory = widgetFactory;
        _objectTyped = objectTyped;
    }

    /// <summary>
    /// Maps widget properties reset by the engine upon widget disconnection (<c>EventManager.OnWidgetDisconnectedFromRoot</c>) to their reset values.
    /// Documented in <c>BindingLoaderOracle.IntendedDeviations["reset written back on removal"]</c>.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ResetOnRemoval = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["GamepadNavigationIndex"] = "-1",
        ["UsedNavigationMovements"] = "None",
        ["IsUsingNavigation"] = "False",
    };

    /// <summary>Maps ViewModel properties bound to reset-on-removal widget attributes to the values written back upon disconnection.</summary>
    public Dictionary<string, HashSet<string>> BoundToResetOnRemoval { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the collection of unique command event identifiers encountered across evaluated prefabs.</summary>
    public HashSet<string> EventNames { get; } = new(StringComparer.Ordinal);

    public List<Scope> Scopes { get; } = [];

    public Scope Collect(string prefabName)
    {
        var root = NewScope(null);
        var prefab = _widgetFactory.GetCustomType(prefabName);
        // The movie's root view is bound at Root whatever its template declares
        Visit(prefab.RootTemplate, prefab, root, new(), 0, ignoresDataSource: true);
        return root;
    }

    /// <summary>
    /// Computes the synthetic item count instantiated per list, bounding deeply recursive item templates to prevent memory exhaustion.
    /// </summary>
    private static int ItemsPerList(Scope items)
    {
        var listDepth = 0;
        for (var scope = items; scope.Parent is { } parent; scope = parent)
        {
            if (parent.Lists.ContainsValue(scope))
                listDepth++;
        }
        return listDepth <= 2 ? 2 : 1;
    }

    private Scope NewScope(Scope? parent)
    {
        var scope = new Scope($"S{_classes++}", parent);
        Scopes.Add(scope);
        return scope;
    }

    /// <summary>
    /// Traverses a widget template and its child hierarchy, resolving databinding scopes and extracting bound properties, lists, and commands.
    /// </summary>
    private Scope Visit(WidgetTemplate template, WidgetPrefab prefab, Scope parentScope, Dictionary<string, WidgetAttributeTemplate> given, int depth, bool ignoresDataSource)
    {
        var usage = template.GetExtensionData<ItemTemplateUsage>();
        var ownDataSource = ignoresDataSource ? null : template.GetAttributesOf<WidgetAttributeKeyTypeDataSource>()
            .Select(x => DataSourcePathOf(x, prefab, given)).FirstOrDefault(x => !string.IsNullOrEmpty(x));

        // Inherits scope from the root template of an embedded prefab unless overridden locally.
        var scope = parentScope;
        WidgetPrefab? used = null;
        Dictionary<string, WidgetAttributeTemplate>? passed = null;
        if (_widgetFactory.IsCustomType(template.Type) && depth < MaxPrefabDepth)
        {
            passed = new Dictionary<string, WidgetAttributeTemplate>(StringComparer.Ordinal);
            foreach (var attribute in template.AllAttributes.Where(x => x.KeyType is WidgetAttributeKeyTypeParameter))
            {
                // Forwards parameter values passed from the enclosing prefab context.
                passed[attribute.Key] = attribute.ValueType is WidgetAttributeValueTypeParameter && given.TryGetValue(attribute.Value, out var forwarded)
                    ? forwarded
                    : attribute;
            }
            try
            {
                used = _widgetFactory.GetCustomType(template.Type);
            }
            catch (Exception)
            {
                // Ignores unresolvable external prefab references during scoping.
            }
        }

        // Associates list collection scopes when either template declares item template usage.
        var rootBuildsItems = used?.RootTemplate.GetExtensionData<ItemTemplateUsage>() is not null;
        if (ownDataSource is not null)
            scope = Descend(parentScope, ownDataSource, asList: usage is not null || rootBuildsItems);
        if (used is not null)
        {
            // Merges bindings into the target scope only when the embedded root builds child items.
            var usedScope = Visit(used.RootTemplate, used, ownDataSource is not null && rootBuildsItems ? scope : parentScope, passed!, depth + 1, ignoresDataSource: ignoresDataSource || ownDataSource is not null);
            if (ownDataSource is null)
                scope = usedScope;
        }

        if (usage is not null)
        {
            // Descends into item templates using the list item scope; list widgets declare no direct property bindings.
            foreach (var itemTemplate in new[] { usage.DefaultItemTemplate, usage.FirstItemTemplate, usage.LastItemTemplate })
            {
                if (itemTemplate is not null)
                    Visit(itemTemplate, prefab, scope, given, depth, ignoresDataSource: false);
            }
        }
        else
        {
            foreach (var attribute in template.AllAttributes)
            {
                if (attribute.KeyType is WidgetAttributeKeyTypeAttribute or WidgetAttributeKeyTypeId && BindingOf(attribute, given) is { } binding)
                {
                    AddProperty(scope, binding, PropertyTypeOf(template.Type, attribute.Key));
                    if (ResetOnRemoval.TryGetValue(attribute.Key, out var reset))
                    {
                        var name = new BindingPath(binding).LastNode;
                        if (!BoundToResetOnRemoval.TryGetValue(name, out var values))
                            BoundToResetOnRemoval[name] = values = new HashSet<string>(StringComparer.Ordinal);
                        values.Add(reset);
                    }
                }
            }
        }

        var parameters = template.GetAttributesOf<WidgetAttributeKeyTypeCommandParameter>().Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var attribute in template.GetAttributesOf<WidgetAttributeKeyTypeCommand>())
        {
            if (Resolve(attribute, given) is { Length: > 0 } path)
            {
                EventNames.Add(attribute.Key);
                AddCommand(scope, path, parameters.Contains(attribute.Key));
            }
        }

        for (var i = 0; i < template.ChildCount; i++)
            Visit(template.GetChildAt(i), prefab, scope, given, depth, ignoresDataSource: false);
        return scope;
    }

    /// <summary>
    /// Resolves the effective DataSource binding path for an attribute template, substituting forwarded parameter values.
    /// </summary>
    private static string? DataSourcePathOf(WidgetAttributeTemplate attribute, WidgetPrefab prefab, Dictionary<string, WidgetAttributeTemplate> given) => attribute.ValueType switch
    {
        WidgetAttributeValueTypeBindingPath => attribute.Value,
        WidgetAttributeValueTypeParameter => given.TryGetValue(attribute.Value, out var passed) && passed.ValueType is WidgetAttributeValueTypeBindingPath
            ? passed.Value
            : prefab.GetParameterDefaultValue(attribute.Value),
        _ => null,
    };

    /// <summary>Resolves the string value of an attribute template, substituting forwarded parameter values where applicable.</summary>
    private static string? Resolve(WidgetAttributeTemplate attribute, Dictionary<string, WidgetAttributeTemplate> given) =>
        attribute.ValueType is WidgetAttributeValueTypeParameter
            ? given.TryGetValue(attribute.Value, out var passed) ? passed.Value : null
            : attribute.Value;

    /// <summary>Extracts the binding path expression from a direct or forwarded attribute template.</summary>
    private static string? BindingOf(WidgetAttributeTemplate attribute, Dictionary<string, WidgetAttributeTemplate> given) => attribute.ValueType switch
    {
        WidgetAttributeValueTypeBinding => attribute.Value,
        WidgetAttributeValueTypeParameter when given.TryGetValue(attribute.Value, out var passed) && passed.ValueType is WidgetAttributeValueTypeBinding => passed.Value,
        _ => null,
    };

    /// <summary>
    /// Resolves or instantiates child scopes along a delimited binding path expression following GauntletUI navigation semantics.
    /// </summary>
    private Scope Descend(Scope scope, string pathText, bool asList)
    {
        var nodes = new BindingPath(pathText).Nodes;
        // The list of `scope` the walk stands on, or null on the ViewModel itself
        string? atList = null;
        for (var i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
            if (node == "..")
            {
                if (atList is not null)
                    atList = null;
                else if (scope.ListName is { } listName && scope.Parent is { } owner)
                    (scope, atList) = (owner, listName);
                else
                    scope = scope.Parent ?? scope;
                continue;
            }
            if (node is "." or "")
                continue;
            if (atList is not null)
            {
                if (!IsIndex(node))
                    return NewScope(scope);
                scope = scope.Lists[atList];
                atList = null;
                if (int.TryParse(node, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var index) && index < 64)
                    scope.MinItems = Math.Max(scope.MinItems, index + 1);
                continue;
            }
            if (IsIndex(node) || !IsIdentifier(node))
                return NewScope(scope);
            var last = i == nodes.Length - 1;
            var list = last ? asList : IsIndex(nodes[i + 1]);
            if (list)
            {
                if (!scope.Lists.TryGetValue(node, out var items))
                {
                    if (scope.Declares(node))
                        return NewScope(scope);
                    scope.Lists[node] = items = NewScope(scope);
                    items.ListName = node;
                }
                if (last)
                    return items;
                atList = node;
                continue;
            }
            if (!scope.Children.TryGetValue(node, out var child))
            {
                if (scope.Declares(node))
                    return NewScope(scope);
                scope.Children[node] = child = NewScope(scope);
            }
            scope = child;
        }
        // Returns the list item scope when traversing item templates; otherwise creates an isolated scope.
        return atList is null ? scope : asList ? scope.Lists[atList] : NewScope(scope);
    }

    private static bool IsIndex(string node) => node.Length > 0 && char.IsDigit(node[0]);

    private void AddProperty(Scope scope, string pathText, Type type)
    {
        var path = new BindingPath(pathText);
        if (path.Nodes.Length > 1)
            scope = Descend(scope, path.ParentPath.Path, asList: false);
        var name = path.LastNode;
        if (IsIdentifier(name) && !scope.Declares(name) && !ReservedNames.Contains(name))
        {
            scope.Properties[name] = type;
            if (_objectTyped && (SynthValues.Hash(0, name, 0) & 2) != 0)
                scope.Held[name] = type;
        }
    }

    private void AddCommand(Scope scope, string pathText, bool withParameter)
    {
        var path = new BindingPath(pathText);
        if (path.Nodes.Length > 1)
            scope = Descend(scope, path.ParentPath.Path, asList: false);
        var name = path.LastNode;
        if (!IsIdentifier(name) || ReservedNames.Contains(name))
            return;
        if (scope.Commands.TryGetValue(name, out var existing))
            scope.Commands[name] = existing || withParameter;
        else if (!scope.Declares(name))
            scope.Commands[name] = withParameter;
    }

    /// <summary>Determines the declared C# property type for a ViewModel property bound to the specified widget attribute.</summary>
    private Type PropertyTypeOf(string widgetTypeName, string attributeName)
    {
        if (!_widgetFactory.TryGetWidgetTypeWithinPrefabRoots(widgetTypeName, out var widgetType) || WidgetPropertyPath.Resolve(widgetType, attributeName) is not { } property)
            return typeof(string);
        var type = property.PropertyType;
        if (type == typeof(bool) || type == typeof(int) || type == typeof(uint) || type == typeof(float) || type == typeof(double) ||
            type == typeof(string) || type == typeof(Color) || type == typeof(Vec2) || type == typeof(System.Numerics.Vector2) || (type.IsEnum && type.IsPublic))
            return type;
        // Falls back to string for complex types requiring runtime converter coercion (such as sprites or brushes).
        return typeof(string);
    }

    private static bool IsIdentifier(string name) =>
        name.Length > 0 && (char.IsLetter(name[0]) || name[0] == '_') && name.All(c => char.IsLetterOrDigit(c) || c == '_');

    // --- the C# -------------------------------------------------------------------------------------------------------

    /// <summary>Emits C# source code defining all synthesized ViewModel classes within the specified namespace.</summary>
    public string Emit(string ns)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"namespace {ns}");
        sb.AppendLine("{");
        foreach (var scope in Scopes)
            EmitScope(sb, scope);
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void EmitScope(StringBuilder sb, Scope scope)
    {
        var values = typeof(SynthValues).FullName!;
        sb.AppendLine($"public class {scope.ClassName} : TaleWorlds.Library.ViewModel");
        sb.AppendLine("{");
        sb.AppendLine($"    public readonly {values}.Log Log;");
        sb.AppendLine("    public readonly string Path;");
        sb.AppendLine("    public readonly int Seed;");
        string Declared(string name, Type type) => scope.Held.ContainsKey(name) ? "object" : TypeName(type);
        foreach (var (name, type) in scope.Properties)
            sb.AppendLine($"    private {Declared(name, type)} _{name};");
        // Tracks re-entrant setter invocation depth to prevent infinite binding update loops.
        sb.AppendLine("    private int _setDepth;");
        foreach (var (name, child) in scope.Children)
            sb.AppendLine($"    private {child.ClassName} _{name};");
        foreach (var (name, items) in scope.Lists)
            sb.AppendLine($"    private TaleWorlds.Library.MBBindingList<{items.ClassName}> _{name};");

        sb.AppendLine($"    public {scope.ClassName}({values}.Log log, string path, int seed)");
        sb.AppendLine("    {");
        sb.AppendLine("        Log = log; Path = path; Seed = seed;");
        foreach (var (name, type) in scope.Properties)
            sb.AppendLine($"        _{name} = {values}.Get<{TypeName(type)}>(seed, \"{name}\", 0);");
        foreach (var (name, child) in scope.Children)
            sb.AppendLine($"        _{name} = new {child.ClassName}(log, path + \"\\\\{name}\", seed * 31 + {name.Length});");
        foreach (var (name, items) in scope.Lists)
        {
            sb.AppendLine($"        _{name} = new TaleWorlds.Library.MBBindingList<{items.ClassName}>();");
            sb.AppendLine($"        for (var i = 0; i < {Math.Max(ItemsPerList(items), items.MinItems)}; i++) _{name}.Add(new {items.ClassName}(log, path + \"\\\\{name}\\\\\" + i, seed * 37 + i));");
        }
        sb.AppendLine("    }");

        foreach (var (name, type) in scope.Properties)
        {
            var typeName = Declared(name, type);
            var notify = scope.Held.ContainsKey(name) || HasTypedNotification(type) ? $"OnPropertyChangedWithValue(value, \"{name}\")" : $"OnPropertyChanged(\"{name}\")";
            sb.AppendLine("    [TaleWorlds.Library.DataSourceProperty]");
            if (scope.Held.ContainsKey(name))
                sb.AppendLine($"    [{typeof(SynthValues.HoldsAttribute).FullName!.Replace('+', '.')}(typeof({TypeName(type)}))]");
            sb.AppendLine($"    public {typeName} @{name}");
            sb.AppendLine("    {");
            sb.AppendLine($"        get => _{name};");
            // Prevents infinite recursion when bidirectional bindings update conflicting range clamped properties.
            sb.AppendLine($"        set {{ if (_setDepth >= {MaxSetDepth}) {{ Log.Add(Path + \".{name} set too deep, stopped\"); return; }} _setDepth++; try {{ if (!System.Collections.Generic.EqualityComparer<{typeName}>.Default.Equals(value, _{name})) {{ _{name} = value; Log.Add(Path + \".{name} = \" + {values}.Describe(value)); {notify}; }} }} finally {{ _setDepth--; }} }}");
            sb.AppendLine("    }");
        }
        foreach (var (name, child) in scope.Children)
        {
            sb.AppendLine("    [TaleWorlds.Library.DataSourceProperty]");
            sb.AppendLine($"    public {child.ClassName} @{name}");
            sb.AppendLine("    {");
            sb.AppendLine($"        get => _{name};");
            sb.AppendLine($"        set {{ if (value != _{name}) {{ _{name} = value; Log.Add(Path + \".{name} replaced\"); OnPropertyChangedWithValue(value, \"{name}\"); }} }}");
            sb.AppendLine("    }");
        }
        foreach (var (name, items) in scope.Lists)
        {
            sb.AppendLine("    [TaleWorlds.Library.DataSourceProperty]");
            sb.AppendLine($"    public TaleWorlds.Library.MBBindingList<{items.ClassName}> @{name}");
            sb.AppendLine("    {");
            sb.AppendLine($"        get => _{name};");
            sb.AppendLine($"        set {{ if (value != _{name}) {{ _{name} = value; Log.Add(Path + \".{name} replaced\"); OnPropertyChangedWithValue(value, \"{name}\"); }} }}");
            sb.AppendLine("    }");
        }
        foreach (var (name, withParameter) in scope.Commands)
        {
            sb.AppendLine(withParameter
                ? $"    public void @{name}(string parameter) => Log.Add(Path + \".{name}(\" + (parameter ?? \"null\") + \")\");"
                : $"    public void @{name}() => Log.Add(Path + \".{name}()\");");
        }
        sb.AppendLine("}");
    }

    private static bool HasTypedNotification(Type type) =>
        type == typeof(bool) || type == typeof(int) || type == typeof(uint) || type == typeof(float) || type == typeof(double) ||
        type == typeof(Color) || type == typeof(Vec2) || !type.IsValueType;

    private static string TypeName(Type type) => ViewModelMemberResolution.GetCodeTypeName(type)!;
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Xml;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.TwoDimension;

namespace Bannerlord.UIExtenderEx.Tests.Oracle;

/// <summary>
/// Captures observable visual tree state across widgets into a flat property map to compare XML runtime instantiation against compiled prefabs.
/// <para>
/// Records widget class names, IDs, child counts, and public readable properties. Recursively inspects nested objects (such as brushes, visual definitions,
/// and layouts) up to two levels deep to capture dotted attributes (<c>Brush.FontSize</c>, <c>LayoutImp.LayoutMethod</c>). Replaces instance-specific pointers
/// with stable structural representations: widgets by hierarchical tree path, sprites and fonts by name, and visual definitions by state properties.
/// </para>
/// </summary>
public static class WidgetSnapshot
{
    private const int MaxObjectDepth = 2;

    /// <summary>Identifies infrastructure properties that link back to shared engine contexts or parent tree hierarchies.</summary>
    private static readonly HashSet<string> SkippedProperties = new(StringComparer.Ordinal)
    {
        nameof(Widget.ParentWidget), nameof(Widget.Context), nameof(Widget.EventManager), nameof(Widget.GamepadNavigationContext),
    };

    /// <summary>
    /// Generates a shallow or deep property snapshot for the widget tree rooted at <paramref name="root"/>, keyed by tree path and property identifier.
    /// Excludes private backing fields to minimize memory consumption during high-frequency comparison passes.
    /// </summary>
    /// <param name="root">The root widget of the visual tree to snapshot.</param>
    /// <param name="visibleType">Delegate normalizing generated proxy types to public TaleWorlds widget types.</param>
    /// <param name="objectDepth">Specifies the recursive object traversal depth for nested complex properties.</param>
    public static Dictionary<string, Dictionary<string, string>> Take(Widget root, Func<Type, Type> visibleType, int objectDepth = MaxObjectDepth)
    {
        var snapshot = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        AddWidget(snapshot, root, root, "root", visibleType, objectDepth);
        return snapshot;
    }

    /// <summary>Determines whether the specified snapshot property key represents a field rather than a property or class identifier.</summary>
    public static bool IsField(string key) => key.Length > 0 && key[0] == ':' && key != ":Class";

    /// <summary>
    /// Compares widget hierarchies between XML and compiled implementations side by side, evaluating properties and private state fields incrementally.
    /// Filters out non-deterministic discrepancies identified by the secondary control tree.
    /// </summary>
    /// <param name="xml">The primary XML runtime widget tree.</param>
    /// <param name="compiled">The compiled prefab widget tree.</param>
    /// <param name="control">The secondary control XML tree used to detect non-deterministic runtime variations.</param>
    /// <param name="visibleType">Delegate normalizing proxy types to public TaleWorlds widget types.</param>
    /// <param name="objectDepth">Specifies the recursive object inspection depth for complex properties.</param>
    /// <param name="missingControlIsUnstable">Specifies whether absence of the control tree indicates that the prefab is unstable.</param>
    /// <param name="visit">Optional callback invoked with each widget's path and captured property dictionaries during traversal.</param>
    public static List<(string Key, string Line, string? Xml, string? Compiled)> Compare(Widget? xml, Widget? compiled, Widget? control, Func<Type, Type> visibleType,
        int objectDepth = MaxObjectDepth, bool missingControlIsUnstable = false, Action<string, Dictionary<string, string>, Dictionary<string, string>>? visit = null)
    {
        var differences = new List<(string Key, string Line, string? Xml, string? Compiled)>();
        var controlSide = control is null && !missingControlIsUnstable ? (Side?) null : new Side(control, control);
        CompareWidget(differences, new Side(xml, xml), new Side(compiled, compiled), controlSide, "root", visibleType, objectDepth, visit);
        return differences;
    }

    /// <summary>Encapsulates a widget node and its relative tree root during multi-tree comparison traversal.</summary>
    private readonly struct Side(Widget? root, Widget? widget)
    {
        public Widget? Widget { get; } = widget;

        public Side Child(int index) => new(root, Widget is { } parent && index < parent.ChildCount ? parent.GetChild(index) : null);

        /// <summary>Rents a property dictionary from the internal pool and populates it with the widget's local values.</summary>
        public Dictionary<string, string> Own(string path, Func<Type, Type> visibleType, int objectDepth)
        {
            var values = RentValues();
            if (Widget is not null)
                AddOwn(values, Widget, root!, path, visibleType, objectDepth);
            return values;
        }
    }

    [ThreadStatic]
    private static Stack<Dictionary<string, string>>? _valuesPool;

    /// <summary>
    /// Rents a dictionary instance from the thread-local pool to capture single-widget property maps during comparison without heap thrashing.
    /// </summary>
    private static Dictionary<string, string> RentValues() =>
        _valuesPool is { Count: > 0 } pool ? pool.Pop() : new Dictionary<string, string>(StringComparer.Ordinal);

    private static void ReturnValues(Dictionary<string, string>? values)
    {
        if (values is null)
            return;
        values.Clear();
        (_valuesPool ??= new Stack<Dictionary<string, string>>()).Push(values);
    }

    [ThreadStatic]
    private static HashSet<object>? _visiting;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Type Type, int Depth, bool Fields), int> ValueCounts = new();

    /// <summary>
    /// Allocates a new property dictionary initialized to the cached capacity previously recorded for the widget type.
    /// </summary>
    private static Dictionary<string, string> NewValues(Widget widget, int objectDepth, bool fields) =>
        new(ValueCounts.TryGetValue((widget.GetType(), objectDepth, fields), out var count) ? count : 0, StringComparer.Ordinal);

    private static void Sized(Widget widget, int objectDepth, bool fields, Dictionary<string, string> values) =>
        ValueCounts[(widget.GetType(), objectDepth, fields)] = values.Count;

    private static void CompareWidget(List<(string Key, string Line, string? Xml, string? Compiled)> differences, Side xml, Side compiled, Side? control, string path, Func<Type, Type> visibleType, int objectDepth,
        Action<string, Dictionary<string, string>, Dictionary<string, string>>? visit)
    {
        var xmlValues = xml.Own(path, visibleType, objectDepth);
        var compiledValues = compiled.Own(path, visibleType, objectDepth);
        // Reads control values unconditionally to ensure side-effecting property getters execute identically across trees.
        var controlValues = control?.Own(path, visibleType, objectDepth);
        visit?.Invoke(path, xmlValues, compiledValues);
        // Skips key sorting and difference extraction when property maps match completely.
        if (!Same(xmlValues, compiledValues))
        {
            foreach (var (key, xmlValue, compiledValue) in DiffKeys(xmlValues, compiledValues))
            {
                // Filters unstable properties where the secondary XML control also disagrees with the primary XML build.
                if (controlValues is not null && !SameAt(xmlValues, controlValues, key))
                    continue;
                differences.Add((path + key, Line(path + key, xmlValue, compiledValue), xmlValue, compiledValue));
            }
        }
        // Recycles rented property dictionaries to the pool prior to descending into child widgets.
        ReturnValues(xmlValues);
        ReturnValues(compiledValues);
        ReturnValues(controlValues);

        var children = Math.Max(xml.Widget?.ChildCount ?? 0, compiled.Widget?.ChildCount ?? 0);
        for (var i = 0; i < children; i++)
            CompareWidget(differences, xml.Child(i), compiled.Child(i), control?.Child(i), $"{path}[{i}]", visibleType, objectDepth, visit);
    }

    private static bool Same(Dictionary<string, string> fromXml, Dictionary<string, string> compiled)
    {
        if (fromXml.Count != compiled.Count)
            return false;
        foreach (var pair in fromXml)
        {
            if (!compiled.TryGetValue(pair.Key, out var value) || value != pair.Value)
                return false;
        }
        return true;
    }

    /// <summary>Determines whether both dictionaries contain identical values for the specified key or both omit it.</summary>
    private static bool SameAt(Dictionary<string, string> left, Dictionary<string, string> right, string key)
    {
        var inLeft = left.TryGetValue(key, out var leftValue);
        var inRight = right.TryGetValue(key, out var rightValue);
        return inLeft == inRight && leftValue == rightValue;
    }

    /// <summary>
    /// Extracts differing and unique keys between XML and compiled dictionaries, returning entries sorted ordinally by key.
    /// </summary>
    private static List<(string Key, string? Xml, string? Compiled)> DiffKeys(Dictionary<string, string> fromXml, Dictionary<string, string> compiled)
    {
        var differing = new List<(string Key, string? Xml, string? Compiled)>();
        foreach (var pair in fromXml)
        {
            if (!compiled.TryGetValue(pair.Key, out var compiledValue))
                differing.Add((pair.Key, pair.Value, null));
            else if (compiledValue != pair.Value)
                differing.Add((pair.Key, pair.Value, compiledValue));
        }
        foreach (var pair in compiled)
        {
            if (!fromXml.ContainsKey(pair.Key))
                differing.Add((pair.Key, null, pair.Value));
        }
        differing.Sort((x, y) => string.CompareOrdinal(x.Key, y.Key));
        return differing;
    }

    private static string Line(string key, string? xml, string? compiled) =>
        $"{key}: XML {(xml is not null ? $"'{xml}'" : "<absent>")}, compiled {(compiled is not null ? $"'{compiled}'" : "<absent>")}";

    private static void AddWidget(Dictionary<string, Dictionary<string, string>> snapshot, Widget widget, Widget root, string path, Func<Type, Type> visibleType, int objectDepth)
    {
        var own = NewValues(widget, objectDepth, fields: false);
        AddOwn(own, widget, root, path, visibleType, objectDepth, fields: false);
        Sized(widget, objectDepth, fields: false, own);
        snapshot[path] = own;
        for (var i = 0; i < widget.ChildCount; i++)
            AddWidget(snapshot, widget.GetChild(i), root, $"{path}[{i}]", visibleType, objectDepth);
    }

    /// <summary>
    /// Populates property and field state for a single widget relative to its local path, excluding child widgets.
    /// </summary>
    private static void AddOwn(Dictionary<string, string> snapshot, Widget widget, Widget root, string widgetPath, Func<Type, Type> visibleType, int objectDepth, bool fields = true)
    {
        var type = visibleType(widget.GetType());
        snapshot[":Class"] = type.FullName!;
        // Clears the thread-local cycle detection set for each widget node before traversal.
        var visiting = _visiting ??= new HashSet<object>(ReferenceComparer.Instance);
        visiting.Clear();
        visiting.Add(widget);
        AddProperties(snapshot, widget, type, root, widgetPath, "", visibleType, depth: MaxObjectDepth - objectDepth, visiting);
        if (fields)
            AddFields(snapshot, widget, type, root, widgetPath, visibleType, visiting);
    }

    /// <summary>
    /// Inspects declared instance fields across the widget type hierarchy to capture internal state modifications (such as cloned brushes or event subscriptions).
    /// Omits proxy fields belonging solely to compiled code generators.
    /// </summary>
    private static void AddFields(Dictionary<string, string> snapshot, Widget widget, Type type, Widget root, string widgetPath, Func<Type, Type> visibleType, HashSet<object> visiting)
    {
        foreach (var field in FieldsOf(type))
        {
            object? value;
            try
            {
                OracleMemoryGuard.Reading(widgetPath, "", field.Name, type.Name);
                value = field.Read(widget);
            }
            catch (Exception e) when (e is not OracleMemoryExceededException)
            {
                snapshot[field.Name] = $"<throws {e.GetType().Name}>";
                continue;
            }
            if (value is Delegate handlers)
            {
                // A navigation scope of a widget that has left the tree is kept apart: the loader leaves some behind, see
                // BindingLoaderOracle.IntendedDeviations["list rebuilt through {..}"]
                var subscribers = handlers.GetInvocationList();
                var removed = subscribers.Where(x => IsOfARemovedWidget(x, widget)).ToArray();
                snapshot[field.Name] = DescribeHandlers(subscribers.Except(removed).ToArray(), visibleType);
                if (removed.Length > 0)
                    snapshot[field.Name + RemovedSubscribers] = DescribeHandlers(removed, visibleType);
                continue;
            }
            AddValue(snapshot, value, root, widgetPath, field.Name, visibleType, MaxObjectDepth, visiting);
        }
    }

    private static void AddProperties(Dictionary<string, string> snapshot, object target, Type type, Widget root, string widgetPath, string path,
        Func<Type, Type> visibleType, int depth, HashSet<object> visiting)
    {
        foreach (var property in PropertiesOf(type))
        {
            object? value;
            try
            {
                // Notifies the memory watchdog of the active property read to pinpoint runaway getters.
                OracleMemoryGuard.Reading(widgetPath, path, property.Name, type.Name);
                value = property.Read(target);
            }
            catch (Exception e) when (e is not OracleMemoryExceededException)
            {
                snapshot[$"{path}.{property.Name}"] = $"<throws {e.GetType().Name}>";
                continue;
            }
            AddValue(snapshot, value, root, widgetPath, $"{path}.{property.Name}", visibleType, depth, visiting);
        }
    }

    private static void AddValue(Dictionary<string, string> snapshot, object? value, Widget root, string widgetPath, string path, Func<Type, Type> visibleType, int depth, HashSet<object> visiting)
    {
        switch (value)
        {
            case null:
                snapshot[path] = "null";
                return;
            case string text:
                snapshot[path] = text;
                return;
            case float number:
                snapshot[path] = number.ToString("R", CultureInfo.InvariantCulture);
                return;
            case double number:
                snapshot[path] = number.ToString("R", CultureInfo.InvariantCulture);
                return;
            case IFormattable formattable when value.GetType().IsPrimitive || value.GetType().IsEnum || value is decimal:
                snapshot[path] = formattable.ToString(null, CultureInfo.InvariantCulture);
                return;
            case bool flag:
                snapshot[path] = flag ? "true" : "false";
                return;
            case Widget widget:
                snapshot[path] = "widget " + TreePath(widget, root);
                return;
            case Sprite sprite:
                snapshot[path] = "sprite " + sprite.Name;
                return;
            case Font font:
                snapshot[path] = "font " + font.Name;
                return;
            case VisualDefinition definition:
                snapshot[path] = DescribeVisualDefinition(definition);
                return;
            case XmlNode node:
                snapshot[path] = node.OuterXml;
                return;
            case Type or Delegate or UIContext:
                return;
            case ICollection collection:
                snapshot[path] = "count " + collection.Count.ToString(CultureInfo.InvariantCulture);
                // Records child elements up to the maximum traversal depth, retaining element count beyond the threshold.
                if (depth < MaxObjectDepth)
                    AddItems(snapshot, collection, root, widgetPath, path, visibleType, depth, visiting);
                return;
            case IEnumerable:
                return;
        }

        var type = value.GetType();
        if (type.IsValueType)
        {
            snapshot[path] = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
            return;
        }
        if (type.Namespace?.StartsWith("System", StringComparison.Ordinal) == true)
            return;

        snapshot[path] = visibleType(type).FullName!;
        if (depth >= MaxObjectDepth || !visiting.Add(value))
            return;
        AddProperties(snapshot, value, type, root, widgetPath, path, visibleType, depth + 1, visiting);
        visiting.Remove(value);
    }

    /// <summary>
    /// Formats the subscriber list for an event delegate, identifying subscriber counts and declaring methods to isolate dangling subscriptions.
    /// </summary>
    private static string DescribeHandlers(Delegate[] list, Func<Type, Type> visibleType)
    {
        var whose = list
            .GroupBy(x => $"{(x.Target is { } target ? visibleType(target.GetType()).Name : x.Method.DeclaringType?.Name)}.{x.Method.Name}")
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => $"{x.Key} x{x.Count().ToString(CultureInfo.InvariantCulture)}");
        return $"handlers {list.Length.ToString(CultureInfo.InvariantCulture)} ({string.Join(", ", whose)})";
    }

    /// <summary>Represents the snapshot key suffix identifying event subscribers attached to widgets detached from the active hierarchy.</summary>
    public const string RemovedSubscribers = " (of widgets removed from the tree)";

    /// <summary>Parses the total subscriber count from a handler description string generated by <see cref="DescribeHandlers"/>.</summary>
    public static int HandlerCount(string? value) =>
        value is not null && value.StartsWith("handlers ", StringComparison.Ordinal)
        && int.TryParse(value.Substring("handlers ".Length).Split(' ')[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : 0;

    /// <summary>
    /// Determines whether a subscriber represents a gamepad navigation scope whose widget has been detached from the hierarchy containing <paramref name="owner"/>.
    /// </summary>
    private static bool IsOfARemovedWidget(Delegate subscriber, Widget owner) =>
        subscriber.Target is TaleWorlds.GauntletUI.GamepadNavigation.GamepadNavigationScope scope
        && (scope.ParentWidget is not { } parent || TopOf(parent) != TopOf(owner));

    private static Widget TopOf(Widget widget)
    {
        var current = widget;
        while (current.ParentWidget is { } parent)
            current = parent;
        return current;
    }

    private const int MaxItems = 64;

    /// <summary>
    /// Traverses and records items within a collection or dictionary up to <see cref="MaxItems"/>, indexing entries by numeric offset or dictionary key.
    /// </summary>
    private static void AddItems(Dictionary<string, string> snapshot, ICollection collection, Widget root, string widgetPath, string path, Func<Type, Type> visibleType, int depth, HashSet<object> visiting)
    {
        if (collection.Count > MaxItems || !visiting.Add(collection))
            return;
        try
        {
            if (collection is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                    AddValue(snapshot, entry.Value, root, widgetPath, $"{path}[{Convert.ToString(entry.Key, CultureInfo.InvariantCulture)}]", visibleType, depth + 1, visiting);
                return;
            }
            var index = 0;
            foreach (var item in collection)
                AddValue(snapshot, item, root, widgetPath, $"{path}[{index++}]", visibleType, depth + 1, visiting);
        }
        catch (InvalidOperationException)
        {
            // Handles collections mutated during enumeration by state-modifying property getters.
            snapshot[path + "[]"] = "<changed while read>";
        }
        finally
        {
            visiting.Remove(collection);
        }
    }

    /// <summary>Encapsulates a property name and a delegate invoker that evaluates its getter.</summary>
    private sealed class Readable(string name, Func<object, object?> read)
    {
        public string Name { get; } = name;
        public Func<object, object?> Read { get; } = read;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Readable[]> Properties = new();

    /// <summary>
    /// Discovers and caches readable public properties on the specified type, prioritizing the most derived declarations in alphabetical order.
    /// </summary>
    private static Readable[] PropertiesOf(Type type) => Properties.GetOrAdd(type, x => x.GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && p.GetGetMethod() is not null && !SkippedProperties.Contains(p.Name))
        // Skips BrushWidget.Brush because reading it instantiates a duplicate brush clone, inflating memory and mutating state.
        // ReadOnlyBrush inspects the active brush without allocating redundant clones.
        .Where(p => !(p.Name == nameof(BrushWidget.Brush) && typeof(BrushWidget).IsAssignableFrom(x)))
        .GroupBy(p => p.Name)
        .Select(g => g.OrderByDescending(p => Depth(p.DeclaringType!)).First())
        .OrderBy(p => p.Name, StringComparer.Ordinal)
        .Select(p => new Readable(p.Name, ReaderOf(p)))
        .ToArray());

    /// <summary>
    /// Creates a fast compiled delegate for the property getter, falling back to standard reflection when delegate creation is unsupported.
    /// </summary>
    private static Func<object, object?> ReaderOf(PropertyInfo property)
    {
        var getter = property.GetGetMethod()!;
        var declaring = property.DeclaringType!;
        if (!declaring.IsValueType && !declaring.ContainsGenericParameters && !property.PropertyType.IsByRef && !property.PropertyType.IsPointer)
        {
            try
            {
                return (Func<object, object?>) typeof(WidgetSnapshot).GetMethod(nameof(Reader), BindingFlags.Static | BindingFlags.NonPublic)!
                    .MakeGenericMethod(declaring, property.PropertyType).Invoke(null, [getter])!;
            }
            catch (Exception)
            {
                // Falls back to standard reflection invocation when dynamic delegate construction fails.
            }
        }
        return target =>
        {
            try
            {
                return property.GetValue(target);
            }
            catch (TargetInvocationException e) when (e.InnerException is { } inner)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(inner).Throw();
                throw;
            }
        };
    }

    private static Func<object, object?> Reader<TTarget, TValue>(MethodInfo getter) where TTarget : class
    {
        var read = (Func<TTarget, TValue>) Delegate.CreateDelegate(typeof(Func<TTarget, TValue>), getter);
        return target => read((TTarget) target);
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Readable[]> Fields = new();

    /// <summary>
    /// Defines internal framework fields omitted from widget snapshots, documenting architectural differences between XML runtime views and compiled widgets.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> SkippedFields = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [":Widget._components"] = "Where a movie keeps its binding on the widget: a GauntletView per widget from XML, a " +
                                  "GeneratedWidgetData from a compiled prefab. Which one it is is what compiled means.",
        [":Widget._eventTargets"] = "The GauntletViews that hear a widget's events and run its commands; a compiled prefab " +
                                    "subscribes its commands to EventFire itself, which is counted as any other handler.",
    };

    /// <summary>
    /// Enumerates all instance fields declared on the widget type hierarchy down to <see cref="Widget"/>, skipping auto-property backing fields already captured.
    /// </summary>
    private static Readable[] FieldsOf(Type type) => Fields.GetOrAdd(type, x =>
    {
        var fields = new List<Readable>();
        for (var current = x; current is not null && typeof(Widget).IsAssignableFrom(current); current = current.BaseType)
        {
            var read = PropertiesOf(current).Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
            foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).OrderBy(f => f.Name, StringComparer.Ordinal))
            {
                if (field.Name.StartsWith("<", StringComparison.Ordinal) && field.Name.IndexOf('>') is var end and > 1 && read.Contains(field.Name.Substring(1, end - 1)))
                    continue;
                if (field.FieldType.IsPointer || field.FieldType.IsByRef || IsRefStruct(field.FieldType))
                    continue;
                var name = $":{current.Name}.{field.Name}";
                if (!SkippedFields.ContainsKey(name))
                    fields.Add(new Readable(name, ReaderOf(field)));
            }
        }
        return [.. fields.OrderBy(f => f.Name, StringComparer.Ordinal)];
    });

    /// <summary>Compiles an expression tree lambda to access the specified field, falling back to reflection if compilation fails.</summary>
    private static Func<object, object?> ReaderOf(FieldInfo field)
    {
        try
        {
            var target = System.Linq.Expressions.Expression.Parameter(typeof(object));
            var body = System.Linq.Expressions.Expression.Convert(
                System.Linq.Expressions.Expression.Field(System.Linq.Expressions.Expression.Convert(target, field.DeclaringType!), field), typeof(object));
            return System.Linq.Expressions.Expression.Lambda<Func<object, object?>>(body, target).Compile();
        }
        catch (Exception)
        {
            return field.GetValue;
        }
    }

    private static bool IsRefStruct(Type type) =>
        type.GetCustomAttributes(false).Any(x => x.GetType().FullName == "System.Runtime.CompilerServices.IsByRefLikeAttribute");

    private static int Depth(Type type)
    {
        var depth = 0;
        for (var current = type; current is not null; current = current.BaseType)
            depth++;
        return depth;
    }

    private static string TreePath(Widget widget, Widget root)
    {
        // Accumulates ancestor indices from child to root, then reverses order to construct the canonical path.
        var steps = new List<int>();
        for (var current = widget; current != root; current = current.ParentWidget)
        {
            if (current.ParentWidget is not { } parent)
                return "<outside the tree>";
            steps.Add(parent.GetChildIndex(current));
        }
        var path = new System.Text.StringBuilder("root", 4 + steps.Count * 4);
        for (var i = steps.Count - 1; i >= 0; i--)
            path.Append('[').Append(steps[i].ToString(CultureInfo.InvariantCulture)).Append(']');
        return path.ToString();
    }

    private static string DescribeVisualDefinition(VisualDefinition definition)
    {
        var states = definition.VisualStates.Values.OrderBy(x => x.State, StringComparer.Ordinal).Select(state =>
            state.State + "{" + string.Join(", ", typeof(VisualState).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(x => x.Name != nameof(VisualState.State))
                .OrderBy(x => x.Name, StringComparer.Ordinal)
                .Select(x => $"{x.Name}={Convert.ToString(x.GetValue(state), CultureInfo.InvariantCulture)}")) + "}");
        return $"visual definition {definition.Name} ({definition.TransitionDuration.ToString("R", CultureInfo.InvariantCulture)}, {definition.DelayOnBegin.ToString("R", CultureInfo.InvariantCulture)}, {definition.EaseType}, {definition.EaseFunction}): {string.Join("; ", states)}";
    }

    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceComparer Instance = new();
        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
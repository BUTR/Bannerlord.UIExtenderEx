using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Bannerlord.UIExtenderEx.Tests.Oracle;

/// <summary>
/// Provides reference graph path traversal diagnostics to identify objects and static fields retaining target instances in memory.
/// Traverses reference graphs breadth-first starting from static fields and explicit root objects.
/// </summary>
internal static class OracleRetention
{
    private const int MaxObjects = 30_000_000;

    /// <summary>Finds the shortest reference path from registered roots and static fields to the specified target instance.</summary>
    public static string? PathTo(object target, IEnumerable<Assembly> assemblies, IEnumerable<(string Name, object? Root)> roots)
    {
        var parents = new Dictionary<object, (object? Parent, string Edge)>(ReferenceComparer.Instance);
        var pending = new Queue<object>();

        void Visit(object? value, object? parent, string edge)
        {
            if (value is null || value is string || value is Type || value is MemberInfo || value is Assembly || value is Module || value is Pointer)
                return;
            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || type.IsPointer)
                return;
            if (parents.ContainsKey(value) || parents.Count >= MaxObjects)
                return;
            parents[value] = (parent, edge);
            pending.Enqueue(value);
        }

        foreach (var (name, root) in roots)
            Visit(root, null, name);
        foreach (var type in assemblies.SelectMany(TypesOf))
        {
            if (type.ContainsGenericParameters)
                continue;
            foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (field.FieldType.IsPointer || field.IsLiteral)
                    continue;
                object? value;
                try { value = field.GetValue(null); }
                catch (Exception) { continue; }
                Visit(value, null, $"static {type.FullName}.{field.Name}");
            }
        }

        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            if (ReferenceEquals(current, target))
                return Describe(current, parents);

            switch (current)
            {
                case Delegate handler:
                    var index = 0;
                    foreach (var single in handler.GetInvocationList())
                    {
                        if (!ReferenceEquals(single, handler))
                            Visit(single, current, $"[handler {index}]");
                        Visit(single.Target, current, $"target of {single.Method.DeclaringType?.Name}.{single.Method.Name}");
                        index++;
                    }
                    continue;
                case Array array when !array.GetType().GetElementType()!.IsPrimitive:
                    var i = 0;
                    foreach (var item in array)
                        Visit(item, current, $"[{i++}]");
                    continue;
                case Array:
                    continue;
            }

            foreach (var field in FieldsOf(current.GetType()))
            {
                object? value;
                try { value = field.GetValue(current); }
                catch (Exception) { continue; }
                Visit(value, current, field.Name);
            }
        }
        return null;
    }

    private static string Describe(object found, Dictionary<object, (object? Parent, string Edge)> parents)
    {
        var steps = new List<string>();
        for (object? current = found; current is not null;)
        {
            var (parent, edge) = parents[current];
            steps.Add($"{edge} ({current.GetType().FullName})");
            current = parent;
        }
        steps.Reverse();
        return string.Join(Environment.NewLine + "    -> ", steps);
    }

    private static readonly Dictionary<Type, FieldInfo[]> Fields = [];

    private static FieldInfo[] FieldsOf(Type type)
    {
        if (Fields.TryGetValue(type, out var fields))
            return fields;
        var all = new List<FieldInfo>();
        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            all.AddRange(current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(x => !x.FieldType.IsPointer && !x.FieldType.IsPrimitive && !x.FieldType.IsEnum && x.FieldType != typeof(string)));
        }
        Fields[type] = fields = [.. all];
        return fields;
    }

    private static IEnumerable<Type> TypesOf(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException e) { return e.Types.Where(x => x is not null)!; }
        catch (Exception) { return []; }
    }

    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceComparer Instance = new();
        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
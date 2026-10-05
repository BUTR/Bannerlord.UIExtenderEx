using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using TaleWorlds.GauntletUI;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets;

/// <summary>
/// Analyzes widget IL to determine which property change notification overloads and properties a widget class can raise.
/// <para>
/// Inspects calls to <see cref="PropertyOwnerObject.OnPropertyChanged"/> across the class hierarchy to identify
/// candidate notification variants (e.g. object, bool, float, Vec2, Color) and property names.
/// Allows databinding emitters to attach only the specific writeback listeners that the widget actually fires.
/// </para>
/// </summary>
public static class WidgetRaises
{
    /// <summary>Lists the nine property change notification variant names supported by Gauntlet.</summary>
    public static readonly string[] Variants = ["", "bool", "float", "Vec2", "Vector2", "double", "int", "uint", "Color"];

    /// <summary>Stores the statically analyzed IL notification properties and variants for a widget class.</summary>
    private sealed class Raises
    {
        public readonly HashSet<(string Variant, string Name)> Named = [];
        public readonly HashSet<string> AnyName = [];
        public string? Undecided;
    }

    private static readonly ConcurrentDictionary<Type, Raises> Cache = new();

    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(x => (OpCode) x.GetValue(null)!)
        .ToDictionary(x => x.Value);

    private const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    /// <summary>
    /// Determines the notification variants required to listen for writebacks on the specified bound properties.
    /// </summary>
    /// <param name="widgetType">The widget class type to inspect.</param>
    /// <param name="boundProperties">The collection of property names bound on the widget.</param>
    /// <param name="undecided">When static IL analysis cannot determine raised events, receives the explanation string; otherwise, <see langword="null"/>.</param>
    /// <returns>The list of required notification variant names in standard order.</returns>
    public static IReadOnlyList<string> Needed(Type? widgetType, IEnumerable<string> boundProperties, out string? undecided)
    {
        if (widgetType is null)
        {
            undecided = "the widget class did not resolve";
            return Variants;
        }
        var raises = Cache.GetOrAdd(widgetType, Read);
        undecided = raises.Undecided ?? PatchedMethodOf(widgetType);
        if (undecided is not null)
            return Variants;
        var properties = new HashSet<string>(boundProperties, StringComparer.Ordinal);
        return [.. Variants.Where(x => raises.AnyName.Contains(x) || raises.Named.Any(y => y.Variant == x && properties.Contains(y.Name)))];
    }

    /// <summary>
    /// Identifies the first method within the widget type hierarchy modified by a runtime Harmony patch, if any.
    /// </summary>
    /// <param name="widgetType">The widget type to inspect.</param>
    /// <returns>A description of the patched method, or <see langword="null"/> if unpatched.</returns>
    public static string? PatchedMethodOf(Type widgetType)
    {
        foreach (var method in Methods.GetOrAdd(widgetType, static x => [.. Hierarchy(x).SelectMany(MethodsOf)]))
        {
            if (CodeGeneratorEnvironment.IsPatched(method))
                return $"{method.DeclaringType?.FullName}.{method.Name} is patched";
        }
        return null;
    }

    /// <summary>Caches resolved method lists across widget class hierarchies for patch inspection.</summary>
    private static readonly ConcurrentDictionary<Type, MethodBase[]> Methods = new();

    /// <summary>Enumerates all types in the widget class inheritance hierarchy down to <see cref="PropertyOwnerObject"/>.</summary>
    private static IEnumerable<Type> Hierarchy(Type widgetType)
    {
        for (var current = widgetType; current is not null && current != typeof(PropertyOwnerObject) && current != typeof(object); current = current.BaseType)
        {
            foreach (var type in WithNested(current))
                yield return type;
        }
    }

    private static IEnumerable<Type> WithNested(Type type) =>
        new[] { type }.Concat(type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).SelectMany(WithNested));

    private static IEnumerable<MethodBase> MethodsOf(Type type) =>
        type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared));

    private static Raises Read(Type widgetType)
    {
        var raises = new Raises();
        try
        {
            foreach (var type in Hierarchy(widgetType))
            {
                foreach (var method in MethodsOf(type))
                {
                    if (method.IsAbstract)
                        continue;
                    // A delegate's Invoke, BeginInvoke and constructor are the runtime's, and raise nothing
                    if ((method.MethodImplementationFlags & (MethodImplAttributes.Runtime | MethodImplAttributes.InternalCall)) != 0)
                        continue;
                    if (method.GetMethodBody()?.GetILAsByteArray() is not { } il)
                    {
                        raises.Undecided = $"{type.FullName}.{method.Name} has no IL to read";
                        return raises;
                    }
                    if (ReadCalls(method, il, raises) is { } failure)
                    {
                        raises.Undecided = $"{type.FullName}.{method.Name}: {failure}";
                        return raises;
                    }
                }
            }
        }
        catch (Exception e)
        {
            raises.Undecided = $"reading {widgetType.FullName} failed: {e.GetType().Name}: {e.Message}";
        }
        return raises;
    }

    /// <summary>Every call of an <c>OnPropertyChanged</c> overload in <paramref name="il"/>, with the name it passes when that is a constant; why the IL could not be read, or null.</summary>
    private static string? ReadCalls(MethodBase method, byte[] il, Raises raises)
    {
        var typeArguments = method.DeclaringType is { IsGenericType: true } declaring ? declaring.GetGenericArguments() : null;
        var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
        string? constantName = null;
        for (var i = 0; i < il.Length;)
        {
            short value = il[i++];
            if (value == 0xFE && i < il.Length)
                value = unchecked((short) (0xFE00 | il[i++]));
            if (!OpCodesByValue.TryGetValue(value, out var opCode))
                return $"unknown opcode 0x{value:X} at {i - 1}";
            var operand = i;
            i += opCode.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, i),
                _ => 4,
            };
            if (i > il.Length)
                return "IL ends inside an instruction";

            // The name is the last argument, so a constant one is the string loaded right before the call
            var name = constantName;
            constantName = opCode == OpCodes.Ldstr ? method.Module.ResolveString(BitConverter.ToInt32(il, operand)) : null;
            // A delegate made of an overload raises whatever name it is invoked with, later and from anywhere
            var asDelegate = opCode == OpCodes.Ldftn || opCode == OpCodes.Ldvirtftn;
            if (opCode != OpCodes.Call && opCode != OpCodes.Callvirt && !asDelegate)
                continue;

            MethodBase? callee;
            try
            {
                callee = method.Module.ResolveMethod(BitConverter.ToInt32(il, operand), typeArguments, methodArguments);
            }
            catch (Exception)
            {
                // A method of an assembly that is not loaded: not one of PropertyOwnerObject's, whose assembly a widget's is built on
                continue;
            }
            if (callee is not MethodInfo { Name: "OnPropertyChanged" } overload || overload.DeclaringType != typeof(PropertyOwnerObject))
                continue;
            if (VariantOf(overload) is not { } variant)
                return $"an OnPropertyChanged overload this does not know: {overload}";
            if (name is not null && !asDelegate)
                raises.Named.Add((variant, name));
            else
                raises.AnyName.Add(variant);
        }
        return null;
    }

    /// <summary>The notification an overload raises, by its value parameter; the generic one, for any class, raises the object one.</summary>
    private static string? VariantOf(MethodInfo overload)
    {
        var parameters = (overload.IsGenericMethod ? overload.GetGenericMethodDefinition() : overload).GetParameters();
        if (parameters.Length != 2)
            return null;
        var type = parameters[0].ParameterType;
        if (type.IsGenericParameter)
            return "";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(float)) return "float";
        if (type == typeof(double)) return "double";
        if (type == typeof(int)) return "int";
        if (type == typeof(uint)) return "uint";
        if (type == typeof(TaleWorlds.Library.Vec2)) return "Vec2";
        if (type == typeof(TaleWorlds.Library.Color)) return "Color";
        if (type.FullName == "System.Numerics.Vector2") return "Vector2";
        return null;
    }

    /// <summary>Clears cached IL analysis results and method lists. Intended for testing environments.</summary>
    public static void ClearCache()
    {
        Cache.Clear();
        Methods.Clear();
    }
}
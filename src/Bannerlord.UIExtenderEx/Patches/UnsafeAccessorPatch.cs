using Bannerlord.UIExtenderEx.Attributes;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Bannerlord.UIExtenderEx.Patches;

/// <summary>
/// Transpiles <see cref="BUTRUnsafeAccessorAttribute"/> stub methods to emit direct IL instructions accessing non-public
/// target methods or fields.
/// <para>
/// When an assembly registers, candidate stubs are resolved and patched via Harmony DynamicMethods that bypass CLR
/// visibility checks. Stubs that cannot be resolved retain their placeholder bodies and log diagnostic warnings.
/// </para>
/// </summary>
internal static class UnsafeAccessorPatch
{
    private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>Maps resolved stub methods to their underlying target <see cref="MemberInfo"/> definitions consumed during IL transpilation.</summary>
    private static readonly ConcurrentDictionary<MethodBase, MemberInfo> Targets = new();

    /// <summary>Tracks processed accessor stubs to prevent redundant reflection lookups and duplicate diagnostic logging.</summary>
    private static readonly ConcurrentDictionary<MethodBase, bool> Seen = new();

    /// <summary>Discovers, resolves, and patches all <see cref="BUTRUnsafeAccessorAttribute"/> stubs declared within the specified types.</summary>
    public static void Register(Harmony harmony, string moduleName, IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            MethodInfo[] methods;
            try
            {
                methods = type.GetMethods(Declared);
            }
            catch (TypeLoadException)
            {
                continue;
            }

            foreach (var stub in methods)
            {
                if (stub.GetCustomAttribute<BUTRUnsafeAccessorAttribute>() is not { } attribute || !Seen.TryAdd(stub, true))
                    continue;

                if (Resolve(stub, attribute, out var why) is not { } target)
                {
                    Trace.TraceWarning("UIExtenderEx: {0}: accessor {1}.{2} is not resolved: {3}. Calling it throws.", moduleName, type.FullName, stub.Name, why);
                    continue;
                }
                // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
                if ((stub.MethodImplementationFlags & MethodImplAttributes.NoInlining) == 0)
                {
                    Trace.TraceWarning("UIExtenderEx: {0}: accessor {1}.{2} is not marked [MethodImpl(MethodImplOptions.NoInlining)]; a caller compiled before it was resolved could keep its original body.",
                        moduleName, type.FullName, stub.Name);
                }

                Targets[stub] = target;
                if (!harmony.TryPatch(stub, transpiler: AccessTools2.DeclaredMethod(typeof(UnsafeAccessorPatch), nameof(Transpiler))))
                    Trace.TraceWarning("UIExtenderEx: {0}: accessor {1}.{2} could not be patched. Calling it throws.", moduleName, type.FullName, stub.Name);
            }
        }
    }

    /// <summary>
    /// The member a stub names, or null with the reason. Instance members are looked up from the type of the stub's first
    /// parameter up through its base types, static ones on the type the attribute names; both by name, parameter types and
    /// return type, whatever their accessibility.
    /// </summary>
    internal static MemberInfo? Resolve(MethodInfo stub, BUTRUnsafeAccessorAttribute attribute, out string why)
    {
        why = "";
        if (!stub.IsStatic)
        {
            why = "the stub has to be static";
            return null;
        }

        var name = attribute.Name ?? stub.Name;
        var parameters = stub.GetParameters().Select(x => x.ParameterType).ToArray();
        switch (attribute.Kind)
        {
            case BUTRAccessorKind.Method:
            case BUTRAccessorKind.StaticMethod:
            {
                var isStatic = attribute.Kind == BUTRAccessorKind.StaticMethod;
                if (!TryGetOwner(attribute, parameters, isStatic, out var owner, out why))
                    return null;
                var arguments = isStatic ? parameters : parameters.Skip(1).ToArray();
                var method = AccessTools.Method(owner, name, arguments);
                if (method is null || method.IsStatic != isStatic)
                {
                    why = $"{owner.FullName} has no {(isStatic ? "static" : "instance")} method {name}({string.Join(", ", arguments.Select(x => x.Name))})";
                    return null;
                }
                if (method.ReturnType != stub.ReturnType)
                {
                    why = $"{owner.FullName}.{name} returns {method.ReturnType.Name}, the stub {stub.ReturnType.Name}";
                    return null;
                }
                return method;
            }

            case BUTRAccessorKind.Field:
            case BUTRAccessorKind.StaticField:
            {
                var isStatic = attribute.Kind == BUTRAccessorKind.StaticField;
                if (parameters.Length != (isStatic ? 0 : 1))
                {
                    why = isStatic ? "a static field's stub takes no parameters" : "a field's stub takes the instance and nothing else";
                    return null;
                }
                if (!TryGetOwner(attribute, parameters, isStatic, out var owner, out why))
                    return null;
                var field = AccessTools.Field(owner, name);
                if (field is null || field.IsStatic != isStatic)
                {
                    why = $"{owner.FullName} has no {(isStatic ? "static" : "instance")} field {name}";
                    return null;
                }
                if (!stub.ReturnType.IsByRef || stub.ReturnType.GetElementType() != field.FieldType)
                {
                    why = $"the stub has to return ref {field.FieldType.Name}";
                    return null;
                }
                return field;
            }

            default:
                why = $"unknown kind {attribute.Kind}";
                return null;
        }
    }

    private static bool TryGetOwner(BUTRUnsafeAccessorAttribute attribute, Type[] parameters, bool isStatic, out Type owner, out string why)
    {
        why = "";
        owner = null!;
        if (isStatic)
        {
            if (attribute.Type is null)
            {
                why = "a static member's stub names the type on the attribute: [BUTRUnsafeAccessor(kind, typeof(TheType))]";
                return false;
            }
            owner = attribute.Type;
            return true;
        }
        if (parameters.Length == 0 || parameters[0].IsByRef || parameters[0].IsValueType)
        {
            why = "an instance member's stub takes the instance, of a class, as its first parameter";
            return false;
        }
        owner = parameters[0];
        return true;
    }

    /// <summary>The stub's whole body, replaced: its arguments passed straight on, or the field's address returned.</summary>
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        if (!Targets.TryGetValue(original, out var target))
        {
            foreach (var instruction in instructions)
                yield return instruction;
            yield break;
        }

        switch (target)
        {
            case MethodInfo method:
                for (var i = 0; i < original.GetParameters().Length; i++)
                    yield return new CodeInstruction(OpCodes.Ldarg, i);
                yield return new CodeInstruction(method.IsStatic || !method.IsVirtual ? OpCodes.Call : OpCodes.Callvirt, method);
                break;
            case FieldInfo { IsStatic: true } field:
                yield return new CodeInstruction(OpCodes.Ldsflda, field);
                break;
            case FieldInfo field:
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldflda, field);
                break;
        }
        yield return new CodeInstruction(OpCodes.Ret);
    }
}
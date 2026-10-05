using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Databinding;

using System;
using System.Collections.Generic;
using System.Reflection;

using TaleWorlds.Library.CodeGeneration;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets;

/// <summary>
/// Defines type conversion rules matching reflection binding semantics when assigning source values to widget properties.
/// <para>
/// Replicates the numeric widening conversions and reference assignability rules performed by the TaleWorlds XML loader
/// binder prior to setter invocation, enabling generated code to emit direct typed assignments without reflection.
/// </para>
/// </summary>
internal static class WidgetAssignmentConversion
{
    private static readonly Dictionary<Type, Type[]> WideningConversions = new()
    {
        [typeof(sbyte)] = [typeof(short), typeof(int), typeof(long), typeof(float), typeof(double)],
        [typeof(byte)] = [typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double)],
        [typeof(short)] = [typeof(int), typeof(long), typeof(float), typeof(double)],
        [typeof(ushort)] = [typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double)],
        [typeof(char)] = [typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double)],
        [typeof(int)] = [typeof(long), typeof(float), typeof(double)],
        [typeof(uint)] = [typeof(long), typeof(ulong), typeof(float), typeof(double)],
        [typeof(long)] = [typeof(float), typeof(double)],
        [typeof(ulong)] = [typeof(float), typeof(double)],
        [typeof(float)] = [typeof(double)],
    };

    /// <summary>
    /// Generates the right-hand side C# assignment expression converting <paramref name="sourceExpression"/> from <paramref name="sourceType"/> to <paramref name="targetType"/>.
    /// </summary>
    /// <param name="sourceExpression">The source C# expression evaluating to the bound value.</param>
    /// <param name="sourceType">The CLR type of the source value.</param>
    /// <param name="targetType">The destination widget property CLR type.</param>
    /// <returns>A formatted C# assignment expression if assignable; otherwise, <see langword="null"/>.</returns>
    public static string? GetAssignedExpression(string sourceExpression, Type sourceType, Type targetType)
    {
        if (sourceType == targetType)
        {
            return sourceExpression;
        }
        if (IsAssignable(sourceType, targetType))
        {
            return $"({ViewModelMemberResolution.GetCodeTypeName(targetType)}){sourceExpression}";
        }

        return null;
    }

    /// <summary>
    /// Determines whether a value of <paramref name="sourceType"/> can be assigned to a property of <paramref name="targetType"/>
    /// under Gauntlet binder widening and reference conversion rules.
    /// </summary>
    /// <param name="sourceType">The candidate source type.</param>
    /// <param name="targetType">The target property parameter type.</param>
    /// <returns><see langword="true"/> if the value can be assigned directly via typed C#; otherwise, <see langword="false"/>.</returns>
    public static bool IsAssignable(Type sourceType, Type targetType)
    {
        if (sourceType == targetType)
        {
            return true;
        }
        // The binder unwraps an enum to its underlying type
        var effectiveSourceType = sourceType.IsEnum ? Enum.GetUnderlyingType(sourceType) : sourceType;
        if (effectiveSourceType == targetType || IsWideningConversion(effectiveSourceType, targetType))
        {
            return true;
        }
        // A reference conversion up the hierarchy, which reflection makes without converting anything. Value types are
        // excluded on both sides: boxing into object and unboxing back out are not what a typed accessor would do.
        return !sourceType.IsValueType && !targetType.IsValueType && targetType.IsAssignableFrom(sourceType);
    }

    private static bool IsWideningConversion(Type sourceType, Type targetType) =>
        WideningConversions.TryGetValue(sourceType, out var targets) && Array.IndexOf(targets, targetType) >= 0;
}

/// <summary>
/// Emits optimized, direct C# property assignment statements for by-name databindings when types and members permit.
/// <para>
/// Replaces slow runtime reflection calls (<c>WidgetExtensions.SetWidgetAttribute</c>) with guarded direct property
/// assignments where the target widget property has a single-parameter public setter and compatible type.
/// Wraps invoked setters in <see cref="TargetInvocationException"/> handling to preserve exact reflection exception semantics.
/// </para>
/// </summary>
internal static class WidgetFastAssignment
{
    /// <summary>
    /// Evaluates whether a property binding qualifies for fast direct C# assignment instead of dynamic reflection.
    /// </summary>
    /// <param name="binding">The property binding to evaluate.</param>
    /// <returns><see langword="true"/> if the binding meets single-segment, public setter, and nameable type criteria; otherwise, <see langword="false"/>.</returns>
    public static bool IsEligible(PropertyBinding binding)
    {
        if (binding.WidgetProperty is not { } property || binding.WidgetPropertyType is not { } propertyType)
        {
            return false;
        }
        if (binding.Property.IndexOf('.') >= 0)
        {
            return false;
        }
        if (property.GetIndexParameters().Length != 0)
        {
            return false;
        }
        if (property.GetSetMethod() is not { } setter || setter.GetParameters().Length != 1)
        {
            return false;
        }
        return IsNameableTarget(propertyType);
    }

    /// <summary>
    /// Verifies that a type can be resolved and referenced by name in generated C# source code.
    /// </summary>
    private static bool IsNameableTarget(Type type)
    {
        if (type.IsPointer || type.IsByRef || type.ContainsGenericParameters || Nullable.GetUnderlyingType(type) is not null)
        {
            return false;
        }
        if (!type.IsPublic && !type.IsNestedPublic)
        {
            return false;
        }
        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                if (!IsNameableTarget(argument))
                {
                    return false;
                }
            }
        }
        return !string.IsNullOrEmpty(ViewModelMemberResolution.GetCodeTypeName(type));
    }

    /// <summary>
    /// Emits a direct typed assignment from a strongly-typed notification payload, falling back to <paramref name="fallbackLine"/> if incompatible.
    /// </summary>
    public static void AddFromTypedValue(MethodCode methodCode, PropertyBinding binding, string widgetVariable, string valueExpression, Type valueType, string fallbackLine)
    {
        if (IsEligible(binding)
            && WidgetAssignmentConversion.GetAssignedExpression(valueExpression, valueType, binding.WidgetPropertyType!) is { } assigned)
        {
            AddProtectedAssignment(methodCode, binding, widgetVariable, assigned);
            return;
        }
        methodCode.AddLine(fallbackLine);
    }

    /// <summary>
    /// Emits a pattern-matching type check and guarded assignment for an untyped or object-boxed value, falling back to <paramref name="fallbackLine"/> if unmatched.
    /// </summary>
    public static void AddFromUntypedValue(MethodCode methodCode, PropertyBinding binding, string widgetVariable, string valueExpression, string fallbackLine)
    {
        if (!IsEligible(binding))
        {
            methodCode.AddLine(fallbackLine);
            return;
        }

        var typeName = ViewModelMemberResolution.GetCodeTypeName(binding.WidgetPropertyType!);
        var matched = AssignedLocal(binding, widgetVariable);
        methodCode.AddLine($"if ({valueExpression} is {typeName} {matched})");
        methodCode.AddLine("{");
        AddProtectedAssignment(methodCode, binding, widgetVariable, matched);
        methodCode.AddLine("}");
        methodCode.AddLine("else");
        methodCode.AddLine("{");
        methodCode.AddLine(fallbackLine);
        methodCode.AddLine("}");
    }

    /// <summary>
    /// Emits an assignment wrapped in a try/catch block that rethrows setter exceptions as <see cref="TargetInvocationException"/> to mirror reflection behavior.
    /// </summary>
    private static void AddProtectedAssignment(MethodCode methodCode, PropertyBinding binding, string widgetVariable, string assignedExpression)
    {
        var inOwnBlock = !IsIdentifier(assignedExpression);
        var valueLocal = inOwnBlock ? AssignedLocal(binding, widgetVariable) : assignedExpression;
        var errorLocal = "uiExtenderExSetterFailure_" + widgetVariable + "_" + binding.Property;
        if (inOwnBlock)
        {
            methodCode.AddLine("{");
            methodCode.AddLine($"{ViewModelMemberResolution.GetCodeTypeName(binding.WidgetPropertyType!)} {valueLocal} = {assignedExpression};");
        }
        methodCode.AddLine("try");
        methodCode.AddLine("{");
        methodCode.AddLine($"{widgetVariable}.{binding.Property} = {valueLocal};");
        methodCode.AddLine("}");
        methodCode.AddLine($"catch (global::System.Exception {errorLocal})");
        methodCode.AddLine("{");
        // The wrapper reflection would have put on, so the two paths raise the same thing
        methodCode.AddLine($"throw new global::System.Reflection.TargetInvocationException({errorLocal});");
        methodCode.AddLine("}");
        if (inOwnBlock)
        {
            methodCode.AddLine("}");
        }
    }

    /// <summary>Generates a unique local variable name for storing the assigned typed value for a widget property.</summary>
    private static string AssignedLocal(PropertyBinding binding, string widgetVariable) =>
        "uiExtenderExAssigned_" + widgetVariable + "_" + binding.Property;

    /// <summary>
    /// Determines whether the specified C# expression is a simple identifier requiring no intermediate temporary local variable.
    /// </summary>
    private static bool IsIdentifier(string expression)
    {
        if (expression.Length == 0 || (!char.IsLetter(expression[0]) && expression[0] != '_'))
        {
            return false;
        }
        foreach (var character in expression)
        {
            if (!char.IsLetterOrDigit(character) && character != '_')
            {
                return false;
            }
        }
        return true;
    }
}
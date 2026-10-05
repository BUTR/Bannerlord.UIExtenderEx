using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.CSharp;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

using TaleWorlds.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets;

/// <summary>
/// Converts string-based attribute values into strongly-typed C# expressions and runtime conversions,
/// replicating the conversion rules of <c>WidgetExtensions.SetWidgetAttributeFromString</c>.
/// <para>
/// In TaleWorlds Gauntlet XML prefabs, string attributes undergo type-specific parsing inside guarded try/catch blocks.
/// If parsing fails, the original property value remains unmodified and an assertion error is logged.
/// This class precomputes identical conversions at compile time for string, int, float, bool, enum, Brush, Sprite,
/// and Color values, ensuring generated C# code exhibits identical runtime semantics and error behavior.
/// </para>
/// </summary>
internal static class LoaderStringConversion
{
    /// <summary>Matches floating-point literal strings suitable for C# float literals when appended with 'f'.</summary>
    private static readonly Regex PlainFloat = new(@"^-?(?:\d+(?:\.\d+)?|\.\d+)(?:[eE][+-]?\d+)?$", RegexOptions.CultureInvariant);

    /// <summary>
    /// Attempts to convert a string-encoded attribute value into a compile-time C# literal or factory call for the specified property type.
    /// </summary>
    /// <param name="type">The destination property type.</param>
    /// <param name="value">The raw string value from the prefab XML.</param>
    /// <param name="expression">When successful, receives the generated C# expression string; otherwise, <see langword="null"/>.</param>
    /// <param name="failure">When parsing throws an exception, receives the exception message to emulate the Gauntlet assertion failure.</param>
    /// <returns><see langword="true"/> if the type is handled by the converter (even if parsing failed with <paramref name="failure"/>); otherwise, <see langword="false"/> for unsupported types.</returns>
    public static bool TryGetLiteral(Type type, string value, out string? expression, out string? failure)
    {
        expression = null;
        failure = null;
        try
        {
            if (type == typeof(int))
            {
                expression = Convert.ToInt32(value).ToString(CultureInfo.InvariantCulture);
                return true;
            }
            if (type == typeof(float))
            {
                var number = Convert.ToSingle(value, CultureInfo.InvariantCulture);
                expression = PlainFloat.IsMatch(value) && !float.IsInfinity(number) && float.Parse(value, CultureInfo.InvariantCulture) == number
                    ? value + "f"
                    : GeneratedLiteral.Float(number);
                return true;
            }
            if (type == typeof(bool))
            {
                expression = value == "true" ? "true" : "false";
                return true;
            }
            if (type == typeof(string))
            {
                expression = GeneratedLiteral.String(value);
                return true;
            }
            if (type == typeof(Brush))
            {
                expression = $"this.Context.GetBrush({GeneratedLiteral.String(value)})";
                return true;
            }
            if (type == typeof(Sprite))
            {
                expression = $"this.Context.SpriteData.GetSprite({GeneratedLiteral.String(value)})";
                return true;
            }
            if (type.IsEnum)
            {
                expression = GetEnumLiteral(type, value, Enum.Parse(type, value));
                return true;
            }
            if (type == typeof(Color))
            {
                var color = Color.ConvertStringToColor(value);
                // Invariant, for the same reason the visual definitions are
                expression = $"new global::TaleWorlds.Library.Color({GeneratedLiteral.Float(color.Red)}, {GeneratedLiteral.Float(color.Green)}, {GeneratedLiteral.Float(color.Blue)}, {GeneratedLiteral.Float(color.Alpha)})";
                return true;
            }
        }
        catch (Exception e)
        {
            expression = null;
            failure = e.Message;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Converts an enum value into a C# literal expression, formatting known names directly and parsing numerical values into explicit casts.
    /// </summary>
    private static string GetEnumLiteral(Type type, string value, object parsed)
    {
        var typeName = ViewModelMemberResolution.GetCodeTypeName(type);
        if (Array.IndexOf(Enum.GetNames(type), value) >= 0)
        {
            return $"{typeName}.{value}";
        }
        var underlying = Convert.ChangeType(parsed, Enum.GetUnderlyingType(type), CultureInfo.InvariantCulture);
        var number = ((IFormattable) underlying).ToString(null, CultureInfo.InvariantCulture);
        return $"({typeName}){(number.StartsWith("-", StringComparison.Ordinal) ? $"({number})" : number)}";
    }

    /// <summary>
    /// Holds the fully-qualified runtime type name for emitting attribute failure assertions.
    /// </summary>
    public const string LoaderAssertsType = "global::Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime.LoaderAsserts";

    /// <summary>
    /// Generates a C# statement emitting a Gauntlet-compatible assertion failure for an unassigned attribute.
    /// </summary>
    /// <param name="targetExpression">The C# expression identifying the target widget instance.</param>
    /// <param name="propertyName">The name of the attribute property.</param>
    /// <param name="value">The raw attribute string value.</param>
    /// <param name="failure">The error description or exception message.</param>
    /// <returns>A formatted C# statement string.</returns>
    public static string AssertLine(string targetExpression, string propertyName, string value, string failure) =>
        $"{LoaderAssertsType}.AttributeNotSet({targetExpression}, {GeneratedLiteral.Regular(propertyName)}, {GeneratedLiteral.Regular(value)}, {GeneratedLiteral.Regular(failure)});";

    /// <summary>
    /// Generates a C# statement emitting a Gauntlet-compatible assertion failure when a property setter throws an exception.
    /// </summary>
    /// <param name="targetExpression">The C# expression identifying the target widget instance.</param>
    /// <param name="propertyName">The name of the attribute property.</param>
    /// <param name="value">The raw attribute string value.</param>
    /// <returns>A formatted C# statement string.</returns>
    public static string SetterThrewLine(string targetExpression, string propertyName, string value) =>
        $"{LoaderAssertsType}.SetterThrew({targetExpression}, {GeneratedLiteral.Regular(propertyName)}, {GeneratedLiteral.Regular(value)});";

    /// <summary>
    /// Gets the failure message generated when attempting to invoke a null or missing property setter.
    /// </summary>
    public static string MissingSetterFailure => new NullReferenceException().Message;

    /// <summary>
    /// Computes the exact exception message produced by the current CLR when looking up a missing dictionary key.
    /// </summary>
    /// <param name="key">The key name used to reproduce the missing key exception.</param>
    /// <returns>The localized exception message text.</returns>
    public static string MissingKeyFailure(string key)
    {
        try
        {
            _ = new Dictionary<string, object>()[key];
            return "";
        }
        catch (KeyNotFoundException e)
        {
            return e.Message;
        }
    }

    /// <summary>
    /// Generates a runtime C# conversion expression for a dynamic string expression evaluated at runtime.
    /// </summary>
    /// <param name="type">The destination property type.</param>
    /// <param name="valueExpression">The C# expression evaluating to the source string value.</param>
    /// <param name="expression">When successful, receives the generated conversion expression; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the type supports runtime string conversion; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetRuntimeConversion(Type type, string valueExpression, out string? expression)
    {
        expression = null;
        if (type == typeof(int))
        {
            expression = $"global::System.Convert.ToInt32({valueExpression})";
        }
        else if (type == typeof(float))
        {
            expression = $"global::System.Convert.ToSingle({valueExpression}, global::System.Globalization.CultureInfo.InvariantCulture)";
        }
        else if (type == typeof(bool))
        {
            expression = $"({valueExpression} == \"true\")";
        }
        else if (type == typeof(string))
        {
            expression = valueExpression;
        }
        else if (type == typeof(Brush))
        {
            expression = $"this.Context.GetBrush({valueExpression})";
        }
        else if (type == typeof(Sprite))
        {
            expression = $"this.Context.SpriteData.GetSprite({valueExpression})";
        }
        else if (type.IsEnum)
        {
            var typeName = ViewModelMemberResolution.GetCodeTypeName(type);
            expression = $"({typeName}) global::System.Enum.Parse(typeof({typeName}), {valueExpression})";
        }
        else if (type == typeof(Color))
        {
            expression = $"global::TaleWorlds.Library.Color.ConvertStringToColor({valueExpression})";
        }
        return expression is not null;
    }
}
using System.Globalization;
using System.Text;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.CSharp;

/// <summary>
/// Formats XML attribute and element values as valid C# literals and escaped comment strings for code generation.
/// <para>
/// Handles escaping for string literals (verbatim and regular), control characters, culture-invariant floating-point numbers,
/// and negative integer casts to ensure generated source code compiles deterministically across all system locales.
/// </para>
/// </summary>
public static class GeneratedLiteral
{
    /// <summary>
    /// Formats <paramref name="value"/> as a C# verbatim string literal (<c>@"..."</c>), escaping internal double quotes.
    /// </summary>
    /// <param name="value">The string value to format.</param>
    /// <returns>A formatted verbatim string literal, or <c>"null"</c> if <paramref name="value"/> is <see langword="null"/>.</returns>
    public static string String(string? value)
    {
        if (value is null)
            return "null";

        var sb = new StringBuilder(value.Length + 4);
        sb.Append("@\"");
        foreach (var c in value)
        {
            if (c == '"')
                sb.Append('"');
            sb.Append(c);
        }
        sb.Append('"');
        return sb.ToString();
    }

    /// <summary>
    /// Formats <paramref name="value"/> as a standard regular C# string literal (<c>"..."</c>), escaping backslashes, quotes, control characters, and Unicode line breaks.
    /// </summary>
    /// <param name="value">The string value to format.</param>
    /// <returns>A formatted regular string literal, or <c>"null"</c> if <paramref name="value"/> is <see langword="null"/>.</returns>
    public static string Regular(string? value)
    {
        if (value is null)
            return "null";

        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\': sb.Append(@"\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append(@"\n"); break;
                case '\r': sb.Append(@"\r"); break;
                case '\t': sb.Append(@"\t"); break;
                case '\0': sb.Append(@"\0"); break;
                default:
                    if (c < ' ' || c == '\u0085' || c == '\u2028' || c == '\u2029')
                        sb.Append("\\u").Append(((int) c).ToString("X4", CultureInfo.InvariantCulture));
                    else
                        sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    /// <summary>
    /// Escapes line breaks and Unicode separator characters within a string for inclusion in a single-line C# comment.
    /// </summary>
    /// <param name="value">The text to escape.</param>
    /// <returns>A single-line escaped string safe for comments, or <c>"null"</c> if <paramref name="value"/> is <see langword="null"/>.</returns>
    public static string Comment(string? value)
    {
        if (value is null)
            return "null";

        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '\n': sb.Append(@"\n"); break;
                case '\r': sb.Append(@"\r"); break;
                case '\u0085':
                case '\u2028':
                case '\u2029':
                    sb.Append("\\u").Append(((int) c).ToString("X4", CultureInfo.InvariantCulture));
                    break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Formats a 32-bit floating-point number as an invariant C# literal string with round-trip precision.
    /// </summary>
    /// <param name="value">The float value to format.</param>
    /// <returns>The formatted float literal string with trailing 'f', or special float constants (<c>float.NaN</c>, etc.).</returns>
    public static string Float(float value)
    {
        if (float.IsNaN(value))
            return "float.NaN";
        if (float.IsPositiveInfinity(value))
            return "float.PositiveInfinity";
        if (float.IsNegativeInfinity(value))
            return "float.NegativeInfinity";
        return $"{value.ToString("R", CultureInfo.InvariantCulture)}f";
    }

    /// <summary>
    /// Formats a 64-bit integer invariantly, enclosing negative values in parentheses to ensure safe expression evaluation following casts.
    /// </summary>
    /// <param name="value">The integer value to format.</param>
    /// <returns>The formatted integer string.</returns>
    public static string Integer(long value) => value < 0
        ? $"({value.ToString(CultureInfo.InvariantCulture)})"
        : value.ToString(CultureInfo.InvariantCulture);
}
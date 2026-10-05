using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.CSharp;

/// <summary>
/// Converts XML identifiers, prefab names, and member paths into valid, collision-free C# identifier names.
/// </summary>
public static class GeneratedNaming
{
    /// <summary>Stores the set of C# language keywords requiring verbatim '@' prefixing when used as member identifiers.</summary>
    private static readonly HashSet<string> ReservedKeywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue",
        "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally",
        "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected",
        "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
        "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
        "using", "virtual", "void", "volatile", "while",
    };

    /// <summary>
    /// Escapes a member name with '@' if it matches a reserved C# keyword.
    /// </summary>
    /// <param name="name">The member name to evaluate.</param>
    /// <returns>The escaped or original member identifier.</returns>
    public static string Member(string name) => ReservedKeywords.Contains(name) ? "@" + name : name;

    /// <summary>
    /// Converts an arbitrary string or XML name into a sanitized C# identifier, replacing punctuation and invalid characters with underscores.
    /// </summary>
    /// <param name="name">The raw input name string.</param>
    /// <returns>A valid C# identifier string.</returns>
    public static string GetUsableName(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (c == '.')
                sb.Append('_');
            else if (c == '_' || char.IsLetterOrDigit(c))
                sb.Append(c);
            else
                sb.Append('_').Append(((int) c).ToString("X", CultureInfo.InvariantCulture)).Append('_');
        }
        if (sb.Length == 0 || char.IsDigit(sb[0]))
            sb.Insert(0, '_');
        return sb.ToString();
    }

    /// <summary>
    /// Generates a unique identifier within the specified scope, appending an incrementing numerical suffix if a collision occurs.
    /// </summary>
    /// <param name="candidate">The preferred identifier name.</param>
    /// <param name="taken">The tracking set of existing names in scope.</param>
    /// <returns>A unique, unused identifier name.</returns>
    public static string GetUniqueName(string candidate, HashSet<string> taken)
    {
        var name = candidate;
        for (var suffix = 2; !taken.Add(name); suffix++)
            name = $"{candidate}_{suffix.ToString(CultureInfo.InvariantCulture)}";
        return name;
    }
}
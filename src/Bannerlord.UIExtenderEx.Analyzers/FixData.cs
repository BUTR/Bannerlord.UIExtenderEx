using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers;

/// <summary>
/// What a report hands its code fix, in <see cref="Microsoft.CodeAnalysis.Diagnostic.Properties"/>: what the analyzer
/// already worked out and the fix could not, such as the names close to a misspelled one. The file is compiled into the
/// code fixes' assembly as well, so both read the same keys.
/// </summary>
internal static class FixData
{
    /// <summary>The names a misspelled one could have meant, closest first, separated by <see cref="Separator"/>.</summary>
    public const string Suggestions = "Suggestions";

    /// <summary>The text a suggestion replaces.</summary>
    public const string Replace = "Replace";

    /// <summary>Where <see cref="Replace"/> is written: <see cref="InSpan"/> or <see cref="AfterSpan"/>.</summary>
    public const string Where = "Where";

    /// <summary>Inside the reported span: the attribute's name, or a literal that is the value itself.</summary>
    public const string InSpan = "InSpan";

    /// <summary>In the value of the attribute whose name is the reported span.</summary>
    public const string AfterSpan = "AfterSpan";

    /// <summary>Which of the rule's cases the report is, for rules whose fix depends on it.</summary>
    public const string Reason = "Reason";

    public const string ReasonAbstract = "Abstract";
    public const string ReasonGeneric = "Generic";
    public const string ReasonNoConstructor = "NoConstructor";
    public const string ReasonNotPublic = "NotPublic";
    public const string ReasonStatic = "Static";
    public const string ReasonType = "Type";

    /// <summary>For UIX0017: the content attributes the member's type fits, separated by <see cref="Separator"/>.</summary>
    public const string Attributes = "Attributes";

    public const char Separator = ';';

    public static ImmutableDictionary<string, string?> Of(params (string Key, string? Value)[] entries) =>
        entries.Where(e => e.Value is not null).ToImmutableDictionary(e => e.Key, e => e.Value);

    public static string? Join(IEnumerable<string> values)
    {
        var joined = string.Join(Separator.ToString(), values);
        return joined.Length == 0 ? null : joined;
    }

    public static string[] Split(string? value) =>
        string.IsNullOrEmpty(value) ? [] : value!.Split(Separator);
}

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers;

/// <summary>
/// Encapsulates diagnostic property keys and metadata transmitted via <see cref="Microsoft.CodeAnalysis.Diagnostic.Properties"/>
/// from analyzers to code fix providers (such as fuzzy matching suggestions or diagnostic discrimination reasons).
/// </summary>
/// <remarks>
/// This file is compiled directly into both the analyzer and code fix provider assemblies to share property contract constants.
/// </remarks>
internal static class FixData
{
    /// <summary>Suggested symbol or attribute names ordered by Levenshtein distance, delimited by <see cref="Separator"/>.</summary>
    public const string Suggestions = "Suggestions";

    /// <summary>The target identifier text to be replaced by the code action.</summary>
    public const string Replace = "Replace";

    /// <summary>The relative replacement location: <see cref="InSpan"/> or <see cref="AfterSpan"/>.</summary>
    public const string Where = "Where";

    /// <summary>Indicates the replacement target is within the reported source span (such as an attribute name or string literal).</summary>
    public const string InSpan = "InSpan";

    /// <summary>Indicates the replacement target resides within the attribute value following the reported attribute name span.</summary>
    public const string AfterSpan = "AfterSpan";

    /// <summary>Differentiates diagnostic variation sub-reasons for rules supporting multiple code actions.</summary>
    public const string Reason = "Reason";

    public const string ReasonAbstract = "Abstract";
    public const string ReasonGeneric = "Generic";
    public const string ReasonNoConstructor = "NoConstructor";
    public const string ReasonNotPublic = "NotPublic";
    public const string ReasonStatic = "Static";
    public const string ReasonType = "Type";

    /// <summary>For <c>UIX0017</c>: compatible content attribute type short names delimited by <see cref="Separator"/>.</summary>
    public const string Attributes = "Attributes";

    /// <summary>For <c>UIX0024</c>: the underlying diagnostic rule ID triggered on a subset of supported game versions.</summary>
    public const string Rule = "Rule";

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
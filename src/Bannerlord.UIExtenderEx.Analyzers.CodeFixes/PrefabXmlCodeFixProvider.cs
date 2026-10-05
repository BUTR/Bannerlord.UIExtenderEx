using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;

namespace Bannerlord.UIExtenderEx.Analyzers.CodeFixes;

/// <summary>
/// Provides code fixes for Gauntlet XML diagnostics (<c>UIX0012</c> through <c>UIX0016</c> and multi-version variant <c>UIX0024</c>):
/// corrects misspelled widget attributes, enum/boolean values, prefab parameters, and ViewModel binding paths in both
/// standalone XML files and C# XML string literals.
/// </summary>
/// <remarks>
/// Targets either the attribute name itself or occurrences within the attribute value, as specified by diagnostic properties.
/// Diagnostics attached to raw string literals without granular span mapping omit fixes when multiple occurrences are ambiguous.
/// Multi-version diagnostics (<c>UIX0024</c>) offer fixes only when suggestions remain valid across all evaluated game versions.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PrefabXmlCodeFixProvider),
    DocumentKinds = new[] { nameof(TextDocumentKind.Document), nameof(TextDocumentKind.AdditionalDocument) }), Shared]
public sealed class PrefabXmlCodeFixProvider : CodeFixProvider
{
    private static readonly ImmutableHashSet<string> FixedForSomeVersions = ImmutableHashSet.Create("UIX0012", "UIX0013", "UIX0015");

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("UIX0012", "UIX0013", "UIX0014", "UIX0015", "UIX0016", "UIX0024");

    // Fix-all is not supported because each suggestion offers specific replacement text tailored to that diagnostic
    public override FixAllProvider? GetFixAllProvider() => null;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.TextDocument;
        var text = await document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);

        foreach (var diagnostic in context.Diagnostics)
        {
            var properties = diagnostic.Properties;
            if (diagnostic.Id == "UIX0024" && (!properties.TryGetValue(FixData.Rule, out var rule) || !FixedForSomeVersions.Contains(rule!)))
                continue;
            if (!properties.TryGetValue(FixData.Replace, out var replace) || string.IsNullOrEmpty(replace)
                || !properties.TryGetValue(FixData.Where, out var where))
            {
                continue;
            }
            var suggestions = FixData.Split(properties.TryGetValue(FixData.Suggestions, out var value) ? value : null);
            if (suggestions.Length == 0 || Find(text, diagnostic.Location.SourceSpan, replace!, where == FixData.InSpan) is not { } target)
                continue;

            foreach (var suggestion in suggestions)
            {
                context.RegisterCodeFix(CodeAction.Create(
                    $"Change to '{suggestion}'",
                    _ => Task.FromResult(Replace(document, text, target, suggestion)),
                    equivalenceKey: $"{diagnostic.Id}:{suggestion}"), diagnostic);
            }
        }
    }

    private static Solution Replace(TextDocument document, SourceText text, TextSpan target, string replacement)
    {
        var changed = text.WithChanges(new TextChange(target, replacement));
        return document is Document
            ? document.Project.Solution.WithDocumentText(document.Id, changed)
            : document.Project.Solution.WithAdditionalDocumentText(document.Id, changed);
    }

    /// <summary>
    /// Locates whole-word occurrences of <paramref name="word"/> within <paramref name="span"/> or immediately following
    /// it on the same line, resolving string literal matches only when single non-ambiguous occurrences exist.
    /// </summary>
    private static TextSpan? Find(SourceText text, TextSpan span, string word, bool inSpan)
    {
        if (span.IsEmpty || span.End > text.Length)
            return null;
        var isLiteral = text[span.Start] is '"' or '$' or '@';

        if (inSpan)
        {
            var found = Occurrences(text, span.Start, span.End, word);
            return found.Count == 1 || (found.Count > 1 && !isLiteral) ? found[0] : null;
        }
        if (isLiteral)
            return null;
        var lineEnd = text.Lines.GetLineFromPosition(span.End).End;
        var after = Occurrences(text, span.End, lineEnd, word);
        return after.Count > 0 ? after[0] : null;
    }

    private static List<TextSpan> Occurrences(SourceText text, int start, int end, string word)
    {
        var found = new List<TextSpan>();
        var content = text.ToString(TextSpan.FromBounds(start, end));
        for (var index = content.IndexOf(word, System.StringComparison.Ordinal); index >= 0; index = content.IndexOf(word, index + 1, System.StringComparison.Ordinal))
        {
            var before = index > 0 ? content[index - 1] : ' ';
            var next = index + word.Length < content.Length ? content[index + word.Length] : ' ';
            if (!IsNameChar(before) && !IsNameChar(next))
                found.Add(new TextSpan(start + index, word.Length));
        }
        return found;
    }

    private static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
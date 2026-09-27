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
/// UIX0012 to UIX0016: a misspelled attribute, value, parameter or binding replaced by one of the names the analyzer
/// found close to it. The XML is a prefab file or a string literal in a patch, so the fix edits either.
/// <para>
/// The report points at the attribute's name. The text to replace is that name, or is in the attribute's value after it,
/// as the report says; a report the analyzer could only place on a whole literal gets no fix, as the text in it cannot be
/// told apart from the same text elsewhere in the literal.
/// </para>
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PrefabXmlCodeFixProvider),
    DocumentKinds = new[] { nameof(TextDocumentKind.Document), nameof(TextDocumentKind.AdditionalDocument) }), Shared]
public sealed class PrefabXmlCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("UIX0012", "UIX0013", "UIX0014", "UIX0015", "UIX0016");

    // Each fix names the text it puts in, so fixing all at once has nothing to apply everywhere
    public override FixAllProvider? GetFixAllProvider() => null;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.TextDocument;
        var text = await document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);

        foreach (var diagnostic in context.Diagnostics)
        {
            var properties = diagnostic.Properties;
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
    /// Where <paramref name="word"/> is written, as a whole word: in the span, or after it on the same line. In a span that
    /// is a literal, only when the word is there once.
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

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using System.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers.CodeFixes;

/// <summary>
/// Common syntax manipulation and symbol inspection helpers shared across analyzer code fix providers.
/// </summary>
internal static class Fixes
{
    /// <summary>
    /// Detects the line break sequence utilized by the source document to match surrounding indentation and line endings.
    /// </summary>
    public static string EndOfLine(SourceText text)
    {
        var line = text.Lines.FirstOrDefault(l => l.EndIncludingLineBreak > l.End);
        return line.EndIncludingLineBreak - line.End == 2 ? "\r\n" : "\n";
    }

    /// <summary>
    /// Retrieves the leading whitespace trivia preceding the first token on the line containing the specified syntax node.
    /// </summary>
    public static string IndentationOf(SyntaxNode node)
    {
        var trivia = node.GetLeadingTrivia().LastOrDefault();
        return trivia.IsKind(SyntaxKind.WhitespaceTrivia) ? trivia.ToString() : "";
    }

    /// <summary>
    /// Adjusts member accessibility modifiers to <c>public</c>, replacing existing accessibility keywords or inserting
    /// <c>public</c> when modifiers are absent.
    /// </summary>
    public static MemberDeclarationSyntax MakePublic(MemberDeclarationSyntax member)
    {
        var modifiers = member.Modifiers;
        if (modifiers.Any(IsAccessibility))
        {
            var first = modifiers.First(IsAccessibility);
            var result = modifiers
                .Where(t => t == first || !IsAccessibility(t))
                .Select(t => t == first ? SyntaxFactory.Token(first.LeadingTrivia, SyntaxKind.PublicKeyword, first.TrailingTrivia) : t);
            return member.WithModifiers(SyntaxFactory.TokenList(result));
        }

        // Moves leading trivia from the post-attribute token onto the newly inserted public modifier
        var start = member.AttributeLists.Count > 0 ? member.AttributeLists.Last().Span.End : member.SpanStart;
        var next = member.DescendantTokens().First(t => t.SpanStart >= start);
        var keyword = SyntaxFactory.Token(next.LeadingTrivia, SyntaxKind.PublicKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space));
        member = member.ReplaceToken(next, next.WithLeadingTrivia(SyntaxTriviaList.Empty));
        return member.WithModifiers(member.Modifiers.Insert(0, keyword));
    }

    private static bool IsAccessibility(SyntaxToken token) => token.Kind() is
        SyntaxKind.PublicKeyword or SyntaxKind.PrivateKeyword or SyntaxKind.ProtectedKeyword or SyntaxKind.InternalKeyword;

    /// <summary>
    /// Resolves the generic type argument <c>TViewModel</c> from the mixin's base type hierarchy implementing
    /// <c>BaseViewModelMixin&lt;TViewModel&gt;</c>.
    /// </summary>
    public static INamedTypeSymbol? ViewModelOf(INamedTypeSymbol mixin)
    {
        for (var type = mixin.BaseType; type is not null; type = type.BaseType)
        {
            if (type.OriginalDefinition.ToDisplayString() == "Bannerlord.UIExtenderEx.ViewModels.BaseViewModelMixin<TViewModel>")
                return type.TypeArguments[0] as INamedTypeSymbol;
        }
        return null;
    }
}
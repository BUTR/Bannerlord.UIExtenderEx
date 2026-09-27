using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using System.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers.CodeFixes;

/// <summary>What more than one fix needs.</summary>
internal static class Fixes
{
    /// <summary>The line break the document uses, so an inserted line matches the lines around it.</summary>
    public static string EndOfLine(SourceText text)
    {
        var line = text.Lines.FirstOrDefault(l => l.EndIncludingLineBreak > l.End);
        return line.EndIncludingLineBreak - line.End == 2 ? "\r\n" : "\n";
    }

    /// <summary>The whitespace before a node's first token on its line.</summary>
    public static string IndentationOf(SyntaxNode node)
    {
        var trivia = node.GetLeadingTrivia().LastOrDefault();
        return trivia.IsKind(SyntaxKind.WhitespaceTrivia) ? trivia.ToString() : "";
    }

    /// <summary>
    /// The member with its accessibility changed to public: every accessibility keyword it has replaced by one
    /// <c>public</c>, in the place of the first, or put in front when it had none.
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

        // No modifier: the leading trivia of the token after the attributes moves onto the new keyword
        var start = member.AttributeLists.Count > 0 ? member.AttributeLists.Last().Span.End : member.SpanStart;
        var next = member.DescendantTokens().First(t => t.SpanStart >= start);
        var keyword = SyntaxFactory.Token(next.LeadingTrivia, SyntaxKind.PublicKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space));
        member = member.ReplaceToken(next, next.WithLeadingTrivia(SyntaxTriviaList.Empty));
        return member.WithModifiers(member.Modifiers.Insert(0, keyword));
    }

    private static bool IsAccessibility(SyntaxToken token) => token.Kind() is
        SyntaxKind.PublicKeyword or SyntaxKind.PrivateKeyword or SyntaxKind.ProtectedKeyword or SyntaxKind.InternalKeyword;

    /// <summary>The type argument of <c>BaseViewModelMixin&lt;TViewModel&gt;</c> among the type's base types.</summary>
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

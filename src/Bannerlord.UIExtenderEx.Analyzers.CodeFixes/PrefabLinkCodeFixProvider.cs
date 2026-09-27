using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;

namespace Bannerlord.UIExtenderEx.Analyzers.CodeFixes;

/// <summary>UIX0019: the mixin taken out of a <c>[PrefabLink]</c>, which then links the XML to the ViewModel alone.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PrefabLinkCodeFixProvider)), Shared]
public sealed class PrefabLinkCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("UIX0019");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        foreach (var diagnostic in context.Diagnostics)
        {
            // The report is on the third argument, the mixin
            var argument = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true).FirstAncestorOrSelf<AttributeArgumentSyntax>();
            if (argument?.Parent is not AttributeArgumentListSyntax list || list.Arguments.IndexOf(argument) != 2)
                continue;
            context.RegisterCodeFix(CodeAction.Create(
                "Link to the ViewModel alone",
                _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(list, list.WithArguments(list.Arguments.Remove(argument))))),
                equivalenceKey: "UIX0019"), diagnostic);
        }
    }
}

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;

namespace Bannerlord.UIExtenderEx.Analyzers.CodeFixes;

/// <summary>
/// Provides code fixes for unused linked mixin diagnostics (<c>UIX0019</c>), removing the mixin type argument from
/// <c>[assembly: PrefabLink]</c> so the prefab links directly to the ViewModel alone.
/// </summary>
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
            // The diagnostic is reported on the third attribute argument representing the mixin type
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
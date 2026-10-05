using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Bannerlord.UIExtenderEx.Analyzers.CodeFixes;

/// <summary>
/// Provides code fixes for prefab patch content members (<c>UIX0017</c>): changes member accessibility to public,
/// converts static members to instance members, or switches the extension attribute to one matching the member's return type.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PrefabContentCodeFixProvider)), Shared]
public sealed class PrefabContentCodeFixProvider : CodeFixProvider
{
    private static readonly ImmutableHashSet<string> ContentAttributes = ImmutableHashSet.Create(
        "PrefabExtensionFileName", "PrefabExtensionText", "PrefabExtensionXmlNode", "PrefabExtensionXmlNodes", "PrefabExtensionXmlDocument");

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("UIX0017");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        foreach (var diagnostic in context.Diagnostics)
        {
            var member = root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<MemberDeclarationSyntax>();
            if (member is not (PropertyDeclarationSyntax or MethodDeclarationSyntax))
                continue;
            var name = member is PropertyDeclarationSyntax property ? property.Identifier.ValueText : ((MethodDeclarationSyntax) member).Identifier.ValueText;
            var reason = diagnostic.Properties.TryGetValue(FixData.Reason, out var value) ? value : null;

            switch (reason)
            {
                case FixData.ReasonNotPublic:
                    context.RegisterCodeFix(CodeAction.Create(
                        $"Make '{name}' public",
                        _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(member, Fixes.MakePublic(member)))),
                        equivalenceKey: "UIX0017:Public"), diagnostic);
                    break;

                case FixData.ReasonStatic:
                    var @static = member.Modifiers.First(m => m.IsKind(SyntaxKind.StaticKeyword));
                    var instance = member.WithModifiers(member.Modifiers.Remove(@static));
                    context.RegisterCodeFix(CodeAction.Create(
                        $"Make '{name}' an instance member",
                        _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(member, instance))),
                        equivalenceKey: "UIX0017:Instance"), diagnostic);
                    break;

                case FixData.ReasonType:
                    if (member.AttributeLists.SelectMany(l => l.Attributes).FirstOrDefault(a => ContentAttributes.Contains(ShortName(a))) is not { } attribute)
                        break;
                    foreach (var fitting in FixData.Split(diagnostic.Properties.TryGetValue(FixData.Attributes, out var attributes) ? attributes : null))
                    {
                        var replaced = attribute.WithName(Rename(attribute.Name, fitting));
                        // Omits arguments because [PrefabExtensionXmlNodes] does not accept removeRootNode
                        if (fitting == "PrefabExtensionXmlNodes")
                            replaced = replaced.WithArgumentList(null);
                        context.RegisterCodeFix(CodeAction.Create(
                            $"Use [{fitting}]",
                            _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(attribute, replaced))),
                            equivalenceKey: "UIX0017:" + fitting), diagnostic);
                    }
                    break;
            }
        }
    }

    private static string ShortName(AttributeSyntax attribute)
    {
        var name = attribute.Name switch
        {
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
            SimpleNameSyntax simple => simple.Identifier.ValueText,
            _ => "",
        };
        return name.EndsWith("Attribute") ? name.Substring(0, name.Length - "Attribute".Length) : name;
    }

    /// <summary>
    /// Replaces the terminal identifier of an attribute name syntax while preserving namespace qualification and optional <c>Attribute</c> suffix conventions.
    /// </summary>
    private static NameSyntax Rename(NameSyntax name, string shortName)
    {
        SimpleNameSyntax Renamed(SimpleNameSyntax simple) => IdentifierName(Identifier(
            simple.Identifier.LeadingTrivia,
            simple.Identifier.ValueText.EndsWith("Attribute") ? shortName + "Attribute" : shortName,
            simple.Identifier.TrailingTrivia));

        return name switch
        {
            QualifiedNameSyntax qualified => qualified.WithRight(Renamed(qualified.Right)),
            AliasQualifiedNameSyntax alias => alias.WithName(Renamed(alias.Name)),
            SimpleNameSyntax simple => Renamed(simple),
            _ => name,
        };
    }
}
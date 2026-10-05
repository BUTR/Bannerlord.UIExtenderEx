using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Simplification;

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Bannerlord.UIExtenderEx.Analyzers.CodeFixes;

/// <summary>
/// Provides code fixes for unsafe accessor stubs (<c>UIX0010</c>), ensuring methods are annotated with
/// <c>[MethodImpl(MethodImplOptions.NoInlining)]</c> or combining <c>NoInlining</c> into existing method implementation flags.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnsafeAccessorCodeFixProvider)), Shared]
public sealed class UnsafeAccessorCodeFixProvider : CodeFixProvider
{
    private const string MethodImplAttribute = "System.Runtime.CompilerServices.MethodImplAttribute";
    private const string MethodImplOptions = "System.Runtime.CompilerServices.MethodImplOptions";
    private const int AggressiveInlining = 256;

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("UIX0010");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;
        foreach (var diagnostic in context.Diagnostics)
        {
            if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { } stub)
                continue;
            context.RegisterCodeFix(CodeAction.Create(
                "Mark the stub NoInlining",
                cancellation => AddNoInliningAsync(context.Document, stub, cancellation),
                equivalenceKey: "UIX0010"), diagnostic);
        }
    }

    private static async Task<Document> AddNoInliningAsync(Document document, MethodDeclarationSyntax stub, CancellationToken cancellation)
    {
        var root = await document.GetSyntaxRootAsync(cancellation).ConfigureAwait(false);
        var model = await document.GetSemanticModelAsync(cancellation).ConfigureAwait(false);
        if (root is null || model?.Compilation.GetTypeByMetadataName(MethodImplAttribute) is not { } attributeType
            || model.Compilation.GetTypeByMetadataName(MethodImplOptions) is not { } optionsType)
        {
            return document;
        }

        // Emits fully qualified type syntax, imports required namespaces when absent, and reduces syntax via Simplifier
        var added = new SyntaxAnnotation();
        var generator = SyntaxGenerator.GetGenerator(document);
        var noInlining = ((ExpressionSyntax) generator.MemberAccessExpression(generator.TypeExpression(optionsType), "NoInlining"))
            .WithAdditionalAnnotations(added, Simplifier.Annotation);

        var existing = stub.AttributeLists.SelectMany(l => l.Attributes).FirstOrDefault(a =>
            SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(a, cancellation).Symbol?.ContainingType, attributeType));
        if (existing?.ArgumentList?.Arguments.FirstOrDefault() is { NameEquals: null, NameColon: null } flags)
        {
            var asked = model.GetConstantValue(flags.Expression, cancellation).Value is int value ? value : 0;
            ExpressionSyntax combined = (asked & AggressiveInlining) != 0
                ? noInlining
                : BinaryExpression(SyntaxKind.BitwiseOrExpression, flags.Expression.WithoutTrivia(), noInlining);
            return await ShortenAsync(document.WithSyntaxRoot(root.ReplaceNode(flags.Expression, combined.WithTriviaFrom(flags.Expression))), added, cancellation).ConfigureAwait(false);
        }
        if (existing is not null)
        {
            var argument = AttributeArgument(noInlining);
            var list = existing.ArgumentList is null ? AttributeArgumentList(SingletonSeparatedList(argument)) : existing.ArgumentList.WithArguments(existing.ArgumentList.Arguments.Insert(0, argument));
            return await ShortenAsync(document.WithSyntaxRoot(root.ReplaceNode(existing, existing.WithArgumentList(list))), added, cancellation).ConfigureAwait(false);
        }

        var text = await document.GetTextAsync(cancellation).ConfigureAwait(false);
        var eol = Fixes.EndOfLine(text);
        var name = ((NameSyntax) generator.TypeExpression(attributeType)).WithAdditionalAnnotations(added, Simplifier.Annotation);
        var attributeList = AttributeList(SingletonSeparatedList(Attribute(name, AttributeArgumentList(SingletonSeparatedList(AttributeArgument(noInlining))))));

        MethodDeclarationSyntax marked;
        if (stub.AttributeLists.Count > 0)
        {
            var last = stub.AttributeLists.Last();
            attributeList = attributeList.WithLeadingTrivia(Whitespace(Fixes.IndentationOf(stub))).WithTrailingTrivia(EndOfLine(eol));
            marked = stub.WithAttributeLists(stub.AttributeLists.Insert(stub.AttributeLists.IndexOf(last) + 1, attributeList));
        }
        else
        {
            // Transfers method leading trivia (including doc comments) to precede the newly attached attribute list
            var leading = stub.GetLeadingTrivia();
            attributeList = attributeList.WithLeadingTrivia(leading).WithTrailingTrivia(EndOfLine(eol));
            marked = stub.WithoutLeadingTrivia().WithLeadingTrivia(Whitespace(Fixes.IndentationOf(stub))).WithAttributeLists(SingletonList(attributeList));
        }
        return await ShortenAsync(document.WithSyntaxRoot(root.ReplaceNode(stub, marked)), added, cancellation).ConfigureAwait(false);
    }

    private static async Task<Document> ShortenAsync(Document document, SyntaxAnnotation added, CancellationToken cancellation)
    {
        document = await ImportAdder.AddImportsAsync(document, added, cancellationToken: cancellation).ConfigureAwait(false);
        return await Simplifier.ReduceAsync(document, Simplifier.Annotation, cancellationToken: cancellation).ConfigureAwait(false);
    }
}
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Bannerlord.UIExtenderEx.Analyzers.CodeFixes;

/// <summary>
/// UIX0003: the refresh method name replaced by one the ViewModel has. UIX0004: <c>handleDerived: true</c>. UIX0006: the
/// constructor UIExtenderEx creates the mixin through. UIX0007: the marked member made public.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MixinCodeFixProvider)), Shared]
public sealed class MixinCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("UIX0003", "UIX0004", "UIX0006", "UIX0007");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            switch (diagnostic.Id)
            {
                case "UIX0003" when node.FirstAncestorOrSelf<AttributeSyntax>() is { } attribute:
                    await RegisterRefreshMethodFixes(context, diagnostic, attribute).ConfigureAwait(false);
                    break;
                case "UIX0004" when node.FirstAncestorOrSelf<ClassDeclarationSyntax>() is { } mixin:
                    await RegisterHandleDerivedFix(context, diagnostic, mixin).ConfigureAwait(false);
                    break;
                case "UIX0006" when node.FirstAncestorOrSelf<ClassDeclarationSyntax>() is { } mixin:
                    await RegisterCreationFix(context, diagnostic, mixin).ConfigureAwait(false);
                    break;
                case "UIX0007" when node.FirstAncestorOrSelf<MemberDeclarationSyntax>() is PropertyDeclarationSyntax or MethodDeclarationSyntax:
                    var member = node.FirstAncestorOrSelf<MemberDeclarationSyntax>()!;
                    context.RegisterCodeFix(CodeAction.Create(
                        $"Make '{NameOf(member)}' public",
                        _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(member, Fixes.MakePublic(member)))),
                        equivalenceKey: "UIX0007"), diagnostic);
                    break;
            }
        }
    }

    private static string NameOf(MemberDeclarationSyntax member) => member switch
    {
        PropertyDeclarationSyntax property => property.Identifier.ValueText,
        MethodDeclarationSyntax method => method.Identifier.ValueText,
        _ => "",
    };

    /// <summary>
    /// One fix per method the analyzer found close to the name: <c>nameof(TheViewModel.Method)</c> where the mixin can
    /// see the method, the name as a string where it cannot, a protected or private one.
    /// </summary>
    private static async Task RegisterRefreshMethodFixes(CodeFixContext context, Diagnostic diagnostic, AttributeSyntax attribute)
    {
        var suggestions = FixData.Split(diagnostic.Properties.TryGetValue(FixData.Suggestions, out var value) ? value : null);
        if (suggestions.Length == 0 || attribute.ArgumentList is null)
            return;
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (model is null)
            return;

        var argument = attribute.ArgumentList.Arguments.FirstOrDefault(a => model.GetConstantValue(a.Expression, context.CancellationToken).Value is string);
        if (argument is null)
            return;
        var viewModel = attribute.FirstAncestorOrSelf<ClassDeclarationSyntax>() is { } declaration
            && model.GetDeclaredSymbol(declaration, context.CancellationToken) is { } mixin
            ? Fixes.ViewModelOf(mixin)
            : null;

        foreach (var name in suggestions)
        {
            var visible = viewModel is not null && SelfAndBases(viewModel)
                .SelectMany(t => t.GetMembers(name).OfType<IMethodSymbol>())
                .Any(m => model.IsAccessible(argument.SpanStart, m));
            // Parsed rather than built: an identifier built as "nameof" is not the contextual keyword, and does not bind
            ExpressionSyntax replacement = visible
                ? ParseExpression($"nameof({viewModel!.ToMinimalDisplayString(model, argument.SpanStart)}.{name})")
                : LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(name));
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            context.RegisterCodeFix(CodeAction.Create(
                $"Use '{name}'",
                _ => Task.FromResult(context.Document.WithSyntaxRoot(root!.ReplaceNode(argument.Expression, replacement.WithTriviaFrom(argument.Expression)))),
                equivalenceKey: "UIX0003:" + name), diagnostic);
        }
    }

    /// <summary><c>handleDerived: true</c>: a <c>false</c> already written turned over, or the argument added.</summary>
    private static async Task RegisterHandleDerivedFix(CodeFixContext context, Diagnostic diagnostic, ClassDeclarationSyntax mixin)
    {
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (model is null || FindMixinAttribute(mixin, model, context.CancellationToken) is not { } attribute)
            return;
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        var @true = LiteralExpression(SyntaxKind.TrueLiteralExpression);
        var existing = attribute.ArgumentList?.Arguments.FirstOrDefault(a => model.GetConstantValue(a.Expression, context.CancellationToken).Value is bool);
        var fixedAttribute = existing is not null
            ? attribute.ReplaceNode(existing.Expression, @true.WithTriviaFrom(existing.Expression))
            : attribute.WithArgumentList((attribute.ArgumentList ?? AttributeArgumentList()).AddArguments(
                AttributeArgument(null, NameColon("handleDerived"), @true)));

        context.RegisterCodeFix(CodeAction.Create(
            "Set handleDerived: true",
            _ => Task.FromResult(context.Document.WithSyntaxRoot(root!.ReplaceNode(attribute, fixedAttribute))),
            equivalenceKey: "UIX0004"), diagnostic);
    }

    /// <summary>
    /// An abstract mixin made concrete; a constructor taking the ViewModel made public; or, when there is none, one
    /// added after the fields, handing the ViewModel to the base when the base takes it.
    /// </summary>
    private static async Task RegisterCreationFix(CodeFixContext context, Diagnostic diagnostic, ClassDeclarationSyntax mixin)
    {
        var reason = diagnostic.Properties.TryGetValue(FixData.Reason, out var value) ? value : null;
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || model is null || model.GetDeclaredSymbol(mixin, context.CancellationToken) is not { } type)
            return;

        if (reason == FixData.ReasonAbstract)
        {
            var @abstract = mixin.Modifiers.First(m => m.IsKind(SyntaxKind.AbstractKeyword));
            var concrete = mixin.WithModifiers(mixin.Modifiers.Remove(@abstract));
            if (concrete.Modifiers.Count == 0)
                concrete = concrete.WithKeyword(concrete.Keyword.WithLeadingTrivia(@abstract.LeadingTrivia));
            context.RegisterCodeFix(CodeAction.Create(
                $"Make '{type.Name}' not abstract",
                _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(mixin, concrete))),
                equivalenceKey: "UIX0006:Abstract"), diagnostic);
            return;
        }
        if (reason != FixData.ReasonNoConstructor || Fixes.ViewModelOf(type) is not { } viewModel)
            return;

        var compilation = model.Compilation;
        bool Accepts(IMethodSymbol constructor) => constructor.Parameters.Length == 1
            && compilation.ClassifyCommonConversion(viewModel, constructor.Parameters[0].Type) is { IsImplicit: true } conversion
            && (conversion.IsIdentity || conversion.IsReference);

        var hidden = type.InstanceConstructors.FirstOrDefault(c => Accepts(c) && !c.IsImplicitlyDeclared)?
            .DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(context.CancellationToken) as ConstructorDeclarationSyntax;
        if (hidden is not null && mixin.Members.Contains(hidden))
        {
            context.RegisterCodeFix(CodeAction.Create(
                "Make the constructor public",
                _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(hidden, Fixes.MakePublic(hidden)))),
                equivalenceKey: "UIX0006:Public"), diagnostic);
            return;
        }

        var text = await context.Document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);
        var eol = Fixes.EndOfLine(text);
        var fields = mixin.Members.TakeWhile(m => m is FieldDeclarationSyntax).Count();
        var indentation = mixin.Members.Count > 0 ? Fixes.IndentationOf(mixin.Members[0]) : Fixes.IndentationOf(mixin) + "    ";
        var inside = mixin.OpenBraceToken.Span.End;
        var passesViewModel = type.BaseType?.InstanceConstructors.Any(c => Accepts(c) && model.IsAccessible(inside, c)) == true;
        var viewModelName = viewModel.ToMinimalDisplayString(model, inside);
        // A blank line between the constructor and the fields before it, or the members after it
        var constructor = ParseMemberDeclaration($"public {type.Name}({viewModelName} vm){(passesViewModel ? " : base(vm)" : "")} {{ }}")!;
        constructor = fields > 0
            ? constructor.WithLeadingTrivia(EndOfLine(eol), Whitespace(indentation)).WithTrailingTrivia(EndOfLine(eol))
            : constructor.WithLeadingTrivia(Whitespace(indentation)).WithTrailingTrivia(mixin.Members.Count > 0
                ? TriviaList(EndOfLine(eol), EndOfLine(eol))
                : TriviaList(EndOfLine(eol)));

        context.RegisterCodeFix(CodeAction.Create(
            $"Add a constructor taking '{viewModel.Name}'",
            _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(mixin, mixin.WithMembers(mixin.Members.Insert(fields, constructor))))),
            equivalenceKey: "UIX0006:Constructor"), diagnostic);
    }

    private static AttributeSyntax? FindMixinAttribute(ClassDeclarationSyntax mixin, SemanticModel model, CancellationToken cancellation) =>
        mixin.AttributeLists.SelectMany(l => l.Attributes).FirstOrDefault(a =>
            model.GetSymbolInfo(a, cancellation).Symbol?.ContainingType?.ToDisplayString() == "Bannerlord.UIExtenderEx.Attributes.ViewModelMixinAttribute");

    private static System.Collections.Generic.IEnumerable<INamedTypeSymbol> SelfAndBases(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
            yield return current;
    }
}

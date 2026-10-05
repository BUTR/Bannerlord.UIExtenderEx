using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers.Prefabs;

/// <summary>
/// Extracts property change notifications announced in source code by mod-defined widget classes.
/// Serves as the compile-time equivalent of <c>widgets[].announcements</c> from game GUI packages for types defined in the current compilation.
/// <para>
/// Gauntlet widgets announce property changes by invoking one of the nine protected <c>OnPropertyChanged</c> overloads on <c>PropertyOwnerObject</c>.
/// Strongly-typed overloads define the announced property type directly, whereas the <see cref="object"/> overload uses the static type of the argument expression.
/// Property names are extracted from string literal arguments or inferred from <see cref="System.Runtime.CompilerServices.CallerMemberNameAttribute"/> defaults.
/// </para>
/// <para>
/// Only statically determinable announcements are tracked. Non-constant property names, unconstrained types (<see cref="object"/>, interfaces, type parameters),
/// or classes defined in referenced binary assemblies are omitted and permitted without warning.
/// </para>
/// </summary>
internal sealed class SourceAnnouncements
{
    private const string PropertyOwnerObject = "TaleWorlds.GauntletUI.PropertyOwnerObject";

    private readonly Hosts _hosts;
    private readonly Dictionary<INamedTypeSymbol, List<(string Name, ITypeSymbol Type)>> _byType = new(SymbolEqualityComparer.Default);

    public SourceAnnouncements(Hosts hosts) => _hosts = hosts;

    /// <summary>Returns all types announced under the specified property name by the given widget and its base classes declared in source.</summary>
    public IReadOnlyList<ITypeSymbol> Announced(INamedTypeSymbol widget, string name) =>
    [
        .. Hosts.SelfAndBases(widget)
            .Select(t => t.OriginalDefinition)
            .Where(t => t.DeclaringSyntaxReferences.Length > 0)
            .SelectMany(Of)
            .Where(x => string.Equals(x.Name, name, StringComparison.Ordinal))
            .Select(x => x.Type)
            .Distinct<ITypeSymbol>(SymbolEqualityComparer.Default),
    ];

    private List<(string Name, ITypeSymbol Type)> Of(INamedTypeSymbol type)
    {
        lock (_byType)
        {
            if (_byType.TryGetValue(type, out var known))
                return known;
            var announced = new List<(string, ITypeSymbol)>();
            // Analyzes class syntax trees, including nested types, lambdas, and local functions, to locate OnPropertyChanged calls.
            foreach (var reference in type.DeclaringSyntaxReferences)
            {
                var declaration = reference.GetSyntax();
                var model = _hosts.SemanticModel(reference.SyntaxTree);
                foreach (var invocation in declaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (Read(invocation, model) is { } found)
                        announced.Add(found);
                }
            }
            _byType[type] = announced;
            return announced;
        }
    }

    private static (string Name, ITypeSymbol Type)? Read(InvocationExpressionSyntax invocation, SemanticModel model)
    {
        var called = invocation.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            _ => null,
        };
        if (called != "OnPropertyChanged" || model.GetOperation(invocation) is not IInvocationOperation operation)
            return null;
        var method = operation.TargetMethod;
        if (method.Parameters.Length != 2 || method.ContainingType is not { } owner || Hosts.MetadataName(owner.OriginalDefinition) != PropertyOwnerObject)
            return null;
        if (operation.Arguments.FirstOrDefault(a => a.Parameter?.Ordinal == 0) is not { } valueArgument
            || operation.Arguments.FirstOrDefault(a => a.Parameter?.Ordinal == 1) is not { } nameArgument)
            return null;

        var type = method.Parameters[0].Type.SpecialType == SpecialType.System_Object
            ? (valueArgument.Value is IConversionOperation { IsImplicit: true } conversion ? conversion.Operand : valueArgument.Value).Type
            : method.Parameters[0].Type;
        if (type is null || BindingTypes.Unknowable(type))
            return null;

        var name = nameArgument.Value.ConstantValue is { HasValue: true, Value: string constant }
            ? constant
            : nameArgument.ArgumentKind == ArgumentKind.DefaultValue && IsCallerMemberName(nameArgument.Parameter) ? CallerMemberName(model, invocation) : null;
        return name is null ? null : (name, type);
    }

    private static bool IsCallerMemberName(IParameterSymbol? parameter) =>
        parameter?.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.CallerMemberNameAttribute") == true;

    /// <summary>Resolves the effective member name supplied by <see cref="System.Runtime.CompilerServices.CallerMemberNameAttribute"/> at the invocation location, traversing past local functions and anonymous lambdas.</summary>
    private static string? CallerMemberName(SemanticModel model, InvocationExpressionSyntax invocation)
    {
        var symbol = model.GetEnclosingSymbol(invocation.SpanStart);
        while (symbol is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction or MethodKind.LocalFunction })
            symbol = symbol.ContainingSymbol;
        return symbol switch
        {
            IMethodSymbol { AssociatedSymbol: { } associated } => associated.Name,
            IMethodSymbol { MethodKind: MethodKind.Constructor } => ".ctor",
            IMethodSymbol { MethodKind: MethodKind.StaticConstructor } => ".cctor",
            _ => symbol?.Name,
        };
    }
}
using Microsoft.CodeAnalysis;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers;

/// <summary>
/// Represents a type annotated with <c>[ViewModelMixin]</c>, evaluated according to UIExtenderEx runtime semantics:
/// attribute configuration from <c>UIExtenderRuntime.Register</c>, target ViewModel discovery from <c>ViewModelComponent.GetViewModelType</c>,
/// and member injection from <c>ViewModelComponent.InitializeMixinsForVMInstance</c>.
/// </summary>
internal sealed class Mixin
{
    public INamedTypeSymbol Type { get; }
    public AttributeData Attribute { get; }

    /// <summary>
    /// The target ViewModel type argument resolved by <c>ViewModelComponent.GetViewModelType</c> (from
    /// <c>BaseViewModelMixin&lt;TViewModel&gt;</c> or the primary type argument of an <c>IViewModelMixin</c> interface implementation).
    /// </summary>
    public ITypeSymbol? ViewModelArgument { get; }

    /// <summary>The resolved host ViewModel symbol, or <see langword="null"/> if the type argument is invalid or cannot be verified.</summary>
    public INamedTypeSymbol? Host { get; }

    public string? RefreshMethodName { get; }
    public bool HandleDerived { get; }

    /// <summary>Public instance properties annotated with <c>[DataSourceProperty]</c> (including inherited properties).</summary>
    public ImmutableArray<IPropertySymbol> Properties { get; }

    /// <summary>Public instance methods annotated with <c>[DataSourceMethod]</c> (including inherited methods).</summary>
    public ImmutableArray<IMethodSymbol> Methods { get; }

    public Location Location => Type.Locations.FirstOrDefault() ?? Location.None;

    public Location AttributeLocation =>
        Attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? Location;

    private Mixin(INamedTypeSymbol type, AttributeData attribute, ITypeSymbol? viewModelArgument, INamedTypeSymbol? host,
        string? refreshMethodName, bool handleDerived, ImmutableArray<IPropertySymbol> properties, ImmutableArray<IMethodSymbol> methods)
    {
        Type = type;
        Attribute = attribute;
        ViewModelArgument = viewModelArgument;
        Host = host;
        RefreshMethodName = refreshMethodName;
        HandleDerived = handleDerived;
        Properties = properties;
        Methods = methods;
    }

    public static Mixin? TryCreate(INamedTypeSymbol type, KnownTypes known)
    {
        if (type.TypeKind != TypeKind.Class)
            return null;
        var attribute = type.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, known.ViewModelMixinAttribute));
        if (attribute is null)
            return null;

        // Parses constructor arguments for optional refresh method names (string) and handleDerived flags (bool)
        string? refreshMethodName = null;
        var handleDerived = false;
        foreach (var argument in attribute.ConstructorArguments)
        {
            if (argument.Kind != TypedConstantKind.Primitive)
                continue;
            if (argument.Type?.SpecialType == SpecialType.System_String)
                refreshMethodName = argument.Value as string;
            else if (argument.Type?.SpecialType == SpecialType.System_Boolean && argument.Value is bool value)
                handleDerived = value;
        }

        ITypeSymbol? viewModelArgument = null;
        for (var node = type; node is not null && known.BaseViewModelMixin is not null; node = node.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(node.OriginalDefinition, known.BaseViewModelMixin))
            {
                viewModelArgument = node.TypeArguments[0];
                break;
            }
        }
        for (var node = type; node is not null && viewModelArgument is null; node = node.BaseType)
        {
            if (!node.AllInterfaces.Contains(known.ViewModelMixinInterface, SymbolEqualityComparer.Default))
                continue;
            if (node.TypeArguments.Length > 0)
            {
                viewModelArgument = node.TypeArguments[0];
                break;
            }
        }
        var host = viewModelArgument is INamedTypeSymbol named && IsViewModel(named, known) ? named : null;

        return new Mixin(type, attribute, viewModelArgument, host, refreshMethodName, handleDerived,
            CollectMarked<IPropertySymbol>(type, known.DataSourcePropertyAttribute),
            CollectMarked<IMethodSymbol>(type, known.DataSourceMethodAttribute));
    }

    /// <summary>
    /// Collects public members annotated with the specified attribute across the inheritance hierarchy, prioritizing
    /// the most derived declaration and respecting reflection behavior (overrides without explicit attributes are not registered).
    /// </summary>
    private static ImmutableArray<T> CollectMarked<T>(INamedTypeSymbol type, INamedTypeSymbol? attribute) where T : class, ISymbol
    {
        if (attribute is null)
            return ImmutableArray<T>.Empty;

        var result = ImmutableArray.CreateBuilder<T>();
        var seen = new HashSet<string>();
        for (var node = type; node is not null && node.SpecialType != SpecialType.System_Object; node = node.BaseType)
        {
            foreach (var member in node.GetMembers().OfType<T>())
            {
                if (member.DeclaredAccessibility != Accessibility.Public)
                    continue;
                if (member is IMethodSymbol { MethodKind: not MethodKind.Ordinary })
                    continue;
                if (member is IPropertySymbol { IsIndexer: true })
                    continue;
                if (!seen.Add(member.Name))
                    continue;
                if (HasAttribute(member, attribute))
                    result.Add(member);
            }
        }
        return result.ToImmutable();
    }

    public static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attribute) =>
        symbol.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attribute));

    private static bool IsViewModel(INamedTypeSymbol type, KnownTypes known)
    {
        // When TaleWorlds.Library.ViewModel is not referenced in compilation, accepts any class symbol as potential ViewModel
        if (known.ViewModel is null)
            return type.TypeKind == TypeKind.Class;
        for (var node = type; node is not null; node = node.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(node, known.ViewModel))
                return true;
        }
        return false;
    }
}
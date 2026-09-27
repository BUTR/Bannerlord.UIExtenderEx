using Microsoft.CodeAnalysis;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers;

/// <summary>
/// A type marked <c>[ViewModelMixin]</c>, read the way the runtime reads it: <c>UIExtenderRuntime.Register</c> for the
/// attribute, <c>ViewModelComponent.GetViewModelType</c> for the ViewModel, and
/// <c>ViewModelComponent.InitializeMixinsForVMInstance</c> for the members it adds.
/// </summary>
internal sealed class Mixin
{
    public INamedTypeSymbol Type { get; }
    public AttributeData Attribute { get; }

    /// <summary>
    /// What <c>GetViewModelType</c> answers: the argument of the closed <c>BaseViewModelMixin&lt;TViewModel&gt;</c> among the
    /// base types; for a mixin implementing <c>IViewModelMixin</c> some other way, the first type argument of the first
    /// type from the mixin up that implements it.
    /// </summary>
    public ITypeSymbol? ViewModelArgument { get; }

    /// <summary><see cref="ViewModelArgument"/> when it is a ViewModel type the rules can check against.</summary>
    public INamedTypeSymbol? Host { get; }

    public string? RefreshMethodName { get; }
    public bool HandleDerived { get; }

    /// <summary>Public properties carrying <c>[DataSourceProperty]</c>, inherited ones included, as <c>Type.GetProperties()</c> returns them.</summary>
    public ImmutableArray<IPropertySymbol> Properties { get; }

    /// <summary>Public methods carrying <c>[DataSourceMethod]</c>, inherited ones included, as <c>Type.GetMethods()</c> returns them.</summary>
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

        // Every constructor of the attribute takes the refresh method name as a string, handleDerived as a bool, or both
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
    /// Public members up the chain, the most derived declaration of a name winning. An override hides what it overrides,
    /// and carries only its own attributes, as a <c>PropertyInfo</c> or <c>MethodInfo</c> does; so an override without the
    /// attribute adds nothing even when the member it overrides has it.
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
        // Without the game's ViewModel referenced there is nothing to tell a ViewModel by; take the argument as given
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

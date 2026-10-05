using Microsoft.CodeAnalysis;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Bannerlord.UIExtenderEx.Analyzers;

/// <summary>
/// Manages host ViewModel symbol inspection, providing a secondary compilation view configured with
/// <see cref="MetadataImportOptions.All"/> so private and internal members of referenced game ViewModels are visible to analyzers.
/// </summary>
/// <remarks>
/// Standard Roslyn compilations import public metadata only. Because TaleWorlds' Gauntlet runtime inspects private and internal
/// properties for DataSource reflection bindings and walks base types during <c>ViewModel.ExecuteCommand</c>, analyzer rules
/// require complete member visibility. The secondary compilation shares syntax trees and metadata references lazily, instantiating
/// only when mixin analysis begins.
/// </remarks>
internal sealed class Hosts
{
    private readonly Compilation _compilation;
    private readonly Lazy<Compilation> _complete;
    private readonly Lazy<ImmutableArray<INamedTypeSymbol>> _allTypes;
    private readonly ConcurrentDictionary<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>> _derived = new(SymbolEqualityComparer.Default);

    public Hosts(Compilation compilation)
    {
        _compilation = compilation;
        _complete = new Lazy<Compilation>(() => compilation.Options.MetadataImportOptions == MetadataImportOptions.All
            ? compilation
            : compilation.WithOptions(compilation.Options.WithMetadataImportOptions(MetadataImportOptions.All)), LazyThreadSafetyMode.ExecutionAndPublication);
        _allTypes = new Lazy<ImmutableArray<INamedTypeSymbol>>(() => CollectTypes(_complete.Value.GlobalNamespace), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>Gets all class symbols visible to the compilation within the complete metadata view.</summary>
    public ImmutableArray<INamedTypeSymbol> AllTypes => _allTypes.Value;

    /// <summary>Retrieves the semantic model for a syntax tree within the complete metadata view.</summary>
    public SemanticModel SemanticModel(SyntaxTree tree) => _complete.Value.GetSemanticModel(tree);

    /// <summary>Resolves a type symbol by its Gauntlet package metadata name (formatted as <c>Namespace.Outer+Inner</c>).</summary>
    public INamedTypeSymbol? TypeByMetadataName(string name) => _complete.Value.GetTypeByMetadataName(name);

    /// <summary>Formats a type symbol into standard Gauntlet metadata notation (<c>Namespace.Outer+Inner</c>).</summary>
    public static string MetadataName(INamedTypeSymbol type)
    {
        var name = type.MetadataName;
        for (var containing = type.ContainingType; containing is not null; containing = containing.ContainingType)
            name = containing.MetadataName + "+" + name;
        return type.ContainingNamespace is { IsGlobalNamespace: false } ns ? ns.ToDisplayString() + "." + name : name;
    }

    /// <summary>Resolves the corresponding symbol within the complete metadata view, ensuring all private and base members are imported.</summary>
    public INamedTypeSymbol Complete(INamedTypeSymbol type)
    {
        if (type.IsGenericType || type.ContainingAssembly is null)
            return type;

        var complete = _complete.Value;
        IAssemblySymbol? assembly;
        if (SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, _compilation.Assembly))
            assembly = complete.Assembly;
        else if (_compilation.GetMetadataReference(type.ContainingAssembly) is { } reference)
            assembly = complete.GetAssemblyOrModuleSymbol(reference) as IAssemblySymbol;
        else
            assembly = null;

        return assembly?.GetTypeByMetadataName(MetadataName(type)) ?? type;
    }

    /// <summary>
    /// Resolves instantiable types targeted by a mixin configuring <c>handleDerived: true</c>, returning the non-abstract
    /// host ViewModel itself along with any non-abstract derived subtypes discovered in the complete compilation view.
    /// </summary>
    public ImmutableArray<INamedTypeSymbol> InstantiableSelfAndDerived(INamedTypeSymbol completeHost) =>
        _derived.GetOrAdd(completeHost, host => _allTypes.Value
            .Where(t => !t.IsAbstract && SelfAndBases(t).Contains(host, SymbolEqualityComparer.Default))
            .ToImmutableArray());

    /// <summary>
    /// Resolves the host property matched by a Gauntlet DataSource binding path, mirroring <c>ViewModel.GetPropertiesOfType</c>
    /// reflection semantics: all instance properties declared on the type plus non-private instance properties inherited from base types.
    /// </summary>
    public static IPropertySymbol? FindTableProperty(INamedTypeSymbol host, string name)
    {
        foreach (var type in SelfAndBases(host))
        {
            foreach (var property in type.GetMembers(name).OfType<IPropertySymbol>())
            {
                if (property.IsStatic || property.IsIndexer)
                    continue;
                if (!SymbolEqualityComparer.Default.Equals(type, host) && property.DeclaredAccessibility == Accessibility.Private)
                    continue;
                return property;
            }
        }
        return null;
    }

    /// <summary>
    /// Resolves the host method invoked by a Gauntlet command binding, mirroring <c>ViewModel.ExecuteCommand</c>
    /// reflection lookup across the host hierarchy (including private instance methods).
    /// </summary>
    public static IMethodSymbol? FindCommandMethod(INamedTypeSymbol host, string name) =>
        SelfAndBases(host)
            .SelectMany(t => t.GetMembers(name).OfType<IMethodSymbol>())
            .FirstOrDefault(m => !m.IsStatic && m.MethodKind == MethodKind.Ordinary);

    /// <summary>
    /// Evaluates why <c>AccessTools2.Method(type, name)</c> would fail to resolve a refresh hook method on <paramref name="host"/>,
    /// returning an explanatory error message or <see langword="null"/> when a matching parameterless overload is found.
    /// </summary>
    public static string? WhyRefreshMethodIsNotFound(INamedTypeSymbol host, string name)
    {
        for (var type = host; type is not null; type = type.BaseType)
        {
            var visible = VisibleMethods(type, name);
            if (visible.Count == 0)
                continue;
            if (visible.Count == 1)
                return null;

            // Handles AmbiguousMatchException by retrying with Type.EmptyTypes across the type hierarchy
            for (var retry = host; retry is not null; retry = retry.BaseType)
            {
                if (VisibleMethods(retry, name).Any(m => m.Parameters.Length == 0))
                    return null;
            }
            return "it is overloaded, and no overload takes no parameters";
        }
        return "there is no method of that name";
    }

    /// <summary>Enumerates all candidate method names across the ViewModel hierarchy suitable for refresh hook registration.</summary>
    public static IEnumerable<string> RefreshMethodNames(INamedTypeSymbol host) => SelfAndBases(host)
        .SelectMany(t => t.GetMembers().OfType<IMethodSymbol>())
        .Where(m => m.MethodKind == MethodKind.Ordinary)
        .Select(m => m.Name)
        .Distinct()
        .Where(name => WhyRefreshMethodIsNotFound(host, name) is null);

    /// <summary>Simulates <c>Type.GetMethod(name, AccessTools.all)</c> reflection visibility for a given type level.</summary>
    private static List<IMethodSymbol> VisibleMethods(INamedTypeSymbol type, string name)
    {
        var bySignature = new Dictionary<string, IMethodSymbol>();
        foreach (var current in SelfAndBases(type))
        {
            var own = SymbolEqualityComparer.Default.Equals(current, type);
            foreach (var method in current.GetMembers(name).OfType<IMethodSymbol>())
            {
                if (method.MethodKind != MethodKind.Ordinary)
                    continue;
                if (!own && (method.IsStatic || method.DeclaredAccessibility == Accessibility.Private))
                    continue;
                var signature = string.Join(",", method.Parameters.Select(p => p.Type.ToDisplayString()));
                if (!bySignature.ContainsKey(signature)) // The most derived declaration of a signature hides base declarations
                    bySignature.Add(signature, method);
            }
        }
        return bySignature.Values.ToList();
    }

    /// <summary>
    /// Compares two type symbols for equivalence across disparate compilation contexts (such as standard and complete views)
    /// using fully qualified metadata display strings.
    /// </summary>
    public static bool SameType(ITypeSymbol a, ITypeSymbol b) =>
        SymbolEqualityComparer.Default.Equals(a, b)
        || a.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == b.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    /// <summary>Enumerates <paramref name="type"/> and its base types, excluding <see cref="object"/>.</summary>
    public static IEnumerable<INamedTypeSymbol> SelfAndBases(INamedTypeSymbol type)
    {
        for (var current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
            yield return current;
    }

    private static ImmutableArray<INamedTypeSymbol> CollectTypes(INamespaceSymbol root)
    {
        var result = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        var pending = new Stack<INamespaceOrTypeSymbol>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            switch (pending.Pop())
            {
                case INamespaceSymbol ns:
                    foreach (var member in ns.GetMembers())
                        pending.Push(member);
                    break;
                case INamedTypeSymbol type:
                    if (type.TypeKind == TypeKind.Class)
                        result.Add(type);
                    foreach (var nested in type.GetTypeMembers())
                        pending.Push(nested);
                    break;
            }
        }
        return result.ToImmutable();
    }
}
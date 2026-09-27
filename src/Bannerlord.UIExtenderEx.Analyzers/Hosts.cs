using Microsoft.CodeAnalysis;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Bannerlord.UIExtenderEx.Analyzers;

/// <summary>
/// The ViewModels mixins extend, with every member visible.
/// <para>
/// A compilation imports only the public members of what it references, and an analyzer cannot change the options of
/// the build it runs in. The binding table the runtime writes into holds private members too, and so does the walk
/// <c>ViewModel.ExecuteCommand</c> makes, so the rules need them. A second view of the same compilation with every member
/// imported has them. It shares the syntax trees and the references and imports lazily; measured at under a
/// millisecond to create, and it is only created once a mixin is found.
/// </para>
/// </summary>
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

    /// <summary>Every class the compilation can see, in the view with every member imported.</summary>
    public ImmutableArray<INamedTypeSymbol> AllTypes => _allTypes.Value;

    /// <summary>The same type, seen with every member of it and of its base types imported.</summary>
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
    /// The types a <c>handleDerived</c> mixin reaches that can have an instance: the ViewModel itself unless it is abstract,
    /// and every non-abstract type derived from it that the compilation can see. At runtime the mixin reaches every loaded
    /// type derived from it, other mods' included; those the build cannot know.
    /// </summary>
    public ImmutableArray<INamedTypeSymbol> InstantiableSelfAndDerived(INamedTypeSymbol completeHost) =>
        _derived.GetOrAdd(completeHost, host => _allTypes.Value
            .Where(t => !t.IsAbstract && SelfAndBases(t).Contains(host, SymbolEqualityComparer.Default))
            .ToImmutableArray());

    /// <summary>
    /// The host property a binding by this name reaches: the keys of <c>ViewModel.GetPropertiesOfType</c>, which takes
    /// <c>GetProperties(Instance | Public | NonPublic)</c> - every instance property the type declares, and every
    /// non-private one it inherits.
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
    /// The host method a command by this name reaches: <c>ViewModel.ExecuteCommand</c> looks in the table, which holds the
    /// type's instance methods of every accessibility, and then walks up the base types, private methods included.
    /// </summary>
    public static IMethodSymbol? FindCommandMethod(INamedTypeSymbol host, string name) =>
        SelfAndBases(host)
            .SelectMany(t => t.GetMembers(name).OfType<IMethodSymbol>())
            .FirstOrDefault(m => !m.IsStatic && m.MethodKind == MethodKind.Ordinary);

    /// <summary>
    /// Why <c>AccessTools2.Method(type, name)</c> would answer null for the refresh method, or null when it finds one.
    /// It asks each type from <paramref name="host"/> up for the name (its own methods of any accessibility and the
    /// non-private instance methods it inherits), and when a type has several overloads it looks for a parameterless one
    /// instead.
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

            // An AmbiguousMatchException, and the retry with Type.EmptyTypes from the start again
            for (var retry = host; retry is not null; retry = retry.BaseType)
            {
                if (VisibleMethods(retry, name).Any(m => m.Parameters.Length == 0))
                    return null;
            }
            return "it is overloaded, and no overload takes no parameters";
        }
        return "there is no method of that name";
    }

    /// <summary>The names on the ViewModel and its base types a refresh method can be hooked by.</summary>
    public static IEnumerable<string> RefreshMethodNames(INamedTypeSymbol host) => SelfAndBases(host)
        .SelectMany(t => t.GetMembers().OfType<IMethodSymbol>())
        .Where(m => m.MethodKind == MethodKind.Ordinary)
        .Select(m => m.Name)
        .Distinct()
        .Where(name => WhyRefreshMethodIsNotFound(host, name) is null);

    /// <summary>What <c>Type.GetMethod(name, AccessTools.all)</c> sees on one type, one entry per signature.</summary>
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
                if (!bySignature.ContainsKey(signature)) // the most derived declaration of a signature hides the rest
                    bySignature.Add(signature, method);
            }
        }
        return bySignature.Values.ToList();
    }

    /// <summary>
    /// Whether two types are the same, one of them possibly seen through <see cref="Complete"/>. The two views are two
    /// compilations, whose symbols never compare equal, so across them a type is compared by its fully qualified name.
    /// </summary>
    public static bool SameType(ITypeSymbol a, ITypeSymbol b) =>
        SymbolEqualityComparer.Default.Equals(a, b)
        || a.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == b.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    /// <summary>The type and its base types, <c>System.Object</c> left out: nothing a mixin adds collides with it usefully.</summary>
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

    private static string MetadataName(INamedTypeSymbol type)
    {
        var name = type.MetadataName;
        for (var outer = type.ContainingType; outer is not null; outer = outer.ContainingType)
            name = outer.MetadataName + "+" + name;
        return type.ContainingNamespace is { IsGlobalNamespace: false } ns ? ns.ToDisplayString() + "." + name : name;
    }
}

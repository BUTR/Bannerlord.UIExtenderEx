using Microsoft.CodeAnalysis;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers.Prefabs;

/// <summary>
/// Represents the data-binding context at a point in the prefab XML hierarchy:
/// a ViewModel, a binding list, an unknown type, or a probe scope.
/// </summary>
internal abstract class Scope
{
    public abstract string Key { get; }
}

/// <summary>Represents a data-binding scope resolved to a specific ViewModel type.</summary>
internal sealed class ViewModelScope : Scope
{
    public INamedTypeSymbol Type { get; }
    public ViewModelScope(INamedTypeSymbol type) => Type = type;
    public override string Key => Type.ToDisplayString();
}

/// <summary>Represents a data-binding scope resolved to a collection such as <c>MBBindingList&lt;T&gt;</c>.</summary>
internal sealed class ListScope : Scope
{
    public ITypeSymbol Element { get; }
    public ListScope(ITypeSymbol element) => Element = element;
    public override string Key => "list of " + Element.ToDisplayString();
}

/// <summary>Represents an unresolved or opaque ViewModel scope whose children cannot be verified at compile time.</summary>
internal sealed class UnknownScope : Scope
{
    public static readonly UnknownScope Instance = new();
    public override string Key => "?";
}

/// <summary>
/// Represents the insertion point of a patch where the target ViewModel scope must be inferred.
/// Records bound property and command names instead of verifying them immediately, allowing candidate
/// mixin host ViewModels to be evaluated for compatibility.
/// </summary>
internal sealed class ProbeScope : Scope
{
    /// <summary>The collection of property and command bindings encountered, including command flags and source locations.</summary>
    public List<(string Name, bool IsCommand, Location Location, bool InSpan)> Names { get; } = [];
    public override string Key => "probe";
}

/// <summary>
/// Resolves member and command bindings against a ViewModel hierarchy, mirroring Gauntlet's runtime binding table
/// and <c>ExecuteCommand</c> dispatch. Evaluates intrinsic ViewModel members, mod mixin extensions, and visible derived types.
/// </summary>
internal sealed class ScopeResolver
{
    private readonly Hosts _hosts;
    private readonly IReadOnlyList<Mixin> _mixins;
    private readonly ConcurrentDictionary<(string Type, string Name, bool Command), (bool Found, IPropertySymbol? Property, bool ViaMixin)> _cache = new();

    public ScopeResolver(Hosts hosts, IReadOnlyList<Mixin> mixins)
    {
        _hosts = hosts;
        _mixins = mixins;
    }

    /// <summary>Gets all ViewModels extended by the mod's mixins, including derived types when <c>handleDerived</c> is enabled.</summary>
    public IReadOnlyList<INamedTypeSymbol> MixinHosts()
    {
        var result = new List<INamedTypeSymbol>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mixin in _mixins)
        {
            var host = _hosts.Complete(mixin.Host!);
            var reached = mixin.HandleDerived ? _hosts.InstantiableSelfAndDerived(host) : ImmutableArray.Create(host);
            foreach (var type in reached)
            {
                if (seen.Add(type.ToDisplayString()))
                    result.Add(type);
            }
        }
        return result;
    }

    public bool TryProperty(INamedTypeSymbol viewModel, string name, out ITypeSymbol? type, out bool viaMixin)
    {
        var found = TryPropertySymbol(viewModel, name, out var property, out viaMixin);
        type = property?.Type;
        return found;
    }

    /// <summary>Resolves a property symbol on the ViewModel or its attached mixins to determine its declared type and setter signature.</summary>
    public bool TryPropertySymbol(INamedTypeSymbol viewModel, string name, out IPropertySymbol? property, out bool viaMixin)
    {
        var result = _cache.GetOrAdd((viewModel.ToDisplayString(), name, false), _ => Find(viewModel, name, command: false));
        property = result.Property;
        viaMixin = result.ViaMixin;
        return result.Found;
    }

    public bool TryCommand(INamedTypeSymbol viewModel, string name, out bool viaMixin)
    {
        var result = _cache.GetOrAdd((viewModel.ToDisplayString(), name, true), _ => Find(viewModel, name, command: true));
        viaMixin = result.ViaMixin;
        return result.Found;
    }

    private (bool Found, IPropertySymbol? Property, bool ViaMixin) Find(INamedTypeSymbol viewModel, string name, bool command)
    {
        if (FindOn(viewModel, name, command) is { Found: true } direct)
            return direct;
        foreach (var derived in _hosts.InstantiableSelfAndDerived(viewModel))
        {
            if (!Hosts.SameType(derived, viewModel) && FindOn(derived, name, command) is { Found: true } fromDerived)
                return fromDerived;
        }
        return (false, null, false);
    }

    private (bool Found, IPropertySymbol? Property, bool ViaMixin) FindOn(INamedTypeSymbol viewModel, string name, bool command)
    {
        foreach (var mixin in MixinsOf(viewModel))
        {
            if (command && mixin.Methods.Any(m => m.Name == name))
                return (true, null, true);
            if (!command && mixin.Properties.FirstOrDefault(p => p.Name == name) is { } property)
                return (true, property, true);
        }
        if (command)
            return Hosts.FindCommandMethod(viewModel, name) is not null ? (true, null, false) : (false, null, false);
        return Hosts.FindTableProperty(viewModel, name) is { } own ? (true, own, false) : (false, null, false);
    }

    /// <summary>
    /// Enumerates all accessible property or command names available on the specified ViewModel and its attached mixins,
    /// used for suggesting corrections when an unbound identifier is encountered.
    /// </summary>
    public IEnumerable<string> Names(INamedTypeSymbol viewModel, bool command)
    {
        foreach (var type in _hosts.InstantiableSelfAndDerived(viewModel).Prepend(viewModel))
        {
            foreach (var mixin in MixinsOf(type))
            {
                foreach (var name in command ? mixin.Methods.Select(m => m.Name) : mixin.Properties.Select(p => p.Name))
                    yield return name;
            }
            foreach (var declaring in Hosts.SelfAndBases(type))
            {
                foreach (var member in declaring.GetMembers())
                {
                    if (member.IsStatic || member.IsImplicitlyDeclared)
                        continue;
                    if (command && member is IMethodSymbol { MethodKind: MethodKind.Ordinary })
                        yield return member.Name;
                    else if (!command && member is IPropertySymbol { IsIndexer: false }
                             && (Hosts.SameType(declaring, type) || member.DeclaredAccessibility != Accessibility.Private))
                        yield return member.Name;
                }
            }
        }
    }

    /// <summary>
    /// Determines whether the mod itself supplies the requested property or command on the specified ViewModel across
    /// all game versions (via a mixin or a mod-derived ViewModel subclass), rather than relying on vanilla game GUI definitions.
    /// </summary>
    public bool ModAnswers(INamedTypeSymbol viewModel, string name, bool command, Func<INamedTypeSymbol, bool> isGame)
    {
        foreach (var type in _hosts.InstantiableSelfAndDerived(viewModel).Prepend(viewModel))
        {
            if (MixinsOf(type).Any(m => command ? m.Methods.Any(x => x.Name == name) : m.Properties.Any(x => x.Name == name)))
                return true;
            foreach (var declaring in Hosts.SelfAndBases(type).TakeWhile(t => t.Locations.Any(l => l.IsInSource) && !isGame(t)))
            {
                var members = declaring.GetMembers(name);
                if (command
                        ? members.OfType<IMethodSymbol>().Any(m => !m.IsStatic && m.MethodKind == MethodKind.Ordinary)
                        : members.OfType<IPropertySymbol>().Any(p => !p.IsStatic && !p.IsIndexer && (Hosts.SameType(declaring, type) || p.DeclaredAccessibility != Accessibility.Private)))
                    return true;
            }
        }
        return false;
    }

    /// <summary>Returns the mixins targeting the specified ViewModel type or inherited base types with <c>handleDerived</c> enabled.</summary>
    private IEnumerable<Mixin> MixinsOf(INamedTypeSymbol viewModel) => _mixins.Where(m =>
        Hosts.SameType(m.Host!, viewModel)
        || (m.HandleDerived && Hosts.SelfAndBases(viewModel).Any(b => Hosts.SameType(b, m.Host!))));

    /// <summary>Determines the child data-binding scope resulting from navigating into a property of the specified type.</summary>
    public Scope ChildScope(ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol named)
            return UnknownScope.Instance;
        foreach (var candidate in Hosts.SelfAndBases(named).Concat(named.AllInterfaces))
        {
            if (candidate.IsGenericType && candidate.Name is "MBBindingList" && candidate.TypeArguments.Length == 1)
                return new ListScope(candidate.TypeArguments[0]);
        }
        if (Hosts.SelfAndBases(named).Any(t => t.ToDisplayString() == "TaleWorlds.Library.ViewModel") && named.ToDisplayString() != "TaleWorlds.Library.ViewModel")
            return new ViewModelScope(_hosts.Complete(named));
        return UnknownScope.Instance;
    }
}
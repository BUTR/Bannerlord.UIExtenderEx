using Microsoft.CodeAnalysis;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers.Prefabs;

/// <summary>What a point of the XML binds: a ViewModel, a binding list, something the build cannot tell, or a probe.</summary>
internal abstract class Scope
{
    public abstract string Key { get; }
}

internal sealed class ViewModelScope : Scope
{
    public INamedTypeSymbol Type { get; }
    public ViewModelScope(INamedTypeSymbol type) => Type = type;
    public override string Key => Type.ToDisplayString();
}

internal sealed class ListScope : Scope
{
    public ITypeSymbol Element { get; }
    public ListScope(ITypeSymbol element) => Element = element;
    public override string Key => "list of " + Element.ToDisplayString();
}

/// <summary>A ViewModel the build cannot know: nothing below it is checked.</summary>
internal sealed class UnknownScope : Scope
{
    public static readonly UnknownScope Instance = new();
    public override string Key => "?";
}

/// <summary>
/// The insertion point of a patch whose ViewModel is to be inferred: records the names bound at it instead of checking
/// them, so the mod's mixins can be asked which of their ViewModels answers.
/// </summary>
internal sealed class ProbeScope : Scope
{
    /// <summary>Each name, and whether <c>Location</c> is the literal that is the value rather than an attribute's name.</summary>
    public List<(string Name, bool IsCommand, Location Location, bool InSpan)> Names { get; } = [];
    public override string Key => "probe";
}

/// <summary>
/// Whether a ViewModel answers a name, the way an instance's binding table and <c>ExecuteCommand</c> would: its own
/// members, the members this mod's mixins put on it, and, because a property declared as a base type may hold a derived
/// instance, the same of the types derived from it that the compilation can see.
/// </summary>
internal sealed class ScopeResolver
{
    private readonly Hosts _hosts;
    private readonly IReadOnlyList<Mixin> _mixins;
    private readonly ConcurrentDictionary<(string Type, string Name, bool Command), (bool Found, ITypeSymbol? Type, bool ViaMixin)> _cache = new();

    public ScopeResolver(Hosts hosts, IReadOnlyList<Mixin> mixins)
    {
        _hosts = hosts;
        _mixins = mixins;
    }

    /// <summary>The ViewModels the mod's mixins extend, with the derived types a <c>handleDerived</c> mixin reaches.</summary>
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
        var result = _cache.GetOrAdd((viewModel.ToDisplayString(), name, false), _ => Find(viewModel, name, command: false));
        type = result.Type;
        viaMixin = result.ViaMixin;
        return result.Found;
    }

    public bool TryCommand(INamedTypeSymbol viewModel, string name, out bool viaMixin)
    {
        var result = _cache.GetOrAdd((viewModel.ToDisplayString(), name, true), _ => Find(viewModel, name, command: true));
        viaMixin = result.ViaMixin;
        return result.Found;
    }

    private (bool Found, ITypeSymbol? Type, bool ViaMixin) Find(INamedTypeSymbol viewModel, string name, bool command)
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

    private (bool Found, ITypeSymbol? Type, bool ViaMixin) FindOn(INamedTypeSymbol viewModel, string name, bool command)
    {
        foreach (var mixin in MixinsOf(viewModel))
        {
            if (command && mixin.Methods.Any(m => m.Name == name))
                return (true, null, true);
            if (!command && mixin.Properties.FirstOrDefault(p => p.Name == name) is { } property)
                return (true, property.Type, true);
        }
        if (command)
            return Hosts.FindCommandMethod(viewModel, name) is not null ? (true, null, false) : (false, null, false);
        return Hosts.FindTableProperty(viewModel, name) is { } own ? (true, own.Type, false) : (false, null, false);
    }

    /// <summary>
    /// Every name a binding (or with <paramref name="command"/>, a command) on this ViewModel reaches, the way
    /// <see cref="TryProperty"/> and <see cref="TryCommand"/> look them up: for suggesting one in place of a misspelling.
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

    /// <summary>The mod's mixins attached to instances of this type: registered for it, or for a base with <c>handleDerived</c>.</summary>
    private IEnumerable<Mixin> MixinsOf(INamedTypeSymbol viewModel) => _mixins.Where(m =>
        Hosts.SameType(m.Host!, viewModel)
        || (m.HandleDerived && Hosts.SelfAndBases(viewModel).Any(b => Hosts.SameType(b, m.Host!))));

    /// <summary>What binds below a property of this type: a ViewModel, a binding list's elements, or nothing the build can tell.</summary>
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

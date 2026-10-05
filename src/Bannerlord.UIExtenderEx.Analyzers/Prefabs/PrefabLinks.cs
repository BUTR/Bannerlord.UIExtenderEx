using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers.Prefabs;

/// <summary>
/// Represents a validated <c>[assembly: PrefabLink]</c> declaration associating a prefab patch or standalone prefab
/// with its bound ViewModel and optional mixin.
/// </summary>
internal sealed class PrefabLink
{
    public INamedTypeSymbol? Patch { get; }
    public PrefabXml? Prefab { get; }
    public string Name { get; }
    public INamedTypeSymbol ViewModel { get; }
    public Mixin? Mixin { get; }
    public Location MixinLocation { get; }
    public Location ViewModelLocation { get; }

    public PrefabLink(INamedTypeSymbol? patch, PrefabXml? prefab, string name, INamedTypeSymbol viewModel, Mixin? mixin, Location mixinLocation, Location viewModelLocation)
    {
        ViewModelLocation = viewModelLocation;
        Patch = patch;
        Prefab = prefab;
        Name = name;
        ViewModel = viewModel;
        Mixin = mixin;
        MixinLocation = mixinLocation;
    }
}

/// <summary>
/// Collects and validates <c>[assembly: PrefabLink]</c> attributes declared across the compilation.
/// Reports invalid links (<c>UIX0018</c>) and excludes them from subsequent scope analysis.
/// </summary>
internal static class PrefabLinks
{
    public static List<PrefabLink> Collect(Compilation compilation, KnownTypes known, PrefabSources sources, Action<Diagnostic> report)
    {
        var links = new List<PrefabLink>();
        if (known.PrefabLinkAttribute is null)
            return links;

        var patches = new HashSet<INamedTypeSymbol>(sources.Patches.Select(p => p.Type), SymbolEqualityComparer.Default);
        var viewModelOf = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
        foreach (var attribute in compilation.Assembly.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, known.PrefabLinkAttribute))
                continue;
            var arguments = attribute.ConstructorArguments;
            if (arguments.Length < 2)
                continue;

            var syntax = attribute.ApplicationSyntaxReference?.GetSyntax() as AttributeSyntax;
            Location At(int index) => syntax?.ArgumentList is { } list && list.Arguments.Count > index
                ? list.Arguments[index].GetLocation()
                : syntax?.GetLocation() ?? Location.None;
            void Invalid(int index, string why) => report(Diagnostic.Create(Descriptors.PrefabLinkDoesNotHold, At(index), why));

            INamedTypeSymbol? patch = null;
            PrefabXml? prefab = null;
            string name;
            switch (arguments[0].Value)
            {
                case INamedTypeSymbol type:
                    name = type.Name;
                    if (!patches.Contains(type))
                    {
                        Invalid(0, $"'{name}' is not a prefab patch of this mod, a patch class marked [PrefabExtension] that goes in at an XPath");
                        continue;
                    }
                    patch = type;
                    break;
                case string prefabName:
                    name = prefabName;
                    if (!sources.PrefabsByTag.TryGetValue(prefabName, out prefab))
                    {
                        Invalid(0, $"no prefab of this mod is registered under the name '{prefabName}' or has it as its file name");
                        continue;
                    }
                    break;
                default:
                    continue;
            }

            if (arguments[1].Value is not INamedTypeSymbol viewModel || viewModel.TypeKind == TypeKind.Error)
                continue;
            if (!IsViewModel(viewModel, known))
            {
                Invalid(1, $"'{viewModel.Name}' is not a ViewModel");
                continue;
            }

            Mixin? mixin = null;
            if (arguments.Length > 2 && arguments[2].Value is INamedTypeSymbol mixinType && mixinType.TypeKind != TypeKind.Error)
            {
                mixin = Mixin.TryCreate(mixinType, known);
                if (mixin is null)
                {
                    Invalid(2, $"'{mixinType.Name}' is not marked [ViewModelMixin]");
                    continue;
                }
                // If the mixin lacks an identifiable host ViewModel (reported under UIX0005), evaluate the link without the mixin
                if (mixin.Host is { } host && WhyNotAttached(mixin, host, viewModel) is { } why)
                {
                    Invalid(2, why);
                    continue;
                }
            }

            var key = patch is not null ? "patch:" + patch.ToDisplayString() : "prefab:" + name;
            if (viewModelOf.TryGetValue(key, out var earlier))
            {
                if (!Hosts.SameType(earlier, viewModel))
                {
                    Invalid(1, $"'{name}' is already linked to '{earlier.Name}', and its XML binds one ViewModel where it goes in");
                    continue;
                }
            }
            else
            {
                viewModelOf[key] = viewModel;
            }

            links.Add(new PrefabLink(patch, prefab, name, viewModel, mixin, At(2), At(1)));
        }
        return links;
    }

    /// <summary>
    /// Explains why the specified mixin does not attach to instances of the target ViewModel, or returns <see langword="null"/> if attached.
    /// </summary>
    private static string? WhyNotAttached(Mixin mixin, INamedTypeSymbol host, INamedTypeSymbol viewModel)
    {
        if (Hosts.SameType(host, viewModel))
            return null;
        var derives = Hosts.SelfAndBases(viewModel).Any(b => Hosts.SameType(b, host));
        if (derives && mixin.HandleDerived)
            return null;
        return derives
            ? $"'{mixin.Type.Name}' extends '{host.Name}', a base of '{viewModel.Name}', without handleDerived, so it is not attached to a '{viewModel.Name}'"
            : $"'{mixin.Type.Name}' extends '{host.Name}', not '{viewModel.Name}'";
    }

    private static bool IsViewModel(INamedTypeSymbol type, KnownTypes known)
    {
        if (known.ViewModel is null)
            return type.TypeKind == TypeKind.Class;
        return Hosts.SelfAndBases(type).Any(t => Hosts.SameType(t, known.ViewModel));
    }
}
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers;

/// <summary>
/// Analyzes <c>[ViewModelMixin]</c> declarations against their target host ViewModels, validating member collisions,
/// inheritance compatibility, refresh hook resolutions, instantiation requirements, and cross-mixin member collisions.
/// </summary>
/// <remarks>
/// Per-mixin diagnostics run incrementally on symbol analysis for immediate IDE feedback; cross-mixin collisions
/// (<c>UIX0002</c>) evaluate during compilation end actions across the complete assembly scope.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MixinAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        Descriptors.MixinMemberReplacesHostMember,
        Descriptors.DuplicateMixinMember,
        Descriptors.RefreshMethodNotFound,
        Descriptors.MixinHostNeverInstantiated,
        Descriptors.MixinHasNoViewModel,
        Descriptors.MixinCannotBeCreated,
        Descriptors.MixinMemberNotPublic,
        Descriptors.OverrideDoesNotMatch);

    public override void Initialize(AnalysisContext context)
    {
        // Analyzes generated mixins alongside source types to detect collisions between generated and user-defined mixins
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        if (KnownTypes.Create(context.Compilation) is not { } known)
            return;

        var hosts = new Hosts(context.Compilation);
        var mixins = new ConcurrentBag<Mixin>();

        context.RegisterSymbolAction(symbolContext =>
        {
            if (symbolContext.Symbol is not INamedTypeSymbol type || Mixin.TryCreate(type, known) is not { } mixin)
                return;
            AnalyzeMixin(symbolContext, mixin, known, hosts);
            if (mixin.Host is not null)
                mixins.Add(mixin);
        }, SymbolKind.NamedType);

        context.RegisterCompilationEndAction(endContext => ReportDuplicates(endContext, mixins.ToList()));
    }

    private static void AnalyzeMixin(SymbolAnalysisContext context, Mixin mixin, KnownTypes known, Hosts hosts)
    {
        ReportNonPublicMembers(context, mixin, known);

        if (mixin.Host is null)
        {
            var reason = mixin.ViewModelArgument is null
                ? "it does not derive from BaseViewModelMixin<TViewModel>"
                : $"the ViewModel UIExtenderEx takes from its base types is '{mixin.ViewModelArgument.ToDisplayString()}', which is not a ViewModel; derive from BaseViewModelMixin<TViewModel>";
            context.Report(Diagnostic.Create(Descriptors.MixinHasNoViewModel, mixin.Location, mixin.Type.Name, reason));
            return;
        }

        var host = hosts.Complete(mixin.Host);

        if (WhyCannotBeCreated(context.Compilation, mixin) is var (whyNot, creationFix))
            context.Report(Diagnostic.Create(Descriptors.MixinCannotBeCreated, mixin.Location, FixData.Of((FixData.Reason, creationFix)), mixin.Type.Name, mixin.Host.Name, whyNot));

        if (host.IsAbstract && !mixin.HandleDerived)
            context.Report(Diagnostic.Create(Descriptors.MixinHostNeverInstantiated, mixin.Location, mixin.Host.Name, mixin.Type.Name));

        if (mixin.RefreshMethodName is { } refresh && Hosts.WhyRefreshMethodIsNotFound(host, refresh) is { } whyMissing)
        {
            var suggestions = Suggestions.Closest(refresh, Hosts.RefreshMethodNames(host));
            context.Report(Diagnostic.Create(Descriptors.RefreshMethodNotFound, mixin.AttributeLocation,
                FixData.Of((FixData.Suggestions, FixData.Join(suggestions))), refresh, mixin.Host.Name, whyMissing,
                known.IsV2 ? "UIExtender.Register throws here, and the mod's types after this one are not registered" : "the mixin's OnRefresh is never called"));
        }

        ReportReplacedHostMembers(context, mixin, host, hosts);
        ReportMismatchedOverrides(context, mixin, host, known);
    }

    /// <summary>
    /// Reports mismatched method override signatures (<c>UIX0008</c>) under <c>[BUTRViewModelOverride]</c>.
    /// Verifies delegate parameter shapes and confirms matching target method signatures on the host ViewModel hierarchy.
    /// </summary>
    private static void ReportMismatchedOverrides(SymbolAnalysisContext context, Mixin mixin, INamedTypeSymbol host, KnownTypes known)
    {
        if (known.OverrideAttribute is not { } overrideAttribute)
            return;

        foreach (var type in Hosts.SelfAndBases(mixin.Type))
        {
            foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
            {
                if (method.MethodKind != MethodKind.Ordinary || method.IsStatic && !SymbolEqualityComparer.Default.Equals(type, mixin.Type))
                    continue;
                if (!SymbolEqualityComparer.Default.Equals(type, mixin.Type) && method.DeclaredAccessibility == Accessibility.Private)
                    continue;
                var attribute = method.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, overrideAttribute));
                if (attribute is null)
                    continue;

                var targetName = attribute.ConstructorArguments.FirstOrDefault().Value as string ?? "";
                if (WhyOverrideDoesNotMatch(method, host, targetName) is { } why)
                    context.Report(Diagnostic.Create(Descriptors.OverrideDoesNotMatch, method.Locations.FirstOrDefault(l => l.IsInSource) ?? mixin.Location, method.Name, why));
            }
        }
    }

    private static string? WhyOverrideDoesNotMatch(IMethodSymbol method, INamedTypeSymbol host, string targetName)
    {
        if (method.IsStatic)
            return "it is static";
        if (!method.ReturnsVoid)
            return "only methods returning void can be taken over";
        if (method.Parameters.Any(p => p.RefKind != RefKind.None))
            return "methods with ref or out parameters cannot be taken over";
        if (method.Parameters.Length == 0 || method.Parameters[method.Parameters.Length - 1].Type is not INamedTypeSymbol { TypeKind: TypeKind.Delegate, DelegateInvokeMethod: { } invoke })
            return "its last parameter has to be the original, a delegate";

        var hostParameters = method.Parameters.Take(method.Parameters.Length - 1).Select(p => p.Type).ToList();
        var shape = string.Join(", ", hostParameters.Select(t => t.ToDisplayString()));
        if (!invoke.ReturnsVoid || invoke.Parameters.Length != hostParameters.Count
            || invoke.Parameters.Where((p, i) => !SymbolEqualityComparer.Default.Equals(p.Type, hostParameters[i]) || p.RefKind != RefKind.None).Any())
            return $"its original has to be a delegate taking ({shape}) and returning void";

        foreach (var type in Hosts.SelfAndBases(host))
        {
            var target = type.GetMembers(targetName).OfType<IMethodSymbol>().FirstOrDefault(m =>
                m.MethodKind == MethodKind.Ordinary
                && m.Parameters.Length == hostParameters.Count
                && m.Parameters.Select(p => p.Type).Zip(hostParameters, Hosts.SameType).All(x => x));
            if (target is null)
                continue;
            return target.IsStatic || !target.ReturnsVoid ? $"'{host.Name}' has no instance method {targetName}({shape}) returning void" : null;
        }
        return $"'{host.Name}' has no instance method {targetName}({shape}) returning void";
    }

    /// <summary>
    /// Reports collisions where mixin members shadow existing host ViewModel properties or methods (<c>UIX0001</c>).
    /// Evaluates across all instantiable host types (including derived subtypes when <c>handleDerived: true</c>).
    /// </summary>
    private static void ReportReplacedHostMembers(SymbolAnalysisContext context, Mixin mixin, INamedTypeSymbol host, Hosts hosts)
    {
        if (mixin.Properties.IsEmpty && mixin.Methods.IsEmpty)
            return;

        var targets = mixin.HandleDerived ? hosts.InstantiableSelfAndDerived(host) : ImmutableArray.Create(host);
        var reported = new HashSet<(ISymbol, ISymbol)>(PairComparer.Instance);
        foreach (var target in targets)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            foreach (var property in mixin.Properties)
            {
                if (Hosts.FindTableProperty(target, property.Name) is { } hostProperty && reported.Add((property, hostProperty)))
                    Report(property, hostProperty, "property");
            }
            foreach (var method in mixin.Methods)
            {
                if (Hosts.FindCommandMethod(target, method.Name) is { } hostMethod && reported.Add((method, hostMethod)))
                    Report(method, hostMethod, "method");
            }
        }

        void Report(ISymbol member, ISymbol hostMember, string kind)
        {
            // Inherited members from external assemblies lack source locations; substitutes the mixin declaration location
            var location = member.Locations.FirstOrDefault(l => l.IsInSource) ?? mixin.Location;
            var accessibility = hostMember.DeclaredAccessibility switch
            {
                Accessibility.ProtectedOrInternal => "protected internal",
                Accessibility.ProtectedAndInternal => "private protected",
                var other => other.ToString().ToLowerInvariant(),
            };
            context.Report(Diagnostic.Create(Descriptors.MixinMemberReplacesHostMember, location,
                member.Name, mixin.Type.Name, $"{accessibility} {kind}", hostMember.ContainingType.Name));
        }
    }

    /// <summary>
    /// Reports DataSource properties or methods that lack <c>public</c> accessibility (<c>UIX0007</c>),
    /// preventing Gauntlet reflection bindings from accessing them.
    /// </summary>
    private static void ReportNonPublicMembers(SymbolAnalysisContext context, Mixin mixin, KnownTypes known)
    {
        foreach (var member in mixin.Type.GetMembers())
        {
            if (member.DeclaredAccessibility == Accessibility.Public || member.IsImplicitlyDeclared)
                continue;
            var attributeName = member switch
            {
                IPropertySymbol when known.DataSourcePropertyAttribute is { } attribute && Mixin.HasAttribute(member, attribute) => "DataSourceProperty",
                IMethodSymbol { MethodKind: MethodKind.Ordinary } when known.DataSourceMethodAttribute is { } attribute && Mixin.HasAttribute(member, attribute) => "DataSourceMethod",
                _ => null,
            };
            if (attributeName is not null)
                context.Report(Diagnostic.Create(Descriptors.MixinMemberNotPublic, member.Locations.FirstOrDefault(), member.Name, attributeName));
        }
    }

    /// <summary>
    /// Evaluates whether a mixin type can be instantiated via <c>Activator.CreateInstance(mixinType, instance)</c>,
    /// verifying that the type is concrete, non-generic, and exposes a public constructor accepting the host ViewModel.
    /// </summary>
    private static (string Why, string Reason)? WhyCannotBeCreated(Compilation compilation, Mixin mixin)
    {
        if (mixin.Type.IsAbstract)
            return ("it is abstract", FixData.ReasonAbstract);
        if (mixin.Type.TypeParameters.Length > 0)
            return ("it is generic", FixData.ReasonGeneric);

        var host = mixin.Host!;
        foreach (var constructor in mixin.Type.InstanceConstructors)
        {
            if (constructor.DeclaredAccessibility != Accessibility.Public || constructor.Parameters.Length != 1)
                continue;
            var conversion = compilation.ClassifyCommonConversion(host, constructor.Parameters[0].Type);
            if (conversion.Exists && conversion.IsImplicit && (conversion.IsIdentity || conversion.IsReference))
                return null;
        }
        return ($"it has no public constructor taking a '{host.Name}' as its only parameter", FixData.ReasonNoConstructor);
    }

    /// <summary>
    /// Reports duplicate members injected by different mixins onto the same host ViewModel hierarchy (<c>UIX0002</c>).
    /// </summary>
    private static void ReportDuplicates(CompilationAnalysisContext context, List<Mixin> mixins)
    {
        mixins.Sort((a, b) => string.CompareOrdinal(a.Type.ToDisplayString(), b.Type.ToDisplayString()));
        for (var i = 0; i < mixins.Count; i++)
        {
            for (var j = i + 1; j < mixins.Count; j++)
            {
                var (first, second) = (mixins[i], mixins[j]);
                if (Meet(first, second) is not { } host)
                    continue;

                var names = first.Properties.Select(p => p.Name).Intersect(second.Properties.Select(p => p.Name))
                    .Concat(first.Methods.Select(m => m.Name).Intersect(second.Methods.Select(m => m.Name)))
                    .Distinct()
                    .OrderBy(n => n, StringComparer.Ordinal);
                foreach (var name in names)
                {
                    context.Report(Diagnostic.Create(Descriptors.DuplicateMixinMember, second.Location,
                        additionalLocations: new[] { first.Location }, name, host.Name, first.Type.Name, second.Type.Name));
                }
            }
        }
    }

    /// <summary>Resolves the common target host ViewModel shared by two mixins, accounting for <c>handleDerived</c> propagation.</summary>
    private static INamedTypeSymbol? Meet(Mixin a, Mixin b)
    {
        var (hostA, hostB) = (a.Host!, b.Host!);
        if (SymbolEqualityComparer.Default.Equals(hostA, hostB))
            return hostA;
        if (a.HandleDerived && Hosts.SelfAndBases(hostB).Contains(hostA, SymbolEqualityComparer.Default))
            return hostB;
        if (b.HandleDerived && Hosts.SelfAndBases(hostA).Contains(hostB, SymbolEqualityComparer.Default))
            return hostA;
        return null;
    }

    private sealed class PairComparer : IEqualityComparer<(ISymbol, ISymbol)>
    {
        public static readonly PairComparer Instance = new();

        public bool Equals((ISymbol, ISymbol) x, (ISymbol, ISymbol) y) =>
            SymbolEqualityComparer.Default.Equals(x.Item1, y.Item1) && SymbolEqualityComparer.Default.Equals(x.Item2, y.Item2);

        public int GetHashCode((ISymbol, ISymbol) obj) =>
            unchecked((SymbolEqualityComparer.Default.GetHashCode(obj.Item1) * 397) ^ SymbolEqualityComparer.Default.GetHashCode(obj.Item2));
    }
}
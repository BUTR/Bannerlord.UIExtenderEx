using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers;

/// <summary>
/// Checks each <c>[ViewModelMixin]</c> against the ViewModel it extends, and the mixins of one assembly against each
/// other. The rules that concern one mixin are reported as its type is analysed, so they show while typing; the one
/// comparing mixins waits for the end of the compilation.
/// </summary>
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
        // A generated mixin is registered like any other, so it takes part in the comparison between mixins
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
            context.ReportDiagnostic(Diagnostic.Create(Descriptors.MixinHasNoViewModel, mixin.Location, mixin.Type.Name, reason));
            return;
        }

        var host = hosts.Complete(mixin.Host);

        if (WhyCannotBeCreated(context.Compilation, mixin) is var (whyNot, creationFix))
            context.ReportDiagnostic(Diagnostic.Create(Descriptors.MixinCannotBeCreated, mixin.Location, FixData.Of((FixData.Reason, creationFix)), mixin.Type.Name, mixin.Host.Name, whyNot));

        if (host.IsAbstract && !mixin.HandleDerived)
            context.ReportDiagnostic(Diagnostic.Create(Descriptors.MixinHostNeverInstantiated, mixin.Location, mixin.Host.Name, mixin.Type.Name));

        if (mixin.RefreshMethodName is { } refresh && Hosts.WhyRefreshMethodIsNotFound(host, refresh) is { } whyMissing)
        {
            var suggestions = Suggestions.Closest(refresh, Hosts.RefreshMethodNames(host));
            context.ReportDiagnostic(Diagnostic.Create(Descriptors.RefreshMethodNotFound, mixin.AttributeLocation,
                FixData.Of((FixData.Suggestions, FixData.Join(suggestions))), refresh, mixin.Host.Name, whyMissing,
                known.IsV2 ? "UIExtender.Register throws here, and the mod's types after this one are not registered" : "the mixin's OnRefresh is never called"));
        }

        ReportReplacedHostMembers(context, mixin, host, hosts);
        ReportMismatchedOverrides(context, mixin, host, known);
    }

    /// <summary>
    /// UIX0008. The runtime takes the mixin's instance methods of every accessibility, and the non-private ones it inherits,
    /// that carry the attribute; checks the override's own shape; and looks the method up on the ViewModel by name and by
    /// the override's parameters without the original, from the type up, whatever its accessibility.
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
                    context.ReportDiagnostic(Diagnostic.Create(Descriptors.OverrideDoesNotMatch, method.Locations.FirstOrDefault(l => l.IsInSource) ?? mixin.Location, method.Name, why));
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
    /// UIX0001, against every type the mixin is attached to: the ViewModel, or with <c>handleDerived</c> every type
    /// derived from it the compilation can see. One report per host member, however many derived types inherit it.
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
            // A member inherited from a base class in another assembly has no source location; the mixin stands in
            var location = member.Locations.FirstOrDefault(l => l.IsInSource) ?? mixin.Location;
            var accessibility = hostMember.DeclaredAccessibility switch
            {
                Accessibility.ProtectedOrInternal => "protected internal",
                Accessibility.ProtectedAndInternal => "private protected",
                var other => other.ToString().ToLowerInvariant(),
            };
            context.ReportDiagnostic(Diagnostic.Create(Descriptors.MixinMemberReplacesHostMember, location,
                member.Name, mixin.Type.Name, $"{accessibility} {kind}", hostMember.ContainingType.Name));
        }
    }

    /// <summary>
    /// UIX0007, for members the mixin declares itself. <c>GetProperties()</c> returns a property when either accessor is
    /// public, which is what a property declared public is.
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
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.MixinMemberNotPublic, member.Locations.FirstOrDefault(), member.Name, attributeName));
        }
    }

    /// <summary>
    /// Why <c>Activator.CreateInstance(mixinType, instance)</c> would throw: it needs a concrete, closed type with a public
    /// constructor whose one parameter accepts the ViewModel. With <c>handleDerived</c> the instance may be a derived
    /// type, which a parameter accepting the ViewModel accepts too.
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
    /// UIX0002: two mixins of this assembly putting one name on one ViewModel. They meet on a ViewModel when they extend
    /// the same one, or when one of them has <c>handleDerived</c> and extends a base of the other's. Properties and
    /// commands live in separate tables, so a property and a command of one name do not collide.
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
                    context.ReportDiagnostic(Diagnostic.Create(Descriptors.DuplicateMixinMember, second.Location,
                        additionalLocations: new[] { first.Location }, name, host.Name, first.Type.Name, second.Type.Name));
                }
            }
        }
    }

    /// <summary>The ViewModel two mixins both reach, the more derived when one reaches it through <c>handleDerived</c>.</summary>
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

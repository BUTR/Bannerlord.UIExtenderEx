using Bannerlord.UIExtenderEx.Analyzers.Game;
using Bannerlord.UIExtenderEx.Analyzers.Prefabs;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers;

/// <summary>
/// Checks a mod's prefab XML - its prefab files and the XML of its patches - against the widgets it names and the
/// ViewModels it binds. Layer 2 of docs/design-records/build-time-checks.md.
/// <para>
/// Where a patch goes, the XML binds whatever the game's movie binds at that node, which the build cannot read. It is
/// taken from the <c>[assembly: PrefabLink]</c> naming the patch when there is one, and otherwise from the mod's own
/// mixins: the ViewModel they extend on which the patch's names resolve, with at least one of those names added by a
/// mixin. A prefab of the mod's own is linked the same way, by name. From there the scope flows through
/// <c>DataSource</c> paths, list item templates, and into the mod's own prefabs.
/// </para>
/// <para>
/// With a <c>Bannerlord.ReferenceAssemblies.GUI</c> package referenced (layer 3), a patch is also applied to the game's
/// own prefab: its XPath has to select a node there, in the game without and with each DLC package referenced, and the
/// game's scope at that node replaces the one taken from the mixins.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PrefabAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        Descriptors.XmlNotWellFormed,
        Descriptors.UnknownWidgetAttribute,
        Descriptors.InvalidAttributeValue,
        Descriptors.UnknownPrefabParameter,
        Descriptors.BindingNotFound,
        Descriptors.BindingOnNoMixinViewModel,
        Descriptors.PrefabLinkDoesNotHold,
        Descriptors.LinkedMixinNotBound,
        Descriptors.XPathMatchesNothing,
        Descriptors.XPathMatchesSeveral,
        Descriptors.PrefabLinkDisagreesWithGame,
        Descriptors.XPathInvalid);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        // The whole mod has to be known - every patch, mixin and prefab file - so this runs once per compilation
        context.RegisterCompilationAction(compilation =>
        {
            if (KnownTypes.Create(compilation.Compilation) is { } known)
                Analyze(compilation, known);
        });
    }

    private static void Analyze(CompilationAnalysisContext context, KnownTypes known)
    {
        // The game's files come from its GUI packages, tagged by the analyzer targets; the rest are the mod's
        var (gameFiles, modFiles) = GameGui.Split(context.Options);
        var sources = PrefabSources.Collect(context.Compilation, modFiles, context.CancellationToken);
        foreach (var malformed in sources.MalformedFiles)
            context.ReportDiagnostic(Diagnostic.Create(Descriptors.XmlNotWellFormed, malformed.Error!.Value.Location, malformed.Error.Value.Message));
        var links = PrefabLinks.Collect(context.Compilation, known, sources, context.ReportDiagnostic);
        if (sources.Patches.Count == 0 && sources.PrefabsByTag.Count == 0)
            return;

        var hosts = new Hosts(context.Compilation);
        var mixins = SourceTypes(context.Compilation.Assembly.GlobalNamespace)
            .Select(t => Mixin.TryCreate(t, known))
            .Where(m => m?.Host is not null)
            .Select(m => m!)
            .ToList();
        var resolver = new ScopeResolver(hosts, mixins);
        var walker = new PrefabWalker(sources, resolver, hosts, context.ReportDiagnostic);
        var configurations = gameFiles.Count > 0 ? GameGui.Configurations(gameFiles, context.CancellationToken) : [];
        var checker = new GamePatchChecker(configurations, sources, context.CancellationToken);

        foreach (var patch in sources.Patches)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            foreach (var (xml, _) in patch.Contents)
            {
                if (xml.Error is { } error)
                    walker.Report(Descriptors.XmlNotWellFormed, error.Location, error.Message);
            }
            // The core selects the node with SelectSingleNode(xpath ?? ""), and an empty XPath throws as a broken one does
            var whyInvalid = patch.XPath is { } xpath
                ? GamePatchChecker.WhyXPathIsInvalid(xpath)
                : "the patch names none, and SelectSingleNode throws on an empty one";
            if (whyInvalid is not null)
                walker.Report(Descriptors.XPathInvalid, patch.XPathLocation, patch.XPath is { } written ? $" '{written}'" : "", whyInvalid);

            var targets = checker.Check(patch, context.ReportDiagnostic);
            foreach (var target in targets ?? [])
            {
                foreach (var (name, value, nameLocation, _) in patch.SetAttributes)
                    walker.CheckTagAttribute(target.Tag, name, value, nameLocation);
            }

            var patchLinks = links.Where(l => l.Patch is not null && SymbolEqualityComparer.Default.Equals(l.Patch, patch.Type)).ToList();
            AnalyzePatch(patch, patchLinks, walker, resolver, hosts, targets is null ? [] : SymbolScopes(targets, context.Compilation, hosts));
        }

        foreach (var (movie, viewModel) in sources.LoadedMovies)
        {
            if (sources.PrefabsByTag.TryGetValue(movie, out var prefab))
                walker.WalkPrefab(prefab, new ViewModelScope(hosts.Complete(viewModel)), Parameters.Empty);
        }

        var linkedPrefabs = new HashSet<PrefabXml>();
        foreach (var link in links)
        {
            if (link.Prefab is not { } prefab)
                continue;
            linkedPrefabs.Add(prefab);
            var probe = new ProbeScope();
            walker.WalkPrefab(prefab, probe, Parameters.Empty);
            walker.WalkPrefab(prefab, new ViewModelScope(hosts.Complete(link.ViewModel)), Parameters.Empty);
            CheckMixinBound(link, probe, walker, "at its root");
        }

        foreach (var prefab in sources.PrefabsByTag.Values.Distinct())
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            // What needs no scope: attribute names, literal values, parameters
            walker.WalkPrefab(prefab, UnknownScope.Instance, Parameters.Empty);
            if (linkedPrefabs.Contains(prefab))
                continue;

            // A file reached from nowhere the build can follow - a prefab a mod puts over one of the game's - is checked
            // further only when every name at its root resolves on one ViewModel of the mod's mixins, one of them through
            // a mixin. That settles the scope for what lies below without reporting anything at the root on a guess.
            var probe = new ProbeScope();
            walker.WalkPrefab(prefab, probe, Parameters.Empty);
            if (Infer(probe, resolver) is { Unresolved: 0 } inferred)
                walker.WalkPrefab(prefab, new ViewModelScope(inferred.Host), Parameters.Empty);
        }
    }

    /// <summary>
    /// The game's scope where the patch lands, as the compilation sees it, one per distinct type across the
    /// configurations. A configuration that reaches the node with several different scopes adds none, and neither does
    /// a type the mod does not reference: it has nothing to check the patch against then.
    /// </summary>
    private static List<Scope> SymbolScopes(IReadOnlyList<PatchTarget> targets, Compilation compilation, Hosts hosts)
    {
        var result = new List<Scope>();
        foreach (var target in targets)
        {
            if (target.Scopes.Count != 1)
                continue;
            Scope? scope = target.Scopes[0] switch
            {
                ViewModelGameScope vm when compilation.GetTypeByMetadataName(vm.Type) is { } type => new ViewModelScope(hosts.Complete(type)),
                ListGameScope list when compilation.GetTypeByMetadataName(list.ElementType) is { } element => new ListScope(hosts.Complete(element)),
                _ => null,
            };
            if (scope is not null && result.All(x => x.Key != scope.Key))
                result.Add(scope);
        }
        return result;
    }

    private static void AnalyzePatch(PrefabPatch patch, List<PrefabLink> links, PrefabWalker walker, ScopeResolver resolver, Hosts hosts, IReadOnlyList<Scope> gameScopes)
    {
        var probe = new ProbeScope();
        Walk(patch, walker, probe);

        // Every link of one patch names the same ViewModel; PrefabLinks leaves out one that does not
        if (links.Count > 0)
        {
            var linked = hosts.Complete(links[0].ViewModel);
            if (gameScopes.Count > 0 && !gameScopes.Any(scope => Agrees(scope, linked)))
                walker.Report(Descriptors.PrefabLinkDisagreesWithGame, links[0].ViewModelLocation, patch.Type.Name, linked.Name, string.Join(" or ", gameScopes.Select(Describe)));
            Walk(patch, walker, new ViewModelScope(linked));
            foreach (var link in links)
                CheckMixinBound(link, probe, walker, "where it goes in");
            return;
        }

        // The game says what binds where the patch lands: nothing to infer
        if (gameScopes.Count > 0)
        {
            foreach (var scope in gameScopes)
                Walk(patch, walker, scope);
            return;
        }

        if (probe.Names.Count == 0)
            return;

        if (Infer(probe, resolver) is { } inferred)
        {
            Walk(patch, walker, new ViewModelScope(inferred.Host));
            return;
        }

        // No mixin ViewModel of the mod answers the patch: each name none of them has is reported, naming them
        var candidates = resolver.MixinHosts();
        if (candidates.Count == 0)
            return;
        var names = string.Join(", ", candidates.Select(c => c.Name));
        foreach (var (name, isCommand, location, inSpan) in probe.Names)
        {
            var anywhere = candidates.Any(c => isCommand ? resolver.TryCommand(c, name, out _) : resolver.TryProperty(c, name, out _, out _));
            if (anywhere)
                continue;
            var suggestions = Suggestions.Closest(name, candidates.SelectMany(c => resolver.Names(c, isCommand)));
            walker.ReportWith(Descriptors.BindingOnNoMixinViewModel, location, PrefabWalker.Replacing(name, suggestions, inSpan), name, names);
        }
    }

    /// <summary>Whether a link's ViewModel is the game's at the node, or a base or subclass of it.</summary>
    private static bool Agrees(Scope scope, INamedTypeSymbol linked)
    {
        var type = scope switch
        {
            ViewModelScope vm => vm.Type,
            ListScope { Element: INamedTypeSymbol element } => element,
            _ => null,
        };
        return type is null
               || Hosts.SelfAndBases(linked).Any(t => Hosts.SameType(t, type))
               || Hosts.SelfAndBases(type).Any(t => Hosts.SameType(t, linked));
    }

    private static string Describe(Scope scope) => scope switch
    {
        ViewModelScope vm => "'" + vm.Type.Name + "'",
        ListScope list => "the items of a list of '" + list.Element.Name + "'",
        _ => "an unknown scope",
    };

    /// <summary>UIX0019: a link naming a mixin, on XML that binds none of the mixin's members at the point the link names.</summary>
    private static void CheckMixinBound(PrefabLink link, ProbeScope probe, PrefabWalker walker, string where)
    {
        if (link.Mixin is not { } mixin)
            return;
        var bound = probe.Names.Any(n => n.IsCommand
            ? mixin.Methods.Any(m => m.Name == n.Name)
            : mixin.Properties.Any(p => p.Name == n.Name));
        if (!bound)
            walker.Report(Descriptors.LinkedMixinNotBound, link.MixinLocation, link.Name, mixin.Type.Name, where);
    }

    private static void Walk(PrefabPatch patch, PrefabWalker walker, Scope scope)
    {
        foreach (var (xml, removeRootNode) in patch.Contents)
            walker.WalkContent(xml, removeRootNode, scope);
        foreach (var (_, value, _, location) in patch.SetAttributes)
            walker.CheckSetAttribute(value, location, scope);
    }

    /// <summary>
    /// The mixin ViewModel a probe's names point at: among those with at least one name added by a mixin, the one that
    /// answers most of them. Null when no mixin adds any of the names.
    /// </summary>
    private static (INamedTypeSymbol Host, int Unresolved)? Infer(ProbeScope probe, ScopeResolver resolver)
    {
        (INamedTypeSymbol Host, int Resolved)? best = null;
        foreach (var host in resolver.MixinHosts())
        {
            var resolved = 0;
            var evidence = false;
            foreach (var (name, isCommand, _, _) in probe.Names)
            {
                bool found, viaMixin;
                if (isCommand)
                    found = resolver.TryCommand(host, name, out viaMixin);
                else
                    found = resolver.TryProperty(host, name, out _, out viaMixin);
                if (!found)
                    continue;
                resolved++;
                evidence |= viaMixin;
            }
            if (evidence && (best is null || resolved > best.Value.Resolved))
                best = (host, resolved);
        }
        return best is { } chosen ? (chosen.Host, probe.Names.Count - chosen.Resolved) : null;
    }

    private static IEnumerable<INamedTypeSymbol> SourceTypes(INamespaceSymbol ns)
    {
        foreach (var member in ns.GetMembers())
        {
            if (member is INamespaceSymbol child)
            {
                foreach (var type in SourceTypes(child))
                    yield return type;
            }
            else if (member is INamedTypeSymbol type)
            {
                yield return type;
                foreach (var nested in type.GetTypeMembers())
                    yield return nested;
            }
        }
    }
}

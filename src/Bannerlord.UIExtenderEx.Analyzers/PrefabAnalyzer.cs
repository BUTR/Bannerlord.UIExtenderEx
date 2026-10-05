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
/// Analyzes module Gauntlet prefab XML documents and patch XML snippets against known widget definitions and bound ViewModel types.
/// </summary>
/// <remarks>
/// Evaluates Gauntlet hierarchy scopes starting from explicit <c>[assembly: PrefabLink]</c> declarations, game GUI bundle
/// target scopes, or inferred mixin host ViewModels. Propagates ViewModel and List scopes through <c>DataSource</c> paths,
/// list item templates, and nested prefabs.
/// <para>
/// When referenced against game GUI bundle packages (<see cref="GameGui"/>), validates patch XPath selectors against
/// vanilla and DLC prefab structures, ensuring target nodes exist and checking bound properties against official game scopes.
/// </para>
/// </remarks>
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
        Descriptors.BindingThrowsBetweenWidgetAndViewModel,
        Descriptors.DataSourceNotRefreshedWhenReplaced,
        Descriptors.ParameterInPassedChildren,
        Descriptors.DataSourceIntoAListByIndex,
        Descriptors.UnknownWidgetTag,
        Descriptors.PrefabLinkDoesNotHold,
        Descriptors.LinkedMixinNotBound,
        Descriptors.XPathMatchesNothing,
        Descriptors.XPathMatchesSeveral,
        Descriptors.PrefabLinkDisagreesWithGame,
        Descriptors.XPathInvalid,
        Descriptors.HoldsForSomeVersions,
        Descriptors.VersionNotInGuiPackages);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        // Analyzes all patches, mixins, and prefab XML documents across the compilation in a single unified pass
        context.RegisterCompilationAction(compilation =>
        {
            if (KnownTypes.Create(compilation.Compilation) is { } known)
                Analyze(compilation, known);
        });
    }

    private static void Analyze(CompilationAnalysisContext context, KnownTypes known)
    {
        // Segregates game GUI bundle files (tagged by MSBuild analyzer targets) from module-owned prefab files
        var (gameFiles, modFiles) = GameGui.Split(context.Options);
        // Formats reported diagnostics with target game version tags when configured
        var report = GameVersionTag.Reporter(context.Options, context.ReportDiagnostic);
        var sources = PrefabSources.Collect(context.Compilation, modFiles, context.CancellationToken);
        foreach (var malformed in sources.MalformedFiles)
            report(Diagnostic.Create(Descriptors.XmlNotWellFormed, malformed.Error!.Value.Location, malformed.Error.Value.Message));
        var links = PrefabLinks.Collect(context.Compilation, known, sources, report);
        if (sources.Patches.Count == 0 && sources.PrefabsByTag.Count == 0)
            return;

        var hosts = new Hosts(context.Compilation);
        var mixins = SourceTypes(context.Compilation.Assembly.GlobalNamespace)
            .Select(t => Mixin.TryCreate(t, known))
            .Where(m => m?.Host is not null)
            .Select(m => m!)
            .ToList();
        var resolver = new ScopeResolver(hosts, mixins);
        var gameVersions = GameVersions.Of(context.Options);
        var game = gameFiles.Count > 0 ? GameGui.Load(gameFiles, gameVersions, context.CancellationToken) : GameSet.Empty(gameVersions);
        // Emits UIX0025 once in multi-targeting builds on the newest supported version build
        if (game.NotInBundle.Count > 0 && game.Versions.Reports(game.NotInBundle))
        {
            report(Diagnostic.Create(Descriptors.VersionNotInGuiPackages, Location.None,
                string.Join(", ", game.NotInBundle), game.NotInBundle.Count == 1 ? "it" : "them", game.NewestBundled));
        }
        var walker = new PrefabWalker(sources, resolver, hosts, game, report);
        var checker = new GamePatchChecker(game, sources, context.CancellationToken);

        foreach (var patch in sources.Patches)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            foreach (var (xml, _) in patch.Contents)
            {
                if (xml.Error is { } error)
                    walker.Report(Descriptors.XmlNotWellFormed, error.Location, error.Message);
            }
            // TaleWorlds runtime executes SelectSingleNode(xpath ?? ""); empty XPaths throw XPathExceptions
            var whyInvalid = patch.XPath is { } xpath
                ? GamePatchChecker.WhyXPathIsInvalid(xpath)
                : "the patch names none, and SelectSingleNode throws on an empty one";
            if (whyInvalid is not null)
                walker.Report(Descriptors.XPathInvalid, patch.XPathLocation, patch.XPath is { } written ? $" '{written}'" : "", whyInvalid);

            var targets = checker.Check(patch, report);
            if (targets is not null)
            {
                foreach (var (name, value, nameLocation, _) in patch.SetAttributes)
                    walker.CheckTagAttribute(targets, name, value, nameLocation);
            }

            var patchLinks = links.Where(l => l.Patch is not null && SymbolEqualityComparer.Default.Equals(l.Patch, patch.Type)).ToList();
            // Evaluates patch XML in matched game configurations or across all configurations for custom prefabs
            var landsIn = targets?.Select(x => x.Configuration).ToList();
            AnalyzePatch(patch, patchLinks, walker, resolver, hosts, game, targets ?? [], targets is null ? [] : SymbolScopes(targets, context.Compilation, hosts), landsIn);
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
            // Validates scope-independent constructs: widget tag names, attribute declarations, literals, and parameters
            walker.WalkPrefab(prefab, UnknownScope.Instance, Parameters.Empty);
            if (linkedPrefabs.Contains(prefab))
                continue;

            // When a standalone prefab overrides a game movie without explicit links, infer its scope only when
            // root bindings unambiguously resolve against a single mixin ViewModel with at least one member contributed by a mixin
            var probe = new ProbeScope();
            walker.WalkPrefab(prefab, probe, Parameters.Empty);
            if (Infer(probe, resolver) is { Unresolved: 0 } inferred)
                walker.WalkPrefab(prefab, new ViewModelScope(inferred.Host), Parameters.Empty);
        }
    }

    /// <summary>
    /// Resolves target movie scopes where the patch lands across game configurations, grouping matching configurations
    /// by distinct scope type. Ignores configurations where ambiguous scopes or unreferenced types are encountered.
    /// </summary>
    private static List<(Scope Scope, List<GameConfiguration> Configurations)> SymbolScopes(IReadOnlyList<PatchTarget> targets, Compilation compilation, Hosts hosts)
    {
        var result = new List<(Scope Scope, List<GameConfiguration> Configurations)>();
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
            if (scope is null)
                continue;
            if (result.FirstOrDefault(x => x.Scope.Key == scope.Key) is { Scope: not null } known)
                known.Configurations.Add(target.Configuration);
            else
                result.Add((scope, [target.Configuration]));
        }
        return result;
    }

    private static void AnalyzePatch(PrefabPatch patch, List<PrefabLink> links, PrefabWalker walker, ScopeResolver resolver, Hosts hosts, GameSet game,
        IReadOnlyList<PatchTarget> targets, IReadOnlyList<(Scope Scope, List<GameConfiguration> Configurations)> gameScopes, IReadOnlyList<GameConfiguration>? landsIn)
    {
        var probe = new ProbeScope();
        Walk(patch, walker, probe, null);

        // Explicit prefab links establish the canonical ViewModel scope; PrefabLinks filters invalid links
        if (links.Count > 0)
        {
            var linked = hosts.Complete(links[0].ViewModel);
            CheckLinkAgrees(patch, links[0], linked, targets, hosts, game, walker);
            Walk(patch, walker, new ViewModelScope(linked), landsIn);
            foreach (var link in links)
                CheckMixinBound(link, probe, walker, "where it goes in");
            return;
        }

        // Uses official game GUI bundle scopes at the patch target node when available without inference
        if (gameScopes.Count > 0)
        {
            foreach (var (scope, configurations) in gameScopes)
                Walk(patch, walker, scope, configurations);
            return;
        }

        if (probe.Names.Count == 0)
            return;

        if (Infer(probe, resolver) is { } inferred)
        {
            Walk(patch, walker, new ViewModelScope(inferred.Host), landsIn);
            return;
        }

        // When no mixin ViewModel satisfies the patch bindings, reports missing properties/methods against all candidate mixin hosts
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

    /// <summary>
    /// Validates that the ViewModel linked by <c>[assembly: PrefabLink]</c> matches or derives from the game's actual
    /// ViewModel at the patch target node (<c>UIX0022</c> or multi-version <c>UIX0024</c>).
    /// </summary>
    private static void CheckLinkAgrees(PrefabPatch patch, PrefabLink link, INamedTypeSymbol linked, IReadOnlyList<PatchTarget> targets, Hosts hosts, GameSet game, PrefabWalker walker)
    {
        var linkedName = Hosts.MetadataName(linked);
        var present = new List<GameConfiguration>();
        var disagree = new List<GameConfiguration>();
        var bindsInstead = new List<string>();
        foreach (var target in targets)
        {
            if (target.Scopes.Count != 1)
                continue;
            var (type, describe) = target.Scopes[0] switch
            {
                ViewModelGameScope vm => (vm.Type, "'" + ShortName(vm.Type) + "'"),
                ListGameScope list => (list.ElementType, "the items of a list of '" + ShortName(list.ElementType) + "'"),
                _ => (null, ""),
            };
            if (type is null)
                continue;
            present.Add(target.Configuration);
            if (Bases(target.Configuration, linkedName).Contains(type) || Bases(target.Configuration, type).Contains(linkedName))
                continue;
            disagree.Add(target.Configuration);
            if (!bindsInstead.Contains(describe))
                bindsInstead.Add(describe);
        }
        if (disagree.Count == 0)
            return;
        var finding = Diagnostic.Create(Descriptors.PrefabLinkDisagreesWithGame, link.ViewModelLocation, patch.Type.Name, linked.Name, string.Join(" or ", bindsInstead));
        if (game.ForConfigurations(finding, present, disagree) is { } diagnostic)
            walker.Report(diagnostic);

        // Collects inheritance hierarchy from types.json or compilation metadata fallback
        HashSet<string> Bases(GameConfiguration configuration, string name)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            for (var current = name; result.Add(current);)
            {
                if (configuration.ViewModel(current) is { } recorded)
                {
                    if (recorded.BaseType is not { } baseType)
                        break;
                    current = baseType;
                    continue;
                }
                if (hosts.TypeByMetadataName(current) is { } compiled)
                    result.UnionWith(Hosts.SelfAndBases(compiled).Select(Hosts.MetadataName));
                break;
            }
            return result;
        }
    }

    /// <summary>Extracts the unqualified type name without generic arguments or namespace prefixes.</summary>
    private static string ShortName(string type)
    {
        var open = type.IndexOf('<');
        var plain = open < 0 ? type : type.Substring(0, open);
        return plain.Substring(Math.Max(plain.LastIndexOf('.'), plain.LastIndexOf('+')) + 1);
    }

    /// <summary>
    /// Reports linked mixins whose members are not bound within the patch XML at the insertion point (<c>UIX0019</c>).
    /// </summary>
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

    /// <summary>Walks patch content and attribute nodes within the specified scope and configuration filter.</summary>
    private static void Walk(PrefabPatch patch, PrefabWalker walker, Scope scope, IReadOnlyList<GameConfiguration>? configurations)
    {
        foreach (var (xml, removeRootNode) in patch.Contents)
            walker.WalkContent(xml, removeRootNode, scope, configurations);
        foreach (var (_, value, _, location) in patch.SetAttributes)
            walker.CheckSetAttribute(value, location, scope, configurations);
    }

    /// <summary>
    /// Infers the target host ViewModel from probe binding names, selecting the candidate that resolves the maximum
    /// number of bindings while requiring at least one member to originate from a mixin.
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
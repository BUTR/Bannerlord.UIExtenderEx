using Bannerlord.UIExtenderEx.Analyzers.Prefabs;

using Microsoft.CodeAnalysis;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.XPath;

namespace Bannerlord.UIExtenderEx.Analyzers.Game;

/// <summary>Where a patch lands in one configuration: the tag of the node its XPath selects, and what binds there.</summary>
internal sealed record PatchTarget(GameConfiguration Configuration, string Tag, IReadOnlyList<GameScope> Scopes);

/// <summary>
/// Applies a patch's XPath the way <c>PrefabComponent.RegisterPatch</c> does: <c>SelectSingleNode</c> on the document
/// of the prefab the patch names, which takes the first match. Reports an XPath that selects nothing (UIX0020) or more
/// than one node (UIX0021), in every configuration of every game version checked, and hands back where the patch
/// lands in the version the compilation builds against, so its XML can be checked against the game's scope there.
/// <para>
/// A finding is reported once. When it holds for every version checked, under its own rule; when it holds for some of
/// them only, as UIX0024, naming those versions. In one of <c>Bannerlord.BUTRModule.Sdk</c>'s builds per version, only
/// the build of the newest version it holds for reports it.
/// </para>
/// </summary>
internal sealed class GamePatchChecker
{
    private readonly GameSet _game;
    private readonly Dictionary<GameConfiguration, GameScopeResolver> _resolvers = new();
    private readonly PrefabSources _sources;
    private readonly CancellationToken _cancellation;

    public GamePatchChecker(GameSet game, PrefabSources sources, CancellationToken cancellation)
    {
        _game = game;
        _sources = sources;
        _cancellation = cancellation;
    }

    private GameScopeResolver Resolver(GameConfiguration configuration)
    {
        if (!_resolvers.TryGetValue(configuration, out var resolver))
            _resolvers[configuration] = resolver = new GameScopeResolver(configuration, _cancellation);
        return resolver;
    }

    /// <summary>
    /// The XPath is compiled as <c>SelectSingleNode</c> compiles it; null when that throws, or when it evaluates to
    /// something other than nodes, which throws too.
    /// </summary>
    public static string? WhyXPathIsInvalid(string xpath)
    {
        try
        {
            return XPathExpression.Compile(xpath).ReturnType == XPathResultType.NodeSet ? null : "it does not select nodes";
        }
        catch (XPathException exception)
        {
            return exception.Message;
        }
    }

    /// <summary>
    /// Checks the patch's XPath in the prefab it names: the mod's own prefab when it has one by that name, which is the
    /// one the game loads; otherwise the game's, in each configuration that has it. Null when the prefab is neither,
    /// as another mod's is.
    /// </summary>
    public IReadOnlyList<PatchTarget>? Check(PrefabPatch patch, Action<Diagnostic> report)
    {
        if (patch.XPath is not { } xpath || WhyXPathIsInvalid(xpath) is not null)
            return null;

        if (_sources.PrefabsByTag.TryGetValue(patch.Movie, out var own) && own.Document is not null)
        {
            if (Load(own.Document.ToString(SaveOptionsNone)) is { } ownDocument && Count(ownDocument, xpath) == 0 && !InsertedByTheMod(patch, xpath))
                report(Diagnostic.Create(Descriptors.XPathMatchesNothing, patch.XPathLocation, xpath, patch.Movie, " (this mod's own prefab)"));
            return null;
        }

        var targets = new List<PatchTarget>();
        var present = new List<GameConfiguration>();
        var missing = new List<GameConfiguration>();
        var several = new List<(GameConfiguration Configuration, int Count)>();
        foreach (var configuration in _game.Configurations)
        {
            _cancellation.ThrowIfCancellationRequested();
            if (configuration.Prefab(patch.Movie) is not { } prefab || prefab.Document(_cancellation) is not { } document)
                continue;
            present.Add(configuration);

            var nodes = document.SelectNodes(xpath)?.OfType<XmlNode>().ToList() ?? [];
            if (nodes.Count == 0)
            {
                missing.Add(configuration);
                continue;
            }
            if (nodes.Count > 1)
                several.Add((configuration, nodes.Count));
            // The scope at the node is read against the compilation's types, which are the primary version's
            if (_game.IsPrimary(configuration) && nodes[0] is XmlElement target)
            {
                var inside = patch.InsertType is null or "Child" || patch.IsSetAttribute;
                targets.Add(new PatchTarget(configuration, target.Name, Resolver(configuration).ScopesAt(prefab, target, inside)));
            }
        }
        if (present.Count == 0)
            return null;

        if (missing.Count > 0 && !InsertedByTheMod(patch, xpath))
            Report(report, Descriptors.XPathMatchesNothing, patch.XPathLocation, present, missing, where => [xpath, patch.Movie, where]);
        if (several.Count > 0)
            Report(report, Descriptors.XPathMatchesSeveral, patch.XPathLocation, present, several.Select(x => x.Configuration).ToList(), where => [xpath, several[0].Count, patch.Movie, where]);
        return targets;
    }

    /// <summary>
    /// Reports a finding that holds in <paramref name="holds"/> of the configurations that have the prefab. Where it
    /// holds is named only as far as it is not everywhere: the DLC configurations of a version, the versions of the
    /// set. Holding for some versions only, it is UIX0024, carrying the rule's message and the rule in its properties.
    /// </summary>
    private void Report(Action<Diagnostic> report, DiagnosticDescriptor descriptor, Location location,
        IReadOnlyList<GameConfiguration> present, IReadOnlyList<GameConfiguration> holds, Func<string, object[]> arguments)
    {
        var presentVersions = Versions(present);
        var holdVersions = Versions(holds);
        if (!_game.Versions.Reports(holdVersions))
            return;

        // Each version it holds for: alone when it holds in all of that version's configurations, else with which
        string Part(string version)
        {
            var inVersion = present.Count(x => Same(x.Version, version));
            var holdsIn = holds.Where(x => Same(x.Version, version)).ToList();
            var configurations = string.Join(", ", holdsIn.Select(x => x.Describe(withDlcName: true)));
            return holdsIn.Count == inVersion ? version : presentVersions.Count == 1 ? configurations : $"{version} {configurations}";
        }

        if (presentVersions.Count <= 1 || holdVersions.Count == presentVersions.Count)
        {
            var where = holds.Count == present.Count ? "" : $" ({Join(holdVersions, presentVersions, Part)})";
            report(Diagnostic.Create(descriptor, location, arguments(where)));
            return;
        }

        var message = Diagnostic.Create(descriptor, location, arguments("")).GetMessage(System.Globalization.CultureInfo.InvariantCulture);
        report(Diagnostic.Create(Descriptors.HoldsForSomeVersions, location, FixData.Of((FixData.Rule, descriptor.Id)),
            Join(holdVersions, presentVersions, Part), message));
    }

    /// <summary>
    /// The versions a finding holds for, each as <paramref name="part"/> names it. Three or more that follow each other
    /// among the versions checked, each holding in all its configurations, read as a range: v1.0.0 to v1.3.15.
    /// </summary>
    private static string Join(IReadOnlyList<string> holds, IReadOnlyList<string> checkedVersions, Func<string, string> part)
    {
        var parts = new List<string>();
        for (var i = 0; i < holds.Count;)
        {
            var j = i;
            while (j + 1 < holds.Count && part(holds[j + 1]) == holds[j + 1] && part(holds[j]) == holds[j]
                   && IndexOf(checkedVersions, holds[j + 1]) == IndexOf(checkedVersions, holds[j]) + 1)
                j++;
            if (j - i >= 2)
                parts.Add($"{holds[i]} to {holds[j]}");
            else
                parts.AddRange(holds.Skip(i).Take(j - i + 1).Select(part));
            i = j + 1;
        }
        return string.Join(", ", parts);
    }

    private static int IndexOf(IReadOnlyList<string> versions, string version)
    {
        for (var i = 0; i < versions.Count; i++)
        {
            if (Same(versions[i], version))
                return i;
        }
        return -1;
    }

    private static List<string> Versions(IEnumerable<GameConfiguration> configurations) =>
        configurations.Select(x => x.Version).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, GameVersions.Comparer).ToList();

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private const System.Xml.Linq.SaveOptions SaveOptionsNone = System.Xml.Linq.SaveOptions.None;

    /// <summary>
    /// Whether the XPath selects a node in the XML one of this mod's patches inserts into the same prefab: a patch can
    /// build on what another inserted before it. Another patch changing the prefab in a way the build cannot read may have
    /// inserted it too.
    /// </summary>
    private bool InsertedByTheMod(PrefabPatch patch, string xpath)
    {
        if (_sources.UnreadChanges.Any(x => x.Movie == patch.Movie && !SymbolEqualityComparer.Default.Equals(x.Type, patch.Type)))
            return true;

        foreach (var other in _sources.Patches)
        {
            if (ReferenceEquals(other, patch) || other.Movie != patch.Movie)
                continue;
            foreach (var (xml, _) in other.Contents)
            {
                if (xml.Document?.Root is { } root && Load("<UIExtenderExPatchContent>" + root.ToString(SaveOptionsNone) + "</UIExtenderExPatchContent>") is { } document && Count(document, xpath) > 0)
                    return true;
            }
        }
        return false;
    }

    private static int Count(XmlDocument document, string xpath) => document.SelectNodes(xpath)?.Count ?? 0;

    /// <summary>
    /// A document loaded as <c>WidgetPrefab.LoadFrom</c> loads a prefab: without comments, and, as a default
    /// <see cref="XmlDocument"/> does, without whitespace-only text. So an XPath counts the nodes the game counts.
    /// </summary>
    private static XmlDocument? Load(string text)
    {
        try
        {
            var document = new XmlDocument();
            using var reader = XmlReader.Create(new System.IO.StringReader(text), new XmlReaderSettings { IgnoreComments = true });
            document.Load(reader);
            return document;
        }
        catch (XmlException)
        {
            return null;
        }
    }
}

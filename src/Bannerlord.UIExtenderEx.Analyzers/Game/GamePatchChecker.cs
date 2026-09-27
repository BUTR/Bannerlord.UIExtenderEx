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
/// than one node (UIX0021), and hands back where the patch lands, so its XML can be checked against the game's scope
/// there.
/// </summary>
internal sealed class GamePatchChecker
{
    private readonly IReadOnlyList<GameConfiguration> _configurations;
    private readonly List<GameScopeResolver> _resolvers;
    private readonly PrefabSources _sources;
    private readonly CancellationToken _cancellation;

    public GamePatchChecker(IReadOnlyList<GameConfiguration> configurations, PrefabSources sources, CancellationToken cancellation)
    {
        _configurations = configurations;
        _resolvers = configurations.Select(x => new GameScopeResolver(x, cancellation)).ToList();
        _sources = sources;
        _cancellation = cancellation;
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
        var missing = new List<GameConfiguration>();
        var present = 0;
        (int Count, GameConfiguration Configuration)? several = null;
        for (var i = 0; i < _configurations.Count; i++)
        {
            _cancellation.ThrowIfCancellationRequested();
            var configuration = _configurations[i];
            if (configuration.Prefab(patch.Movie) is not { } prefab || prefab.Document(_cancellation) is not { } document)
                continue;
            present++;

            var nodes = document.SelectNodes(xpath)?.OfType<XmlNode>().ToList() ?? [];
            if (nodes.Count == 0)
            {
                missing.Add(configuration);
                continue;
            }
            if (nodes.Count > 1 && several is null)
                several = (nodes.Count, configuration);
            if (nodes[0] is XmlElement target)
            {
                var inside = patch.InsertType is null or "Child" || patch.IsSetAttribute;
                targets.Add(new PatchTarget(configuration, target.Name, _resolvers[i].ScopesAt(prefab, target, inside)));
            }
        }
        if (present == 0)
            return null;

        if (missing.Count > 0 && !InsertedByTheMod(patch, xpath))
        {
            var where = missing.Count == present ? "" : $" ({string.Join(", ", missing.Select(x => x.Describe(withDlcName: true)))})";
            report(Diagnostic.Create(Descriptors.XPathMatchesNothing, patch.XPathLocation, xpath, patch.Movie, where));
        }
        if (several is { } s)
        {
            var where = present == 1 ? "" : $" ({s.Configuration.Describe(withDlcName: true)})";
            report(Diagnostic.Create(Descriptors.XPathMatchesSeveral, patch.XPathLocation, xpath, s.Count, patch.Movie, where));
        }
        return targets;
    }

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

    private static XmlDocument? Load(string text)
    {
        try
        {
            var document = new XmlDocument();
            document.LoadXml(text);
            return document;
        }
        catch (XmlException)
        {
            return null;
        }
    }
}

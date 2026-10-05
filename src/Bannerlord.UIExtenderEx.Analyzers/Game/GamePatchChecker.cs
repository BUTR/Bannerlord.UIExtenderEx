using Bannerlord.UIExtenderEx.Analyzers.Prefabs;

using Microsoft.CodeAnalysis;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.XPath;

namespace Bannerlord.UIExtenderEx.Analyzers.Game;

/// <summary>Represents the resolution target of a patch within a specific game configuration, including selected node tag and active scopes.</summary>
internal sealed record PatchTarget(GameConfiguration Configuration, string Tag, IReadOnlyList<GameScope> Scopes);

/// <summary>
/// Evaluates patch XPath expressions against target prefab documents, mirroring the runtime resolution of <c>PrefabComponent.RegisterPatch</c>.
/// Detects and reports XPath expressions that match zero nodes (UIX0020) or multiple nodes (UIX0021) across all evaluated game configurations.
/// <para>
/// When a diagnostic applies across all evaluated game versions, it is reported under its primary rule ID.
/// When it applies only to a subset of versions, it is reported under UIX0024 with the affected version range specified.
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
    /// Validates an XPath expression for syntax errors and return type, verifying that it evaluates to a node set.
    /// Returns an error message if invalid, or <see langword="null"/> if compilation succeeds.
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
    /// Evaluates a patch's XPath across the target prefab: resolves against the mod's own prefab if present,
    /// or against vanilla game prefabs across all configured game versions. Returns the resolved patch targets.
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
            // Evaluates target node in each configuration to resolve active scopes from types.json.
            if (nodes[0] is XmlElement target)
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
    /// Emits a diagnostic for a finding that occurs in <paramref name="holds"/> configurations out of <paramref name="present"/>.
    /// If the finding affects only a subset of versions, it is emitted as UIX0024 specifying the applicable version range.
    /// </summary>
    private void Report(Action<Diagnostic> report, DiagnosticDescriptor descriptor, Location location,
        IReadOnlyList<GameConfiguration> present, IReadOnlyList<GameConfiguration> holds, Func<string, object[]> arguments)
    {
        var presentVersions = Versions(present);
        var holdVersions = Versions(holds);
        if (!_game.Versions.Reports(holdVersions))
            return;

        // Formats each affected version: standalone if all configurations are affected, otherwise annotated with DLC details.
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
    /// Formats a list of game versions into a readable string, collapsing contiguous sequences of three or more versions into ranges (e.g. "v1.0.0 to v1.3.15").
    /// </summary>
    internal static string Join(IReadOnlyList<string> holds, IReadOnlyList<string> checkedVersions, Func<string, string> part)
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
    /// Determines whether the XPath targets a node introduced by another patch within the same mod,
    /// accounting for chained prefab modifications.
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
    /// Parses XML text into an <see cref="XmlDocument"/> with whitespace and comment settings identical to <c>WidgetPrefab.LoadFrom</c>.
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
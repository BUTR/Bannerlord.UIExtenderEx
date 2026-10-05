using Microsoft.CodeAnalysis.Diagnostics;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers.Game;

/// <summary>
/// Represents the game versions targeted by the current compilation, read from compiler-visible MSBuild properties:
/// the compilation target version (<see cref="GameVersionTag"/>), the supported version list (<c>UIExtenderExGameVersions</c>,
/// derived from <c>supported-game-versions.txt</c>), and multi-target iteration flags (<c>OverrideGameVersion</c>).
/// </summary>
internal sealed class GameVersions
{
    public static readonly IComparer<string> Comparer = new VersionComparer();

    /// <summary>Gets the game version targeted by the current compilation (e.g. <c>v1.4.8</c>), or <see langword="null"/> if unspecified.</summary>
    public string? Current { get; }

    /// <summary>Gets the list of game versions explicitly supported by the mod project.</summary>
    public IReadOnlyList<string> Supported { get; }

    /// <summary>
    /// Indicates whether the current compilation is one iteration of a multi-version build matrix (e.g. <c>BuildModuleTask</c>).
    /// Prevents duplicate diagnostic reporting across matrix builds by reporting version-specific findings only in the newest matching build.
    /// </summary>
    public bool IsOneOfSeveralBuilds { get; }

    public GameVersions(string? current, IReadOnlyList<string> supported, bool isOneOfSeveralBuilds)
    {
        Current = Normalize(current);
        Supported = supported.Select(Normalize).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        IsOneOfSeveralBuilds = isOneOfSeveralBuilds;
    }

    public static GameVersions Of(AnalyzerOptions options)
    {
        var global = options.AnalyzerConfigOptionsProvider.GlobalOptions;
        var supported = global.TryGetValue("build_property.UIExtenderExGameVersions", out var list)
            ? list.Split([';', ',', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            : [];
        var loop = global.TryGetValue("build_property.OverrideGameVersion", out var over) && !string.IsNullOrWhiteSpace(over);
        return new GameVersions(GameVersionTag.Of(options), supported, loop);
    }

    /// <summary>
    /// Determines whether the current compilation should report a diagnostic that holds for the specified versions.
    /// In multi-target matrix builds, reports only during the build matching the newest applicable version.
    /// </summary>
    public bool Reports(IEnumerable<string> holdsFor) =>
        !IsOneOfSeveralBuilds || Current is null || string.Equals(holdsFor.OrderBy(x => x, Comparer).LastOrDefault(), Current, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Normalizes version strings to standard format (e.g. <c>1.4.8</c> and <c>v1.4.8</c> become <c>v1.4.8</c>; Early Access <c>e1.8.1</c> preserves prefix).
    /// </summary>
    public static string? Normalize(string? version)
    {
        if (version?.Trim() is not { Length: > 0 } trimmed)
            return null;
        if (trimmed.Length > 1 && trimmed[0] is 'v' or 'V' or 'e' or 'E' && char.IsDigit(trimmed[1]))
            return char.ToLowerInvariant(trimmed[0]) + trimmed.Substring(1);
        return "v" + trimmed;
    }

    /// <summary>Compares version strings numerically by component (e.g. <c>v1.2.10</c> sorts after <c>v1.2.9</c>).</summary>
    private sealed class VersionComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            var a = Parts(x);
            var b = Parts(y);
            for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                var left = i < a.Length ? a[i] : "0";
                var right = i < b.Length ? b[i] : "0";
                var result = int.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out var l) && int.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out var r)
                    ? l.CompareTo(r)
                    : string.CompareOrdinal(left, right);
                if (result != 0)
                    return result;
            }
            return 0;
        }

        private static string[] Parts(string? version) => (version ?? "").TrimStart('v', 'V', 'e', 'E').Split('.', '-');
    }
}
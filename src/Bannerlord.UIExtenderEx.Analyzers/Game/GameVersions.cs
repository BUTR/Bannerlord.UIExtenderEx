using Microsoft.CodeAnalysis.Diagnostics;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers.Game;

/// <summary>
/// The game versions a build is about, from the build properties the analyzer targets make compiler-visible: the one it
/// compiles against (<see cref="GameVersionTag"/>), the ones the mod supports (<c>UIExtenderExGameVersions</c>, which the
/// targets fill from <c>Bannerlord.BUTRModule.Sdk</c>'s <c>supported-game-versions.txt</c>), and whether this is one of
/// the SDK's builds per version (<c>OverrideGameVersion</c>).
/// </summary>
internal sealed class GameVersions
{
    public static readonly IComparer<string> Comparer = new VersionComparer();

    /// <summary>The version the compilation builds against, <c>v1.4.8</c>; null when the project sets none.</summary>
    public string? Current { get; }

    /// <summary>The versions the mod supports, as the project lists them; empty when it lists none.</summary>
    public IReadOnlyList<string> Supported { get; }

    /// <summary>
    /// A build of <c>BuildModuleTask</c>'s loop, which builds the module once per supported version. Each of those builds
    /// sees every version, so a finding is reported by one of them only: the build of the newest version it holds for.
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
    /// Whether this build reports a finding that holds for these versions: always, but in one of the SDK's builds per
    /// version, only the build of the newest of them, so the finding is reported once across the builds.
    /// </summary>
    public bool Reports(IEnumerable<string> holdsFor) =>
        !IsOneOfSeveralBuilds || Current is null || string.Equals(holdsFor.OrderBy(x => x, Comparer).LastOrDefault(), Current, StringComparison.OrdinalIgnoreCase);

    /// <summary><c>1.4.8</c> and <c>v1.4.8</c> as <c>v1.4.8</c>; null for nothing.</summary>
    public static string? Normalize(string? version)
    {
        if (version?.Trim() is not { Length: > 0 } trimmed)
            return null;
        return trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? "v" + trimmed.Substring(1) : "v" + trimmed;
    }

    /// <summary>By each number in turn, v1.2.10 after v1.2.9; a part that is no number compares as text.</summary>
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

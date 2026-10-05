using System;
using System.Collections.Generic;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers;

/// <summary>
/// Provides fuzzy string distance matching and candidate suggestion ranking for misspelled identifiers and attributes.
/// </summary>
internal static class Suggestions
{
    private const int Max = 3;

    /// <summary>
    /// Selects candidates within a threshold edit distance of <paramref name="name"/> (case-insensitive), ordered by
    /// ascending distance and alphabetical order, prioritizing case-only discrepancies. Excludes exact matches of <paramref name="name"/>.
    /// </summary>
    public static IEnumerable<string> Closest(string name, IEnumerable<string> candidates)
    {
        var allowed = name.Length <= 3 ? 1 : name.Length <= 6 ? 2 : 3;
        return candidates
            .Where(c => c.Length > 0 && !string.Equals(c, name, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Select(c => (Candidate: c, Distance: Distance(name.ToLowerInvariant(), c.ToLowerInvariant())))
            .Where(x => x.Distance <= allowed)
            .OrderBy(x => x.Distance)
            .ThenBy(x => x.Candidate, StringComparer.Ordinal)
            .Take(Max)
            .Select(x => x.Candidate)
            .ToList();
    }

    /// <summary>
    /// Calculates the Damerau-Levenshtein edit distance between strings, treating adjacent character transpositions as a single edit.
    /// </summary>
    private static int Distance(string a, string b)
    {
        if (Math.Abs(a.Length - b.Length) > 3)
            return int.MaxValue;
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++)
            d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++)
            d[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
            }
        }
        return d[a.Length, b.Length];
    }
}
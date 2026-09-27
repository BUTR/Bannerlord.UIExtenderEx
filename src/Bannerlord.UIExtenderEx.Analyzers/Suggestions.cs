using System;
using System.Collections.Generic;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers;

/// <summary>The names close enough to a misspelled one to offer in its place.</summary>
internal static class Suggestions
{
    private const int Max = 3;

    /// <summary>
    /// The candidates within a few edits of <paramref name="name"/>, ignoring case, closest first; one differing only in
    /// case comes before any other. <paramref name="name"/> itself is left out.
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

    /// <summary>Edits between two strings, a swap of two neighbours counting as one.</summary>
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

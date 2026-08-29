using System;
using System.Collections.Generic;

namespace AnvilMap.Generator;

// Cheap typo-detection for diagnostic messages (currently just AM001, Q1 in
// docs/roadmapv4.md) - not a general string-similarity library, deliberately just a small
// case-insensitive Levenshtein distance with a threshold, no external dependency.
internal static class NameSuggestion
{
    // Only worth suggesting a name close enough that it's plausibly the same typo'd property,
    // not just the least-different name in an unrelated set - two bounds, both needed: an
    // absolute cap (3) so long names don't accumulate an implausibly large "close enough"
    // distance, and a relative cap (distance must be smaller than the candidate's own length) so
    // a short name isn't suggested on what's really a coin flip.
    public static string? FindClosest(string name, IEnumerable<string> candidates)
    {
        string? best = null;
        var bestDistance = int.MaxValue;

        foreach (var candidate in candidates)
        {
            var distance = LevenshteinDistance(name, candidate);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best is not null && bestDistance > 0 && bestDistance <= 3 && bestDistance < best.Length
            ? best
            : null;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var lengthA = a.Length;
        var lengthB = b.Length;
        var distances = new int[lengthA + 1, lengthB + 1];

        for (var i = 0; i <= lengthA; i++)
        {
            distances[i, 0] = i;
        }

        for (var j = 0; j <= lengthB; j++)
        {
            distances[0, j] = j;
        }

        for (var i = 1; i <= lengthA; i++)
        {
            for (var j = 1; j <= lengthB; j++)
            {
                var cost = char.ToUpperInvariant(a[i - 1]) == char.ToUpperInvariant(b[j - 1]) ? 0 : 1;

                distances[i, j] = Math.Min(
                    Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                    distances[i - 1, j - 1] + cost);
            }
        }

        return distances[lengthA, lengthB];
    }
}

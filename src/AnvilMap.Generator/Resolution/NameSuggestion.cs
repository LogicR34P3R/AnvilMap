using System;
using System.Collections.Generic;

namespace AnvilMap.Generator;

// Cheap case-insensitive Levenshtein distance for AM001's "did you mean" hint.
internal static class NameSuggestion
{
    // Absolute cap (3) plus a relative one (distance < candidate length) so a short name isn't
    // suggested on what's really a coin flip.
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

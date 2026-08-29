using System;
using System.Text;

namespace AnvilMap.Generator;

// Thread-static single-slot pool, same pattern as .NET's own internal StringBuilderCache.
internal static class StringBuilderCache
{
    private const int MaxCachedCapacity = 8192;

    [ThreadStatic]
    private static StringBuilder? _cached;

    public static StringBuilder Acquire()
    {
        var sb = _cached;

        if (sb is null)
        {
            return new StringBuilder();
        }

        _cached = null;
        sb.Clear();
        return sb;
    }

    public static string GetStringAndRelease(StringBuilder sb)
    {
        var result = sb.ToString();

        if (sb.Capacity <= MaxCachedCapacity)
        {
            _cached = sb;
        }

        return result;
    }
}

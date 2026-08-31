using System.Collections.Generic;

namespace AnvilMap.Generator;

// netstandard2.0 has no System.HashCode - this is that combiner, used by the handful of
// records (MappingDeclaration, ExplicitIncludeMapping) that hand-write Equals/GetHashCode to
// keep raw INamedTypeSymbol fields out of their equality (see MappingDeclaration.cs's own
// comment for why).
internal static class HashCombine
{
    public static int Combine(params object?[] values)
    {
        unchecked
        {
            var hash = 17;
            foreach (var value in values)
            {
                hash = hash * 31 + (value?.GetHashCode() ?? 0);
            }

            return hash;
        }
    }

    public static int CombineSequence<T>(IEnumerable<T>? values)
    {
        unchecked
        {
            var hash = 17;
            if (values is not null)
            {
                foreach (var value in values)
                {
                    hash = hash * 31 + (value?.GetHashCode() ?? 0);
                }
            }

            return hash;
        }
    }
}

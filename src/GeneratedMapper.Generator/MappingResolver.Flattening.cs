using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace GeneratedMapper.Generator;

// Naming-convention flattening: when a destination property has no exact-name source match (and
// no explicit [MapProperty] override), tries splitting its name at PascalCase boundaries against
// a chain of nested property accesses - e.g. HomeAddressCity -> source.HomeAddress.City. A
// fallback for the default name-matching path only; an explicit [MapProperty] override instead
// walks a dotted source name it's given literally (TryResolveExplicitPath below) - no PascalCase
// search needed there, since every segment is already spelled out. Both share the same
// deliberately conservative rule: every non-terminal segment in a candidate path must be
// non-nullable, so the emitted chain (source.A.B.C) never needs a null-guard - a genuinely
// nullable intermediate segment is excluded rather than attempting `?.` chaining (which would
// also need a fallback for a non-nullable destination leaf). Ambiguous PascalCase matches (more
// than one valid split) are surfaced as GM010 and left unmapped rather than guessing - same
// fail-closed posture as TryMatchConstructor.
internal static partial class MappingResolver
{
    // Returns the unique matching property chain (root-to-leaf, length >= 2), or null if zero
    // paths were found. `ambiguous` is true when more than one distinct path was found - the
    // caller reports GM010 for that case instead of falling through to GM001.
    internal static IReadOnlyList<IPropertySymbol>? TryResolveFlattenedPath(
        ITypeSymbol sourceType,
        string destinationPropertyName,
        out bool ambiguous)
    {
        var results = new List<List<IPropertySymbol>>();
        FindPaths(sourceType, destinationPropertyName, new List<IPropertySymbol>(), results);

        if (results.Count == 1)
        {
            ambiguous = false;
            return results[0];
        }

        ambiguous = results.Count > 1;
        return null;
    }

    // Walks an explicit [MapProperty] dotted source name (e.g. "HomeAddress.City") segment by
    // segment, by exact name - unlike TryResolveFlattenedPath above, every segment is already
    // given literally, so there's no PascalCase split point to search for and never more than one
    // possible path. Returns null with a specific `failureReason` (surfaced as GM021) the moment
    // any segment doesn't resolve, rather than silently falling through to a generic "not found".
    private static IReadOnlyList<IPropertySymbol>? TryResolveExplicitPath(
        ITypeSymbol sourceType,
        string dottedSourceName,
        out string? failureReason)
    {
        var segments = dottedSourceName.Split('.');
        var path = new List<IPropertySymbol>(segments.Length);
        var currentType = sourceType;

        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            var property = currentType.GetMembers(segment)
                .OfType<IPropertySymbol>()
                .FirstOrDefault(p => !p.IsStatic && p.GetMethod is not null);

            if (property is null)
            {
                failureReason = $"'{currentType.ToDisplayString()}' has no accessible property called '{segment}'";
                return null;
            }

            var isLast = i == segments.Length - 1;

            if (!isLast && IsNullableSegment(property.Type))
            {
                failureReason = $"'{segment}' is nullable, so the chain can't continue through it safely - only the last segment in a [MapProperty] source path may be nullable";
                return null;
            }

            path.Add(property);
            currentType = property.Type;
        }

        failureReason = null;
        return path;
    }

    // Depth-first search over PascalCase split points. `remainingName` always strictly shrinks
    // by at least one character per recursive call (i >= 1), so this terminates regardless of
    // cycles in the property graph itself (e.g. Address.Owner : Person, Person.Address : Address)
    // - it's bounded by string length, not by the type graph's shape.
    private static void FindPaths(
        ITypeSymbol currentType,
        string remainingName,
        List<IPropertySymbol> currentPath,
        List<List<IPropertySymbol>> results)
    {
        for (var i = 1; i <= remainingName.Length; i++)
        {
            // A valid split point is either the end of the name, or a position where the next
            // character starts a new PascalCase word - otherwise `prefix` couldn't stand alone
            // as a property name.
            if (i < remainingName.Length && !char.IsUpper(remainingName[i]))
            {
                continue;
            }

            var prefix = remainingName.Substring(0, i);

            var property = currentType.GetMembers(prefix)
                .OfType<IPropertySymbol>()
                .FirstOrDefault(p => !p.IsStatic && p.GetMethod is not null);

            if (property is null)
            {
                continue;
            }

            if (i == remainingName.Length)
            {
                currentPath.Add(property);
                results.Add(new List<IPropertySymbol>(currentPath));
                currentPath.RemoveAt(currentPath.Count - 1);
                continue;
            }

            // An intermediate (non-terminal) segment must be non-nullable - see file header.
            if (IsNullableSegment(property.Type))
            {
                continue;
            }

            currentPath.Add(property);
            FindPaths(property.Type, remainingName.Substring(i), currentPath, results);
            currentPath.RemoveAt(currentPath.Count - 1);
        }
    }

    private static bool IsNullableSegment(ITypeSymbol type)
        => type.NullableAnnotation == NullableAnnotation.Annotated
            || (type.IsValueType && type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);
}

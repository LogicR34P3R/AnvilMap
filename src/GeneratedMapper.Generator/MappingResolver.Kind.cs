using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace GeneratedMapper.Generator;

// Type-compatibility resolution: what PropertyMappingKind (if any) connects a source and
// destination property type, and - for Enumerable - what collection shape to materialize.
internal static partial class MappingResolver
{
    // Checked in order: identical type -> direct; enumerable with matching/mapped element
    // types -> Enumerable; same named type with a declared mapping -> Nested; otherwise an
    // implicit conversion -> direct. Enumerable/Nested need `graph`, not just `declaration`,
    // since the element/property type may have its own [MapTo] declared elsewhere.
    private static KindResolution ResolveKind(
        Compilation compilation,
        MappingGraph graph,
        ITypeSymbol source,
        ITypeSymbol destination)
    {
        if (SymbolEqualityComparer.Default.Equals(source, destination))
        {
            return new KindResolution(PropertyMappingKind.Direct, null, null);
        }

        if (TryGetEnumerableElement(source, out var sourceElement) &&
            TryGetEnumerableElement(destination, out var destinationElement))
        {
            var destinationShape = DetermineCollectionShape(destination);

            if (SymbolEqualityComparer.Default.Equals(sourceElement, destinationElement))
            {
                return new KindResolution(PropertyMappingKind.Enumerable, sourceElement, destinationElement, destinationShape);
            }

            if (TryGetNamedType(sourceElement, out var sourceElementNamed) &&
                TryGetNamedType(destinationElement, out var destinationElementNamed) &&
                graph.TryGetMapping(
                    sourceElementNamed.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    destinationElementNamed.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    out _))
            {
                return new KindResolution(PropertyMappingKind.Enumerable, sourceElement, destinationElement, destinationShape);
            }
        }

        if (TryGetNamedType(source, out var sourceNamedType) &&
            TryGetNamedType(destination, out var destinationNamedType) &&
            graph.TryGetMapping(
                sourceNamedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                destinationNamedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                out _))
        {
            return new KindResolution(PropertyMappingKind.Nested, null, null);
        }

        if (compilation is CSharpCompilation csharpCompilation)
        {
            var conversion = csharpCompilation.ClassifyConversion(source, destination);
            if (conversion.IsImplicit)
            {
                return new KindResolution(PropertyMappingKind.Direct, null, null);
            }
        }

        return new KindResolution(null, null, null);
    }

    // `null` Kind means "no strategy matched" - ResolveKind's caller turns that into GM003.
    private readonly record struct KindResolution(
        PropertyMappingKind? Kind,
        ITypeSymbol? ElementSource,
        ITypeSymbol? ElementDestination,
        CollectionShape DestinationShape = CollectionShape.List);

    // Only Array/HashSet need a non-default materialize call; everything else falls back to List.
    private static CollectionShape DetermineCollectionShape(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol)
        {
            return CollectionShape.Array;
        }

        if (type is INamedTypeSymbol { IsGenericType: true } named &&
            named.Name is "HashSet" or "ISet" or "IReadOnlySet")
        {
            return CollectionShape.HashSet;
        }

        foreach (var iface in type.AllInterfaces)
        {
            if (iface.Name is "ISet" or "IReadOnlySet")
            {
                return CollectionShape.HashSet;
            }
        }

        return CollectionShape.List;
    }

    private static bool TryGetNamedType(ITypeSymbol type, out INamedTypeSymbol named)
    {
        if (type is INamedTypeSymbol n)
        {
            named = n;
            return true;
        }

        named = null!;
        return false;
    }

    // Named BCL collection types first, then a fallback scan of implemented interfaces for
    // IEnumerable<T>, so custom collection types still resolve.
    private static bool TryGetEnumerableElement(ITypeSymbol type, out ITypeSymbol element)
    {
        if (type is IArrayTypeSymbol array)
        {
            element = array.ElementType;
            return true;
        }

        if (type is INamedTypeSymbol { IsGenericType: true } named &&
            named.TypeArguments.Length == 1 &&
            (named.Name is "IEnumerable" or "ICollection" or "IList" or "List"
                or "IReadOnlyCollection" or "IReadOnlyList"
                or "HashSet" or "ISet" or "IReadOnlySet"))
        {
            element = named.TypeArguments[0];
            return true;
        }

        foreach (var iface in type.AllInterfaces)
        {
            if (iface.Name == "IEnumerable" && iface.TypeArguments.Length == 1)
            {
                element = iface.TypeArguments[0];
                return true;
            }
        }

        element = null!;
        return false;
    }
}

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AnvilMap.Generator;

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
            TryGetEnumerableElement(destination, out var destinationElement) &&
            DetermineCollectionShape(compilation, destinationElement, destination) is { } destinationShape)
        {
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

        // [MapTo]/[MapFrom] can't target an enum (AttributeTargets.Struct excludes it), so an
        // enum source never has a graph entry of its own - these two are checked ahead of the
        // general implicit-conversion fallback below only for clarity, not to avoid a conflict:
        // both are explicit conversions in C#'s own rules (ClassifyConversion never reports
        // IsImplicit for either), so the fallback would never have matched them anyway.
        if (source is INamedTypeSymbol { TypeKind: TypeKind.Enum } sourceEnum)
        {
            if (SymbolEqualityComparer.Default.Equals(sourceEnum.EnumUnderlyingType, destination))
            {
                return new KindResolution(PropertyMappingKind.EnumConversion, null, null, EnumConversion: EnumConversionKind.ToUnderlyingType);
            }

            if (destination.SpecialType == SpecialType.System_String)
            {
                return new KindResolution(PropertyMappingKind.EnumConversion, null, null, EnumConversion: EnumConversionKind.ToString);
            }
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

    // `null` Kind means "no strategy matched" - ResolveKind's caller turns that into AM003.
    private readonly record struct KindResolution(
        PropertyMappingKind? Kind,
        ITypeSymbol? ElementSource,
        ITypeSymbol? ElementDestination,
        CollectionShape DestinationShape = CollectionShape.List,
        EnumConversionKind? EnumConversion = null);

    // Null (not a silent List default) when List<elementType> isn't actually assignable to
    // destination - e.g. Dictionary<K,V>/IReadOnlyDictionary<K,V> both pass
    // TryGetEnumerableElement's fallback but don't accept a `.ToList()`. The caller falls
    // through to the rest of ResolveKind instead of emitting code that won't compile.
    private static CollectionShape? DetermineCollectionShape(Compilation compilation, ITypeSymbol elementType, ITypeSymbol destination)
    {
        if (destination is IArrayTypeSymbol)
        {
            return CollectionShape.Array;
        }

        if (destination is INamedTypeSymbol { IsGenericType: true } named)
        {
            switch (named.Name)
            {
                case "HashSet" or "ISet" or "IReadOnlySet":
                    return CollectionShape.HashSet;
                case "ImmutableArray":
                    return CollectionShape.ImmutableArray;
                case "ObservableCollection":
                    return CollectionShape.ObservableCollection;
            }
        }

        foreach (var iface in destination.AllInterfaces)
        {
            if (iface.Name is "ISet" or "IReadOnlySet")
            {
                return CollectionShape.HashSet;
            }
        }

        if (compilation is CSharpCompilation csharpCompilation &&
            compilation.GetTypeByMetadataName("System.Collections.Generic.List`1") is { } listDefinition)
        {
            var listType = listDefinition.Construct(elementType);

            if (csharpCompilation.ClassifyConversion(listType, destination).IsImplicit)
            {
                return CollectionShape.List;
            }
        }

        return null;
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

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace GeneratedMapper.Generator;

// Matches a MappingDeclaration's properties against the destination type, producing a
// MappingModel with one PropertyMappingModel per matched property; unmatched properties
// become diagnostics instead. MappingEmitter never re-validates this.
internal static class MappingResolver
{
    public static MappingModel Resolve(
        Compilation compilation,
        MappingGraph graph,
        MappingDeclaration declaration,
        Action<Diagnostic>? report = null)
    {
        var source = declaration.SourceSymbol;
        var destination = declaration.DestinationSymbol;

        var explicitMappings = declaration.ExplicitProperties
            .ToDictionary(x => x.DestinationProperty, x => x.SourceProperty);

        var explicitConditions = declaration.ExplicitConditions
            .ToDictionary(x => x.DestinationProperty, x => x.ConditionMethodName);

        var explicitConverters = declaration.ExplicitConverters
            .ToDictionary(x => x.DestinationProperty, x => x.ConverterMethodName);

        // Write-only properties (no getter) can't be a mapping source, so excluded here.
        var sourceProperties = source.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => !p.IsStatic && p.GetMethod is not null)
            .ToDictionary(p => p.Name);

        var properties = new List<PropertyMappingModel>();

        // Tries each strategy in order: [MapUsing] converter, name match, kind resolution,
        // then [MapCondition] gate. Unmatched properties are reported (GM001/GM003/GM004/
        // GM009) and left out.
        foreach (var destinationProperty in destination.GetMembers().OfType<IPropertySymbol>())
        {
            if (destinationProperty.IsStatic)
                continue;

            if (destinationProperty.SetMethod is null)
                continue;

            if (destinationProperty.GetAttributes()
                .Any(a => a.AttributeClass?.ToDisplayString() == GeneratorConstants.MapIgnoreAttribute))
                continue;

            if (explicitConverters.TryGetValue(destinationProperty.Name, out var converterMethodName))
            {
                var converter = ResolveConverter(compilation, source, destinationProperty, converterMethodName, report);

                if (converter is null)
                    continue;

                // [MapCondition] can still gate a [MapUsing]-converted property; independent.
                var converterCondition = ResolveCondition(source, destination, destinationProperty, explicitConditions, report);

                if (!converterCondition.Success)
                    continue;

                properties.Add(new PropertyMappingModel(
                    string.Empty,
                    destinationProperty.Name,
                    PropertyMappingKind.Converted,
                    TypeModel.From(destinationProperty.Type),
                    TypeModel.From(destinationProperty.Type),
                    ConditionMethodName: converterCondition.MethodName,
                    ConditionAcceptsDestination: converterCondition.AcceptsDestination,
                    DestinationIsInitOnly: destinationProperty.SetMethod!.IsInitOnly,
                    ConverterMethodName: converter));

                continue;
            }

            var sourceName = explicitMappings.TryGetValue(
                destinationProperty.Name,
                out var explicitSource)
                ? explicitSource
                : destinationProperty.Name;

            if (!sourceProperties.TryGetValue(sourceName, out var sourceProperty))
            {
                report?.Invoke(Diagnostic.Create(
                    Diagnostics.UnmappedDestinationProperty,
                    destinationProperty.Locations.FirstOrDefault() ?? Location.None,
                    destinationProperty.Name,
                    destination.ToDisplayString()));
                continue;
            }

            var resolution = ResolveKind(
                compilation,
                graph,
                sourceProperty.Type,
                destinationProperty.Type);

            if (resolution.Kind is null)
            {
                report?.Invoke(Diagnostic.Create(
                    Diagnostics.IncompatiblePropertyTypes,
                    destinationProperty.Locations.FirstOrDefault() ?? Location.None,
                    source.ToDisplayString(),
                    sourceProperty.Name,
                    sourceProperty.Type.ToDisplayString(),
                    destination.ToDisplayString(),
                    destinationProperty.Name,
                    destinationProperty.Type.ToDisplayString()));
                continue;
            }

            var condition = ResolveCondition(source, destination, destinationProperty, explicitConditions, report);

            if (!condition.Success)
                continue;

            var conditionMethodName = condition.MethodName;
            var conditionAcceptsDestination = condition.AcceptsDestination;

            properties.Add(new PropertyMappingModel(
                sourceProperty.Name,
                destinationProperty.Name,
                resolution.Kind.Value,
                TypeModel.From(sourceProperty.Type),
                TypeModel.From(destinationProperty.Type),
                resolution.ElementSource is null ? null : TypeModel.From(resolution.ElementSource),
                resolution.ElementDestination is null ? null : TypeModel.From(resolution.ElementDestination),
                sourceProperty.Type.NullableAnnotation == NullableAnnotation.Annotated
                    || sourceProperty.Type.IsValueType && sourceProperty.Type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T,
                conditionMethodName,
                conditionAcceptsDestination,
                destinationProperty.SetMethod!.IsInitOnly,
                resolution.DestinationShape));
        }

        // Consumed by MappingEmitter: init-only property + no parameterless constructor (e.g.
        // a positional record) means the mapping can't be built at all (GM006) - constructor
        // parameter matching isn't implemented.
        var hasParameterlessConstructor = destination.InstanceConstructors
            .Any(c => c.Parameters.Length == 0);

        return new MappingModel(
            declaration.Source,
            declaration.Destination,
            properties,
            hasParameterlessConstructor,
            declaration.MaxDepth);
    }

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
            return new KindResolution(PropertyMappingKind.Direct, null, null);

        if (TryGetEnumerableElement(source, out var sourceElement) &&
            TryGetEnumerableElement(destination, out var destinationElement))
        {
            var destinationShape = DetermineCollectionShape(destination);

            if (SymbolEqualityComparer.Default.Equals(sourceElement, destinationElement))
                return new KindResolution(PropertyMappingKind.Enumerable, sourceElement, destinationElement, destinationShape);

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
                return new KindResolution(PropertyMappingKind.Direct, null, null);
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
            return CollectionShape.Array;

        if (type is INamedTypeSymbol { IsGenericType: true } named &&
            named.Name is "HashSet" or "ISet" or "IReadOnlySet")
            return CollectionShape.HashSet;

        foreach (var iface in type.AllInterfaces)
        {
            if (iface.Name is "ISet" or "IReadOnlySet")
                return CollectionShape.HashSet;
        }

        return CollectionShape.List;
    }

    // Prefers the two-arg (TSource, TDestination?) signature over one-arg when both exist, so
    // a condition can inspect the destination's current state.
    private static ConditionResolution ResolveCondition(
        INamedTypeSymbol source,
        INamedTypeSymbol destination,
        IPropertySymbol destinationProperty,
        IReadOnlyDictionary<string, string> explicitConditions,
        Action<Diagnostic>? report)
    {
        if (!explicitConditions.TryGetValue(destinationProperty.Name, out var conditionName))
            return new ConditionResolution(true, null, false);

        var candidates = source.GetMembers(conditionName)
            .OfType<IMethodSymbol>()
            .Where(m => m.IsStatic && m.ReturnType.SpecialType == SpecialType.System_Boolean)
            .ToArray();

        var twoArg = candidates.FirstOrDefault(m =>
            m.Parameters.Length == 2 &&
            SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, source) &&
            SymbolEqualityComparer.Default.Equals(m.Parameters[1].Type, destination));

        if (twoArg is not null)
            return new ConditionResolution(true, conditionName, true);

        var oneArg = candidates.FirstOrDefault(m =>
            m.Parameters.Length == 1 &&
            SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, source));

        if (oneArg is not null)
            return new ConditionResolution(true, conditionName, false);

        report?.Invoke(Diagnostic.Create(
            Diagnostics.ConditionMethodNotFound,
            destinationProperty.Locations.FirstOrDefault() ?? Location.None,
            source.ToDisplayString(),
            destinationProperty.Name,
            conditionName,
            destination.ToDisplayString()));

        return new ConditionResolution(false, null, false);
    }

    // Success = false means GM004 was already reported; caller skips the property.
    private readonly record struct ConditionResolution(
        bool Success,
        string? MethodName,
        bool AcceptsDestination);

    // Prefers an exact return-type match, falling back to an implicit conversion.
    private static string? ResolveConverter(
        Compilation compilation,
        INamedTypeSymbol source,
        IPropertySymbol destinationProperty,
        string converterMethodName,
        Action<Diagnostic>? report)
    {
        var candidates = source.GetMembers(converterMethodName)
            .OfType<IMethodSymbol>()
            .Where(m => m.IsStatic &&
                m.Parameters.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, source))
            .ToArray();

        var match = candidates.FirstOrDefault(m =>
            SymbolEqualityComparer.Default.Equals(m.ReturnType, destinationProperty.Type));

        if (match is null && compilation is CSharpCompilation csharpCompilation)
        {
            match = candidates.FirstOrDefault(m =>
                csharpCompilation.ClassifyConversion(m.ReturnType, destinationProperty.Type).IsImplicit);
        }

        if (match is not null)
            return converterMethodName;

        report?.Invoke(Diagnostic.Create(
            Diagnostics.ConverterMethodNotFound,
            destinationProperty.Locations.FirstOrDefault() ?? Location.None,
            source.ToDisplayString(),
            destinationProperty.Name,
            converterMethodName,
            destinationProperty.Type.ToDisplayString()));

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

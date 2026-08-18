using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace GeneratedMapper.Generator;

// The core property-matching pass. Takes one MappingDeclaration (what the user wrote via
// [MapTo]/[MapProperty]/[MapCondition]/[MapUsing]) plus the full MappingGraph (every other
// declared mapping in the compilation, needed below to resolve nested/enumerable properties)
// and produces a MappingModel: one resolved PropertyMappingModel per destination property
// that could be matched, plus a Diagnostic for every property that couldn't be. MappingEmitter
// never re-derives any of this - by the time a MappingModel exists, every property in it is
// known-emittable.
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

        // Write-only properties (get accessor missing) can't be read as a mapping source, so
        // they're excluded here rather than filtered out property-by-property below.
        var sourceProperties = source.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => !p.IsStatic && p.GetMethod is not null)
            .ToDictionary(p => p.Name);

        var properties = new List<PropertyMappingModel>();

        // For every settable, non-ignored destination property, try each mapping strategy in
        // order: [MapUsing] converter (bypasses source-property lookup entirely - the value
        // comes from a method call, not a property read) → name match ([MapProperty] override
        // or exact name) → type-compatibility ("kind") resolution → optional [MapCondition]
        // gate. Anything that falls through every step is reported as GM001/GM003/GM004/GM009
        // and left out of the mapping (not a build error by itself - see Diagnostics.cs for
        // which of these are Info/Warning/Error).
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

                // [MapCondition] can still gate a [MapUsing]-converted property - the two
                // attributes are independent and compose normally.
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

        // Consumed by MappingEmitter: a destination with any init-only property AND no
        // parameterless constructor can't be built at all (not even via object-initializer
        // syntax, since a positional record's constructor requires its parameters) - that
        // whole mapping is skipped with GM006 rather than emitting code that won't compile.
        // Resolver deliberately does not attempt to match constructor parameters to properties
        // for such destinations; see docs/roadmapv2.md F6 for that still-open gap.
        var hasParameterlessConstructor = destination.InstanceConstructors
            .Any(c => c.Parameters.Length == 0);

        return new MappingModel(
            declaration.Source,
            declaration.Destination,
            properties,
            hasParameterlessConstructor,
            declaration.MaxDepth);
    }

    // Decides *how* a source property becomes a destination property's value, checked in this
    // order because each check is progressively more expensive and more general:
    //   1. Identical type -> straight assignment.
    //   2. Both sides are enumerable of some element type, and those element types either match
    //      exactly or are themselves a declared mapping in the graph -> project/select over the
    //      collection.
    //   3. Both sides are the same named type as a declared mapping in the graph -> recurse into
    //      that type's own generated To{Dest}()/projection.
    //   4. Neither of the above, but the C# compiler would allow an implicit conversion (e.g.
    //      int -> long, or a user-defined implicit operator) -> straight assignment, same as (1).
    // Steps 2 and 3 both need `graph`, not just `declaration`, because the element/property type
    // in question may have its own, entirely separate [MapTo] declared on a different type
    // elsewhere in the compilation - MappingSourceGenerator collects every declaration into one
    // MappingGraph before resolving any of them for exactly this reason.
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

    // Only Array and HashSet-family types need a non-default materialization call
    // (MappingEmitter.MaterializeCall); every other destination collection type falls back to
    // List, which is also what a plain IEnumerable<T>/ICollection<T> destination gets.
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

    // [MapCondition] resolves to one of two accepted static-method shapes, preferring the
    // two-argument `(TSource, TDestination?)` overload when both exist so a condition can
    // inspect the destination's current state (e.g. "only overwrite if still default") - see
    // MapConditionAttribute's doc comment for the exact accepted signatures.
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

    // `Success = false` means the referenced method didn't resolve (GM004 already reported by
    // this point) - the caller skips the property entirely rather than emitting a call to a
    // method that doesn't exist.
    private readonly record struct ConditionResolution(
        bool Success,
        string? MethodName,
        bool AcceptsDestination);

    // [MapUsing] resolves to a static `TDestProp Method(TSource)` method, preferring an exact
    // return-type match and falling back to an implicit conversion (mirrors ResolveKind's
    // direct-assignment fallback) so e.g. a converter returning `int` still satisfies a `long`
    // destination property without needing an exact-type overload.
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

    // Two-tier lookup: a closed list of well-known BCL collection shapes first (fast, and
    // covers the vast majority of real DTOs/entities), then a fallback scan of every
    // implemented interface for a single-type-argument IEnumerable<T> - this second tier is
    // what lets a custom collection type (anything implementing IEnumerable<T> without being
    // named List/HashSet/etc.) still resolve correctly.
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

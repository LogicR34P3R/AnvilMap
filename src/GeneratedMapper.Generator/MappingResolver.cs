using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace GeneratedMapper.Generator;

// Matches a MappingDeclaration's properties against the destination type, producing a
// MappingModel with one PropertyMappingModel per matched property; unmatched properties
// become diagnostics instead. MappingEmitter never re-validates this. Split by concern across
// MappingResolver.Kind.cs (type-compatibility), .Condition.cs ([MapCondition]),
// .Converter.cs ([MapUsing]), and .Flattening.cs (naming-convention flattening) - this file
// holds the orchestrating Resolve loop and constructor-matching only.
internal static partial class MappingResolver
{
    public static MappingModel Resolve(
        Compilation compilation,
        MappingGraph graph,
        MappingDeclaration declaration,
        Action<Diagnostic>? report = null)
    {
        var source = declaration.SourceSymbol;
        var destination = declaration.DestinationSymbol;

        // GroupBy+Last, not ToDictionary: two attributes of the same kind naming the same
        // destination property (AllowMultiple = true on all four) would otherwise throw
        // ArgumentException ("duplicate key") and crash the *entire* generator run for every
        // mapping in the compilation, not just this one. Last-declared wins instead.
        var explicitMappings = declaration.ExplicitProperties
            .GroupBy(x => x.DestinationProperty)
            .ToDictionary(g => g.Key, g => g.Last().SourceProperty);

        var explicitConditions = declaration.ExplicitConditions
            .GroupBy(x => x.DestinationProperty)
            .ToDictionary(g => g.Key, g => g.Last().ConditionMethodName);

        var explicitConverters = declaration.ExplicitConverters
            .GroupBy(x => x.DestinationProperty)
            .ToDictionary(g => g.Key, g => g.Last().ConverterMethodName);

        var explicitDefaults = declaration.ExplicitDefaults
            .GroupBy(x => x.DestinationProperty)
            .ToDictionary(g => g.Key, g => g.Last().DefaultValueLiteral);

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

                var converterDefault = explicitDefaults.TryGetValue(destinationProperty.Name, out var converterDefaultLiteral)
                    && CanCoalesceNull(destinationProperty.Type)
                        ? converterDefaultLiteral
                        : null;

                properties.Add(new PropertyMappingModel(
                    string.Empty,
                    destinationProperty.Name,
                    PropertyMappingKind.Converted,
                    TypeModel.From(destinationProperty.Type),
                    TypeModel.From(destinationProperty.Type),
                    ConditionMethodName: converterCondition.MethodName,
                    ConditionAcceptsDestination: converterCondition.AcceptsDestination,
                    DestinationIsInitOnly: destinationProperty.SetMethod!.IsInitOnly,
                    ConverterMethodName: converter,
                    DefaultValueLiteral: converterDefault));

                continue;
            }

            var hasExplicitOverride = explicitMappings.TryGetValue(
                destinationProperty.Name,
                out var explicitSource);
            var sourceName = hasExplicitOverride ? explicitSource : destinationProperty.Name;

            IPropertySymbol? sourceProperty = null;
            string? flattenedSourcePath = null;

            if (sourceProperties.TryGetValue(sourceName, out var directMatch))
            {
                sourceProperty = directMatch;
            }
            else if (!hasExplicitOverride)
            {
                // Naming-convention flattening fallback - only for the default name-matching
                // path, never for an explicit [MapProperty] override (that name must still be an
                // exact top-level property name).
                var path = TryResolveFlattenedPath(source, destinationProperty.Name, out var ambiguous);

                if (ambiguous)
                {
                    report?.Invoke(Diagnostic.Create(
                        Diagnostics.AmbiguousFlattenedMapping,
                        destinationProperty.Locations.FirstOrDefault() ?? Location.None,
                        destinationProperty.Name,
                        destination.ToDisplayString()));
                    continue;
                }

                if (path is not null)
                {
                    sourceProperty = path[path.Count - 1];
                    flattenedSourcePath = string.Join(".", path.Select(p => p.Name));
                }
            }

            if (sourceProperty is null)
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
                    flattenedSourcePath ?? sourceProperty.Name,
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

            // Only a Direct match produces a plain value/reference expression a literal can
            // meaningfully `??` against - a Nested/Enumerable value's own type can't be spelled
            // as an attribute constant, so [MapDefault] is silently ignored there.
            var propertyDefault = resolution.Kind == PropertyMappingKind.Direct &&
                explicitDefaults.TryGetValue(destinationProperty.Name, out var defaultLiteral) &&
                CanCoalesceNull(sourceProperty.Type)
                    ? defaultLiteral
                    : null;

            properties.Add(new PropertyMappingModel(
                flattenedSourcePath ?? sourceProperty.Name,
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
                resolution.DestinationShape,
                DefaultValueLiteral: propertyDefault));
        }

        var hasParameterlessConstructor = destination.InstanceConstructors
            .Any(c => c.Parameters.Length == 0);

        // No parameterless constructor (e.g. a positional record): try to find a constructor
        // whose parameters all correspond to properties already resolved above, so
        // MappingEmitter can build `new Dest(args...)` instead of skipping the mapping (GM006).
        var constructorParameterProperties = hasParameterlessConstructor
            ? null
            : TryMatchConstructor(destination, properties);

        return new MappingModel(
            declaration.Source,
            declaration.Destination,
            properties,
            hasParameterlessConstructor,
            declaration.MaxDepth,
            constructorParameterProperties);
    }

    // Only matches a constructor when *every* parameter resolves to an already-mapped,
    // unconditioned property of the exact same type - falling back to null (GM006 skip) on
    // any ambiguity is safer than guessing wrong and emitting code that double-assigns or
    // misses a required argument. Excludes the compiler-synthesized record copy constructor
    // (single parameter of the destination's own type) and prefers the constructor with the
    // most parameters when several match, since that's the positional one for a typical record.
    private static IReadOnlyList<string>? TryMatchConstructor(
        INamedTypeSymbol destination,
        IReadOnlyList<PropertyMappingModel> properties)
    {
        var resolvedByName = properties.ToDictionary(p => p.DestinationPropertyName);

        var candidates = destination.InstanceConstructors
            .Where(c => c.DeclaredAccessibility == Accessibility.Public && c.Parameters.Length > 0)
            .Where(c => !(c.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(c.Parameters[0].Type, destination)))
            .OrderByDescending(c => c.Parameters.Length);

        foreach (var constructor in candidates)
        {
            var parameterNames = new List<string>(constructor.Parameters.Length);
            var isMatch = true;

            foreach (var parameter in constructor.Parameters)
            {
                var property = destination.GetMembers(parameter.Name)
                    .OfType<IPropertySymbol>()
                    .FirstOrDefault(p => !p.IsStatic);

                if (property is null ||
                    !SymbolEqualityComparer.Default.Equals(property.Type, parameter.Type) ||
                    !resolvedByName.TryGetValue(parameter.Name, out var resolved) ||
                    resolved.ConditionMethodName is not null)
                {
                    isMatch = false;
                    break;
                }

                parameterNames.Add(parameter.Name);
            }

            if (isMatch)
                return parameterNames;
        }

        return null;
    }

    // `??` only compiles against a reference type or Nullable<T> - a plain value type (int,
    // a non-nullable struct) can never be null, so [MapDefault] against one is silently ignored
    // rather than emitting code that fails to compile.
    private static bool CanCoalesceNull(ITypeSymbol type)
        => type.IsReferenceType || (type.IsValueType && type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);

    // Compilation.GetTypeByMetadataName's expected format: '+' between nested types, '.'
    // between namespace segments - unlike ToDisplayString, which uses '.' for both. Shared by
    // ResolveCondition and ResolveConverter (MappingResolver.Condition.cs / .Converter.cs) to
    // populate the diagnostic Properties GeneratedMapper.CodeFixes reads.
    private static string GetMetadataName(INamedTypeSymbol type)
    {
        var name = type.MetadataName;

        for (var containing = type.ContainingType; containing is not null; containing = containing.ContainingType)
            name = containing.MetadataName + "+" + name;

        return type.ContainingNamespace is { IsGlobalNamespace: false } ns
            ? ns.ToDisplayString() + "." + name
            : name;
    }
}

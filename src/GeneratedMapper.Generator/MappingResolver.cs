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
        // mapping in the compilation, not just this one. Last-declared wins instead - and
        // ReportDuplicates (GM017) flags the ambiguity rather than resolving it silently.
        var explicitPropertiesGrouped = declaration.ExplicitProperties.GroupBy(x => x.DestinationProperty).ToArray();
        ReportDuplicates(explicitPropertiesGrouped, "[MapProperty]", declaration.MethodHostSymbol, destination, report);
        var explicitMappings = explicitPropertiesGrouped
            .ToDictionary(g => g.Key, g => g.Last().SourceProperty);

        var explicitConditionsGrouped = declaration.ExplicitConditions.GroupBy(x => x.DestinationProperty).ToArray();
        ReportDuplicates(explicitConditionsGrouped, "[MapCondition]", declaration.MethodHostSymbol, destination, report);
        var explicitConditions = explicitConditionsGrouped
            .ToDictionary(g => g.Key, g => g.Last().ConditionMethodName);

        var explicitConvertersGrouped = declaration.ExplicitConverters.GroupBy(x => x.DestinationProperty).ToArray();
        ReportDuplicates(explicitConvertersGrouped, "[MapUsing]", declaration.MethodHostSymbol, destination, report);
        var explicitConverters = explicitConvertersGrouped
            .ToDictionary(g => g.Key, g => g.Last().ConverterMethodName);

        var explicitDefaultsGrouped = declaration.ExplicitDefaults.GroupBy(x => x.DestinationProperty).ToArray();
        ReportDuplicates(explicitDefaultsGrouped, "[MapDefault]", declaration.MethodHostSymbol, destination, report);
        var explicitDefaults = explicitDefaultsGrouped
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
            {
                continue;
            }

            if (destinationProperty.SetMethod is null)
            {
                continue;
            }

            var mapIgnoreAttributes = destinationProperty.GetAttributes()
                .Where(a => a.AttributeClass?.ToDisplayString() == GeneratorConstants.MapIgnoreAttribute)
                .ToArray();

            // No constructor argument means "ignore for every source"; a typeof(...) argument
            // scopes the exclusion to mappings whose source is that exact type, so the same
            // property can be ignored for one source and still mapped normally for another
            // (repeat the attribute once per source to ignore).
            if (mapIgnoreAttributes.Any(a =>
                a.ConstructorArguments.Length == 0 ||
                (a.ConstructorArguments[0].Value is INamedTypeSymbol ignoredSource &&
                    SymbolEqualityComparer.Default.Equals(ignoredSource, source))))
            {
                // The property is always skipped below regardless, but any of these four
                // per-property overrides configured against it for this exact mapping would
                // otherwise be silently dead - MapIgnore wins first, so none of them are ever
                // consulted.
                var deadOverrides = new List<string>(4);

                if (explicitConditions.ContainsKey(destinationProperty.Name))
                {
                    deadOverrides.Add("[MapCondition]");
                }

                if (explicitConverters.ContainsKey(destinationProperty.Name))
                {
                    deadOverrides.Add("[MapUsing]");
                }

                if (explicitDefaults.ContainsKey(destinationProperty.Name))
                {
                    deadOverrides.Add("[MapDefault]");
                }

                if (explicitMappings.ContainsKey(destinationProperty.Name))
                {
                    deadOverrides.Add("[MapProperty]");
                }

                if (deadOverrides.Count > 0)
                {
                    report?.Invoke(Diagnostic.Create(
                        Diagnostics.AttributeOnIgnoredProperty,
                        destinationProperty.Locations.FirstOrDefault() ?? Location.None,
                        destinationProperty.Name,
                        source.ToDisplayString(),
                        destination.ToDisplayString(),
                        string.Join(" and ", deadOverrides)));
                }

                continue;
            }

            if (explicitConverters.TryGetValue(destinationProperty.Name, out var converterMethodName))
            {
                var converter = ResolveConverter(compilation, declaration.MethodHostSymbol, source, destinationProperty, converterMethodName, report);

                if (converter is null)
                {
                    continue;
                }

                // [MapCondition] can still gate a [MapUsing]-converted property; independent.
                var converterCondition = ResolveCondition(declaration.MethodHostSymbol, source, destination, destinationProperty, explicitConditions, report);

                if (!converterCondition.Success)
                {
                    continue;
                }

                // A 'required' member has to be set unconditionally wherever the destination is
                // constructed (object-initializer/constructor-call), so a condition that might
                // leave it unset can't be honored - see MappingEmitter.Imperative.cs. Excluded
                // here rather than silently emitted, so it surfaces as GM013 (unmapped required
                // property) instead of failing to compile with no explanation.
                if (destinationProperty.IsRequired && converterCondition.MethodName is not null)
                {
                    report?.Invoke(Diagnostic.Create(
                        Diagnostics.ConditionOnRequiredPropertyUnsupported,
                        destinationProperty.Locations.FirstOrDefault() ?? Location.None,
                        destinationProperty.Name,
                        source.ToDisplayString(),
                        destination.ToDisplayString()));
                    continue;
                }

                var converterDefault = ResolveDefault(
                    destinationProperty, PropertyMappingKind.Converted, destinationProperty.Type,
                    explicitDefaults, source, destination, report);

                properties.Add(new PropertyMappingModel(
                    string.Empty,
                    destinationProperty.Name,
                    PropertyMappingKind.Converted,
                    TypeModel.From(destinationProperty.Type),
                    TypeModel.From(destinationProperty.Type),
                    ConditionMethodName: converterCondition.MethodName,
                    ConditionAcceptsDestination: converterCondition.AcceptsDestination,
                    DestinationIsInitOnly: destinationProperty.SetMethod!.IsInitOnly,
                    DestinationIsRequired: destinationProperty.IsRequired,
                    ConverterMethodName: converter,
                    DefaultValueLiteral: converterDefault,
                    MethodHostType: TypeModel.From(declaration.MethodHostSymbol)));

                continue;
            }

            var hasExplicitOverride = explicitMappings.TryGetValue(
                destinationProperty.Name,
                out var explicitSource);
            var sourceName = hasExplicitOverride ? explicitSource : destinationProperty.Name;

            IPropertySymbol? sourceProperty = null;
            string? flattenedSourcePath = null;
            string? explicitPathFailureReason = null;

            if (sourceProperties.TryGetValue(sourceName, out var directMatch))
            {
                sourceProperty = directMatch;
            }
            else if (hasExplicitOverride && sourceName!.Contains('.'))
            {
                // An explicit [MapProperty] naming a dotted path - every segment is already
                // given literally, so this walks it directly rather than searching PascalCase
                // split points like TryResolveFlattenedPath does for the default name-matching
                // path below.
                var path = TryResolveExplicitPath(source, sourceName, out explicitPathFailureReason);

                if (path is not null)
                {
                    sourceProperty = path[path.Count - 1];
                    flattenedSourcePath = sourceName;
                }
            }
            else if (!hasExplicitOverride)
            {
                // Naming-convention flattening fallback - only for the default name-matching
                // path; an explicit [MapProperty] naming a single (non-dotted) segment must
                // still be an exact top-level property name, handled above.
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
                if (hasExplicitOverride)
                {
                    report?.Invoke(Diagnostic.Create(
                        Diagnostics.MapPropertySourceNotFound,
                        destinationProperty.Locations.FirstOrDefault() ?? Location.None,
                        destinationProperty.Name,
                        source.ToDisplayString(),
                        sourceName!,
                        explicitPathFailureReason ?? $"'{source.ToDisplayString()}' has no accessible property called '{sourceName}'"));
                }
                else
                {
                    report?.Invoke(Diagnostic.Create(
                        Diagnostics.UnmappedDestinationProperty,
                        destinationProperty.Locations.FirstOrDefault() ?? Location.None,
                        destinationProperty.Name,
                        destination.ToDisplayString()));
                }

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

            var condition = ResolveCondition(declaration.MethodHostSymbol, source, destination, destinationProperty, explicitConditions, report);

            if (!condition.Success)
            {
                continue;
            }

            var conditionMethodName = condition.MethodName;
            var conditionAcceptsDestination = condition.AcceptsDestination;

            // A 'required' member has to be set unconditionally wherever the destination is
            // constructed (object-initializer/constructor-call), so a condition that might
            // leave it unset can't be honored - see MappingEmitter.Imperative.cs. Excluded here
            // rather than silently emitted, so it surfaces as GM013 (unmapped required property)
            // instead of failing to compile with no explanation.
            if (destinationProperty.IsRequired && conditionMethodName is not null)
            {
                report?.Invoke(Diagnostic.Create(
                    Diagnostics.ConditionOnRequiredPropertyUnsupported,
                    destinationProperty.Locations.FirstOrDefault() ?? Location.None,
                    destinationProperty.Name,
                    source.ToDisplayString(),
                    destination.ToDisplayString()));
                continue;
            }

            var propertyDefault = ResolveDefault(
                destinationProperty, resolution.Kind.Value, sourceProperty.Type,
                explicitDefaults, source, destination, report);

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
                destinationProperty.IsRequired,
                resolution.DestinationShape,
                DefaultValueLiteral: propertyDefault,
                MethodHostType: conditionMethodName is null ? null : TypeModel.From(declaration.MethodHostSymbol)));
        }

        // A 'required' destination property that never made it into `properties` above (for
        // any reason - no matching source, [MapIgnore], a failed [MapCondition]/[MapUsing]
        // lookup, ambiguous flattening, ...) is worse than just "left at its default": the
        // generated `new Dest()`/object-initializer call will fail to compile with CS9035,
        // since the required member is never set anywhere in that expression. Every other
        // reason a property gets skipped only reports an Info/Warning; this is reported
        // regardless, as an Error, since it's a guaranteed downstream compile failure.
        var mappedPropertyNames = new HashSet<string>(properties.Select(p => p.DestinationPropertyName));

        foreach (var destinationProperty in destination.GetMembers().OfType<IPropertySymbol>())
        {
            if (destinationProperty.IsStatic || destinationProperty.SetMethod is null)
            {
                continue;
            }

            if (destinationProperty.IsRequired && !mappedPropertyNames.Contains(destinationProperty.Name))
            {
                report?.Invoke(Diagnostic.Create(
                    Diagnostics.RequiredPropertyUnmapped,
                    destinationProperty.Locations.FirstOrDefault() ?? Location.None,
                    destinationProperty.Name,
                    source.ToDisplayString(),
                    destination.ToDisplayString()));
            }
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

    // Flags a destination property named by more than one instance of the same property-level
    // attribute kind within one mapping - the caller still takes group.Last() to resolve it
    // (unchanged, crash-avoiding behavior), this only makes the ambiguity visible instead of
    // resolving it silently.
    private static void ReportDuplicates<T>(
        IEnumerable<IGrouping<string, T>> grouped,
        string attributeName,
        INamedTypeSymbol methodHost,
        INamedTypeSymbol destination,
        Action<Diagnostic>? report)
    {
        foreach (var group in grouped)
        {
            if (group.Count() <= 1)
            {
                continue;
            }

            report?.Invoke(Diagnostic.Create(
                Diagnostics.DuplicatePropertyAttribute,
                methodHost.Locations.FirstOrDefault() ?? Location.None,
                group.Key,
                destination.ToDisplayString(),
                attributeName));
        }
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
            {
                return parameterNames;
            }
        }

        return null;
    }

    // [MapDefault] only ever applies to a Direct or Converted property whose coalesce-against
    // type can hold null - every other combination reports GM019 instead of resolving silently.
    // `coalesceAgainst` is the source property's type for a Direct match, or the destination
    // property's type for a Converted one (the converter's return type - there's no "source"
    // value to coalesce against there).
    private static string? ResolveDefault(
        IPropertySymbol destinationProperty,
        PropertyMappingKind kind,
        ITypeSymbol coalesceAgainst,
        IReadOnlyDictionary<string, string?> explicitDefaults,
        INamedTypeSymbol source,
        INamedTypeSymbol destination,
        Action<Diagnostic>? report)
    {
        if (!explicitDefaults.TryGetValue(destinationProperty.Name, out var literal))
        {
            return null;
        }

        string? reason = kind switch
        {
            not PropertyMappingKind.Direct and not PropertyMappingKind.Converted =>
                "the property isn't mapped as a plain value (it's a nested or enumerable mapping), so there's no expression to coalesce a literal against",
            _ when literal is null =>
                "its value isn't a literal Roslyn can express as an attribute constant (e.g. an array or typeof(...))",
            _ when !CanCoalesceNull(coalesceAgainst) =>
                $"'{coalesceAgainst.ToDisplayString()}' can't be null, so there's nothing for the default to substitute for",
            _ => null
        };

        if (reason is not null)
        {
            report?.Invoke(Diagnostic.Create(
                Diagnostics.MapDefaultHasNoEffect,
                destinationProperty.Locations.FirstOrDefault() ?? Location.None,
                destinationProperty.Name,
                source.ToDisplayString(),
                destination.ToDisplayString(),
                reason));
            return null;
        }

        return literal;
    }

    // `??` only compiles against a reference type or Nullable<T> - a plain value type (int,
    // a non-nullable struct) can never be null.
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
        {
            name = containing.MetadataName + "+" + name;
        }

        return type.ContainingNamespace is { IsGlobalNamespace: false } ns
            ? ns.ToDisplayString() + "." + name
            : name;
    }
}

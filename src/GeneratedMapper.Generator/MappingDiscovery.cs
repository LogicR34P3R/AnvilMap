using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace GeneratedMapper.Generator;

// Reads one [MapTo]-decorated type's attributes into MappingDeclaration values (one per
// [MapTo]). Only reads attribute data - destination properties are resolved separately by
// MappingResolver.
internal static class MappingDiscovery
{
    public static ImmutableArray<MappingDeclaration> Discover(
        GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol sourceSymbol)
            return ImmutableArray<MappingDeclaration>.Empty;

        var mapToAttributes = sourceSymbol.GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == GeneratorConstants.MapToAttribute)
            .ToArray();

        var mapPropertyAttributes = sourceSymbol.GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == GeneratorConstants.MapPropertyAttribute)
            .ToArray();

        var mapConditionAttributes = sourceSymbol.GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == GeneratorConstants.MapConditionAttribute)
            .ToArray();

        var mapUsingAttributes = sourceSymbol.GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == GeneratorConstants.MapUsingAttribute)
            .ToArray();

        var result = ImmutableArray.CreateBuilder<MappingDeclaration>();

        // Each [MapTo] names its own DestinationType, so per-property attributes below are
        // matched against the *current* [MapTo]'s destination, not collected wholesale.
        foreach (var mapTo in mapToAttributes)
        {
            if (mapTo.ConstructorArguments.Length != 1 ||
                mapTo.ConstructorArguments[0].Value is not INamedTypeSymbol destination)
                continue;

            var explicitProperties = new List<ExplicitPropertyMapping>();

            foreach (var attribute in mapPropertyAttributes)
            {
                // Defensive against invalid intermediate states (e.g. mid-edit in the IDE) -
                // the generator runs on every keystroke.
                if (attribute.ConstructorArguments.Length != 3 ||
                    attribute.ConstructorArguments[0].Value is not INamedTypeSymbol propertyDestination)
                    continue;

                if (!SymbolEqualityComparer.Default.Equals(destination, propertyDestination))
                    continue;

                var sourceProperty = attribute.ConstructorArguments[1].Value?.ToString();
                var destinationProperty = attribute.ConstructorArguments[2].Value?.ToString();

                if (!string.IsNullOrWhiteSpace(sourceProperty) &&
                    !string.IsNullOrWhiteSpace(destinationProperty))
                {
                    explicitProperties.Add(
                        new ExplicitPropertyMapping(sourceProperty!, destinationProperty!));
                }
            }

            var explicitConditions = new List<ExplicitConditionMapping>();

            foreach (var attribute in mapConditionAttributes)
            {
                if (attribute.ConstructorArguments.Length != 3 ||
                    attribute.ConstructorArguments[0].Value is not INamedTypeSymbol conditionDestination)
                    continue;

                if (!SymbolEqualityComparer.Default.Equals(destination, conditionDestination))
                    continue;

                var destinationProperty = attribute.ConstructorArguments[1].Value?.ToString();
                var conditionMethod = attribute.ConstructorArguments[2].Value?.ToString();

                if (!string.IsNullOrWhiteSpace(destinationProperty) &&
                    !string.IsNullOrWhiteSpace(conditionMethod))
                {
                    explicitConditions.Add(
                        new ExplicitConditionMapping(destinationProperty!, conditionMethod!));
                }
            }

            var explicitConverters = new List<ExplicitConverterMapping>();

            foreach (var attribute in mapUsingAttributes)
            {
                if (attribute.ConstructorArguments.Length != 3 ||
                    attribute.ConstructorArguments[0].Value is not INamedTypeSymbol converterDestination)
                    continue;

                if (!SymbolEqualityComparer.Default.Equals(destination, converterDestination))
                    continue;

                var destinationProperty = attribute.ConstructorArguments[1].Value?.ToString();
                var converterMethod = attribute.ConstructorArguments[2].Value?.ToString();

                if (!string.IsNullOrWhiteSpace(destinationProperty) &&
                    !string.IsNullOrWhiteSpace(converterMethod))
                {
                    explicitConverters.Add(
                        new ExplicitConverterMapping(destinationProperty!, converterMethod!));
                }
            }

            var generateReverse = mapTo.NamedArguments
                .FirstOrDefault(x => x.Key == "GenerateReverse")
                .Value.Value as bool? ?? false;

            var maxDepth = mapTo.NamedArguments
                .FirstOrDefault(x => x.Key == "MaxDepth")
                .Value.Value as int? ?? 0;

            result.Add(new MappingDeclaration(
                TypeModel.From(sourceSymbol),
                TypeModel.From(destination),
                sourceSymbol,
                destination,
                generateReverse,
                explicitProperties,
                explicitConditions,
                explicitConverters,
                maxDepth));
        }

        return result.ToImmutable();
    }
}

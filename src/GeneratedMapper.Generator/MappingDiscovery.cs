using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace GeneratedMapper.Generator;

// The first pipeline stage (see MappingSourceGenerator.Initialize): reads a single [MapTo]
// -decorated type's attributes and turns them into MappingDeclaration values - one per
// [MapTo] on the type, since a source can declare mappings to several destinations at once.
// This only reads attribute data (constructor arguments, named arguments) off the Roslyn
// symbol; it doesn't look at destination properties at all - that's MappingResolver's job,
// once every declaration across the whole compilation has been discovered and collected into
// a MappingGraph.
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

        // [MapProperty]/[MapCondition]/[MapUsing] are declared on the same source type as
        // [MapTo] but each names its own DestinationType - when a source has more than one
        // [MapTo], every per-property attribute below is matched against the *current*
        // [MapTo]'s destination (SymbolEqualityComparer checks below), not just collected
        // wholesale, so a [MapCondition] meant for one destination never leaks into another.
        foreach (var mapTo in mapToAttributes)
        {
            if (mapTo.ConstructorArguments.Length != 1 ||
                mapTo.ConstructorArguments[0].Value is not INamedTypeSymbol destination)
                continue;

            var explicitProperties = new List<ExplicitPropertyMapping>();

            foreach (var attribute in mapPropertyAttributes)
            {
                // Constructor-argument shape/length checks throughout this method are
                // defensive against attribute usages the C# compiler hasn't fully bound yet
                // (e.g. mid-edit in the IDE, or a genuinely malformed attribute argument) -
                // the generator runs on every keystroke, including invalid intermediate states.
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

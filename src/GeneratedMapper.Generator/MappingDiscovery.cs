using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace GeneratedMapper.Generator;

// Reads one [MapTo]- or [MapFrom]-decorated type's attributes into MappingDeclaration values
// (one per [MapTo]/[MapFrom]). Only reads attribute data - destination properties are resolved
// separately by MappingResolver.
internal static class MappingDiscovery
{
    // [MapTo] lives on the source type and names its destination; [MapFrom] lives on the
    // destination type and names its source. Either way, the type carrying the attribute is
    // also where companion attributes ([MapProperty]/[MapCondition]/[MapUsing]/[MapDefault])
    // and any named condition/converter method are looked up (MethodHostSymbol) - only which
    // side is Source vs Destination in the resulting declaration differs.
    public static ImmutableArray<MappingDeclaration> Discover(GeneratorAttributeSyntaxContext context)
        => DiscoverCore(context, GeneratorConstants.MapToAttribute, declaringSideIsSource: true);

    public static ImmutableArray<MappingDeclaration> DiscoverFrom(GeneratorAttributeSyntaxContext context)
        => DiscoverCore(context, GeneratorConstants.MapFromAttribute, declaringSideIsSource: false);

    private static ImmutableArray<MappingDeclaration> DiscoverCore(
        GeneratorAttributeSyntaxContext context, string mapAttributeName, bool declaringSideIsSource)
    {
        if (context.TargetSymbol is not INamedTypeSymbol declaringSymbol)
        {
            return ImmutableArray<MappingDeclaration>.Empty;
        }

        var mapAttributes = declaringSymbol.GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == mapAttributeName)
            .ToArray();

        var mapPropertyAttributes = declaringSymbol.GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == GeneratorConstants.MapPropertyAttribute)
            .ToArray();

        var mapConditionAttributes = declaringSymbol.GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == GeneratorConstants.MapConditionAttribute)
            .ToArray();

        var mapUsingAttributes = declaringSymbol.GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == GeneratorConstants.MapUsingAttribute)
            .ToArray();

        var mapDefaultAttributes = declaringSymbol.GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == GeneratorConstants.MapDefaultAttribute)
            .ToArray();

        var result = ImmutableArray.CreateBuilder<MappingDeclaration>();

        // Each [MapTo]/[MapFrom] names its own other-side type, so per-property attributes
        // below are matched against the *current* attribute's other side, not collected
        // wholesale.
        foreach (var mapAttribute in mapAttributes)
        {
            if (mapAttribute.ConstructorArguments.Length != 1 ||
                mapAttribute.ConstructorArguments[0].Value is not INamedTypeSymbol otherSide)
            {
                continue;
            }

            var explicitProperties = new List<ExplicitPropertyMapping>();

            foreach (var attribute in mapPropertyAttributes)
            {
                // Defensive against invalid intermediate states (e.g. mid-edit in the IDE) -
                // the generator runs on every keystroke.
                if (attribute.ConstructorArguments.Length != 3 ||
                    attribute.ConstructorArguments[0].Value is not INamedTypeSymbol propertyOtherSide)
                {
                    continue;
                }

                if (!SymbolEqualityComparer.Default.Equals(otherSide, propertyOtherSide))
                {
                    continue;
                }

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
                    attribute.ConstructorArguments[0].Value is not INamedTypeSymbol conditionOtherSide)
                {
                    continue;
                }

                if (!SymbolEqualityComparer.Default.Equals(otherSide, conditionOtherSide))
                {
                    continue;
                }

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
                    attribute.ConstructorArguments[0].Value is not INamedTypeSymbol converterOtherSide)
                {
                    continue;
                }

                if (!SymbolEqualityComparer.Default.Equals(otherSide, converterOtherSide))
                {
                    continue;
                }

                var destinationProperty = attribute.ConstructorArguments[1].Value?.ToString();
                var converterMethod = attribute.ConstructorArguments[2].Value?.ToString();

                if (!string.IsNullOrWhiteSpace(destinationProperty) &&
                    !string.IsNullOrWhiteSpace(converterMethod))
                {
                    explicitConverters.Add(
                        new ExplicitConverterMapping(destinationProperty!, converterMethod!));
                }
            }

            var explicitDefaults = new List<ExplicitDefaultMapping>();

            foreach (var attribute in mapDefaultAttributes)
            {
                if (attribute.ConstructorArguments.Length != 3 ||
                    attribute.ConstructorArguments[0].Value is not INamedTypeSymbol defaultOtherSide)
                {
                    continue;
                }

                if (!SymbolEqualityComparer.Default.Equals(otherSide, defaultOtherSide))
                {
                    continue;
                }

                var destinationProperty = attribute.ConstructorArguments[1].Value?.ToString();
                var literal = FormatDefaultValueLiteral(attribute.ConstructorArguments[2]);

                if (!string.IsNullOrWhiteSpace(destinationProperty) && literal is not null)
                {
                    explicitDefaults.Add(
                        new ExplicitDefaultMapping(destinationProperty!, literal));
                }
            }

            var generateReverse = mapAttribute.NamedArguments
                .FirstOrDefault(x => x.Key == "GenerateReverse")
                .Value.Value as bool? ?? false;

            var maxDepth = mapAttribute.NamedArguments
                .FirstOrDefault(x => x.Key == "MaxDepth")
                .Value.Value as int? ?? 0;

            var sourceSymbol = declaringSideIsSource ? declaringSymbol : otherSide;
            var destinationSymbol = declaringSideIsSource ? otherSide : declaringSymbol;

            result.Add(new MappingDeclaration(
                TypeModel.From(sourceSymbol),
                TypeModel.From(destinationSymbol),
                sourceSymbol,
                destinationSymbol,
                declaringSymbol,
                generateReverse,
                explicitProperties,
                explicitConditions,
                explicitConverters,
                explicitDefaults,
                maxDepth));
        }

        return result.ToImmutable();
    }

    // A [MapDefault] constant is limited to what Roslyn allows as an attribute argument:
    // numeric/string/bool/char/enum, or an array/typeof/Error TypedConstant that this doesn't
    // attempt to format. Returning null for those means the [MapDefault] is silently skipped
    // (MappingResolver only applies a default it actually has a literal for) rather than
    // guessing at syntax that might not compile.
    private static string? FormatDefaultValueLiteral(TypedConstant constant)
    {
        if (constant.IsNull)
        {
            return "null";
        }

        if (constant.Kind == TypedConstantKind.Primitive)
        {
            return SymbolDisplay.FormatPrimitive(constant.Value!, quoteStrings: true, useHexadecimalNumbers: false);
        }

        if (constant.Kind == TypedConstantKind.Enum && constant.Type is INamedTypeSymbol enumType)
        {
            var member = enumType.GetMembers()
                .OfType<IFieldSymbol>()
                .FirstOrDefault(f => f.HasConstantValue && Equals(f.ConstantValue, constant.Value));

            var qualifiedType = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            return member is not null
                ? $"{qualifiedType}.{member.Name}"
                : $"({qualifiedType}){constant.Value}";
        }

        return null;
    }
}

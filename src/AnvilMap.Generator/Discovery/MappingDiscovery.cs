using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AnvilMap.Generator;

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

        var mapIncludeAttributes = declaringSymbol.GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == GeneratorConstants.MapIncludeAttribute)
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

            var explicitProperties = ExtractMatching(mapPropertyAttributes, otherSide, TryCreatePropertyMapping);
            var explicitConditions = ExtractMatching(mapConditionAttributes, otherSide, TryCreateConditionMapping);
            var explicitConverters = ExtractMatching(mapUsingAttributes, otherSide, TryCreateConverterMapping);
            var explicitDefaults = ExtractMatching(mapDefaultAttributes, otherSide, TryCreateDefaultMapping);
            var explicitIncludes = ExtractMatching(mapIncludeAttributes, otherSide, TryCreateIncludeMapping);

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
                maxDepth,
                explicitIncludes));
        }

        return result.ToImmutable();
    }

    // Shared by all four companion-attribute kinds below: each one's ConstructorArguments[0]
    // must match the current [MapTo]/[MapFrom]'s own other-side type before the rest of its
    // arguments are even looked at - project returns null for anything that doesn't match or
    // doesn't parse, and is only ever called with three-argument attribute shapes.
    private static List<T> ExtractMatching<T>(
        AttributeData[] attributes, INamedTypeSymbol otherSide, Func<AttributeData, T?> project)
        where T : class
    {
        var result = new List<T>();

        foreach (var attribute in attributes)
        {
            // Defensive against invalid intermediate states (e.g. mid-edit in the IDE) - the
            // generator runs on every keystroke.
            if (attribute.ConstructorArguments.Length != 3 ||
                attribute.ConstructorArguments[0].Value is not INamedTypeSymbol candidateOtherSide ||
                !SymbolEqualityComparer.Default.Equals(otherSide, candidateOtherSide))
            {
                continue;
            }

            var item = project(attribute);
            if (item is not null)
            {
                result.Add(item);
            }
        }

        return result;
    }

    private static ExplicitPropertyMapping? TryCreatePropertyMapping(AttributeData attribute)
    {
        var sourceProperty = attribute.ConstructorArguments[1].Value?.ToString();
        var destinationProperty = attribute.ConstructorArguments[2].Value?.ToString();

        return !string.IsNullOrWhiteSpace(sourceProperty) && !string.IsNullOrWhiteSpace(destinationProperty)
            ? new ExplicitPropertyMapping(sourceProperty!, destinationProperty!)
            : null;
    }

    private static ExplicitConditionMapping? TryCreateConditionMapping(AttributeData attribute)
    {
        var destinationProperty = attribute.ConstructorArguments[1].Value?.ToString();
        var conditionMethod = attribute.ConstructorArguments[2].Value?.ToString();

        return !string.IsNullOrWhiteSpace(destinationProperty) && !string.IsNullOrWhiteSpace(conditionMethod)
            ? new ExplicitConditionMapping(destinationProperty!, conditionMethod!)
            : null;
    }

    private static ExplicitConverterMapping? TryCreateConverterMapping(AttributeData attribute)
    {
        var destinationProperty = attribute.ConstructorArguments[1].Value?.ToString();
        var converterMethod = attribute.ConstructorArguments[2].Value?.ToString();

        var inlineInProjection = attribute.NamedArguments
            .FirstOrDefault(x => x.Key == "InlineInProjection")
            .Value.Value as bool? ?? false;

        return !string.IsNullOrWhiteSpace(destinationProperty) && !string.IsNullOrWhiteSpace(converterMethod)
            ? new ExplicitConverterMapping(destinationProperty!, converterMethod!, inlineInProjection)
            : null;
    }

    private static ExplicitDefaultMapping? TryCreateDefaultMapping(AttributeData attribute)
    {
        var destinationProperty = attribute.ConstructorArguments[1].Value?.ToString();

        // The literal may be null here (an unformattable constant, e.g. an array or typeof(...))
        // - still recorded rather than dropped, so MappingResolver can report AM019 for it
        // instead of treating it as if [MapDefault] were never there.
        return !string.IsNullOrWhiteSpace(destinationProperty)
            ? new ExplicitDefaultMapping(destinationProperty!, FormatDefaultValueLiteral(attribute.ConstructorArguments[2]))
            : null;
    }

    private static ExplicitIncludeMapping? TryCreateIncludeMapping(AttributeData attribute)
    {
        if (attribute.ConstructorArguments[1].Value is not INamedTypeSymbol derivedSource ||
            attribute.ConstructorArguments[2].Value is not INamedTypeSymbol derivedDestination)
        {
            return null;
        }

        return new ExplicitIncludeMapping(
            TypeModel.From(derivedSource),
            TypeModel.From(derivedDestination),
            derivedSource,
            derivedDestination);
    }

    // A [MapDefault] constant is limited to what Roslyn allows as an attribute argument:
    // numeric/string/bool/char/enum, or an array/typeof/Error TypedConstant that this doesn't
    // attempt to format (rather than guessing at syntax that might not compile) - returns null
    // for those, which MappingResolver still records and reports as AM019.
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

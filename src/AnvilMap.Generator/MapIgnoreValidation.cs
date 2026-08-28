using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace AnvilMap.Generator;

// Two [MapIgnore] correctness checks that need the *whole* MappingGraph rather than a single
// MappingDeclaration, so they run once per destination type here instead of inside
// MappingResolver.Resolve (which runs once per declaration - checking there would mean the same
// destination gets inspected, and any finding reported, once per source that maps into it).
internal static class MapIgnoreValidation
{
    public static void Validate(MappingGraph graph, Action<Diagnostic>? report)
    {
        if (report is null)
        {
            return;
        }

        var sourcesByDestination = new Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>>(SymbolEqualityComparer.Default);

        foreach (var declaration in graph.GetMappings())
        {
            if (!sourcesByDestination.TryGetValue(declaration.DestinationSymbol, out var sources))
            {
                sources = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                sourcesByDestination[declaration.DestinationSymbol] = sources;
            }

            sources.Add(declaration.SourceSymbol);
        }

        foreach (var entry in sourcesByDestination)
        {
            var destination = entry.Key;
            var actualSources = entry.Value;

            foreach (var property in destination.GetMembers().OfType<IPropertySymbol>())
            {
                var mapIgnoreAttributes = property.GetAttributes()
                    .Where(a => a.AttributeClass?.ToDisplayString() == GeneratorConstants.MapIgnoreAttribute)
                    .ToArray();

                if (mapIgnoreAttributes.Length == 0)
                {
                    continue;
                }

                ValidateStaleSourceTypes(destination, property, mapIgnoreAttributes, actualSources, report);
                ValidateRedundancy(destination, property, mapIgnoreAttributes, report);
            }
        }
    }

    // A [MapIgnore(typeof(X))] where X never actually maps into this destination is almost
    // certainly a typo, or an attribute left behind after the source it named was renamed or
    // the mapping between them removed - it silently does nothing today.
    private static void ValidateStaleSourceTypes(
        INamedTypeSymbol destination,
        IPropertySymbol property,
        AttributeData[] mapIgnoreAttributes,
        HashSet<INamedTypeSymbol> actualSources,
        Action<Diagnostic> report)
    {
        foreach (var attribute in mapIgnoreAttributes)
        {
            if (attribute.ConstructorArguments.Length == 0 ||
                attribute.ConstructorArguments[0].Value is not INamedTypeSymbol namedSource)
            {
                continue;
            }

            if (actualSources.Contains(namedSource))
            {
                continue;
            }

            report(Diagnostic.Create(
                Diagnostics.MapIgnoreSourceTypeNeverMapped,
                property.Locations.FirstOrDefault() ?? Location.None,
                namedSource.ToDisplayString(),
                destination.ToDisplayString(),
                property.Name));
        }
    }

    // Two redundancy shapes: an unscoped [MapIgnore] already excludes every source, so any
    // scoped [MapIgnore(typeof(X))] alongside it adds nothing; and the same source type named
    // by more than one [MapIgnore(typeof(X))] is just a duplicate.
    private static void ValidateRedundancy(
        INamedTypeSymbol destination,
        IPropertySymbol property,
        AttributeData[] mapIgnoreAttributes,
        Action<Diagnostic> report)
    {
        var hasUnscoped = mapIgnoreAttributes.Any(a => a.ConstructorArguments.Length == 0);

        var scopedTypeNames = mapIgnoreAttributes
            .Where(a => a.ConstructorArguments.Length > 0 && a.ConstructorArguments[0].Value is INamedTypeSymbol)
            .Select(a => ((INamedTypeSymbol)a.ConstructorArguments[0].Value!).ToDisplayString())
            .ToList();

        var reasons = new List<string>();

        if (hasUnscoped && scopedTypeNames.Count > 0)
        {
            reasons.Add(
                $"the unscoped [MapIgnore] already excludes every source, so the scoped [MapIgnore] for " +
                $"{string.Join(", ", scopedTypeNames.Distinct())} has no additional effect");
        }

        var duplicated = scopedTypeNames
            .GroupBy(name => name)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicated.Count > 0)
        {
            reasons.Add($"{string.Join(", ", duplicated)} is targeted by more than one [MapIgnore]");
        }

        if (reasons.Count == 0)
        {
            return;
        }

        report(Diagnostic.Create(
            Diagnostics.RedundantMapIgnore,
            property.Locations.FirstOrDefault() ?? Location.None,
            property.Name,
            destination.ToDisplayString(),
            string.Join("; ", reasons)));
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace AnvilMap.Generator;

internal static partial class MappingResolver
{
    private static IReadOnlyList<PolymorphicIncludeModel>? ResolveIncludes(
        MappingGraph graph,
        MappingDeclaration declaration,
        Action<Diagnostic>? report)
    {
        if (declaration.ExplicitIncludes is not { Count: > 0 } explicitIncludes)
        {
            return null;
        }

        var groupedByDerivedSource = explicitIncludes
            .GroupBy(x => x.DerivedSourceSymbol, SymbolEqualityComparer.Default)
            .ToArray();

        foreach (var group in groupedByDerivedSource)
        {
            if (group.Count() <= 1)
            {
                continue;
            }

            report?.Invoke(Diagnostic.Create(
                Diagnostics.DuplicateMapInclude,
                declaration.MethodHostSymbol.Locations.FirstOrDefault() ?? Location.None,
                declaration.Source.DisplayName,
                declaration.Destination.DisplayName,
                ((INamedTypeSymbol)group.Key!).ToDisplayString()));
        }

        var results = new List<PolymorphicIncludeModel>();

        foreach (var group in groupedByDerivedSource)
        {
            var include = group.Last();
            var sourceDerives = SymbolEqualityComparer.Default.Equals(
                include.DerivedSourceSymbol.BaseType, declaration.SourceSymbol);
            var destinationDerives = SymbolEqualityComparer.Default.Equals(
                include.DerivedDestinationSymbol.BaseType, declaration.DestinationSymbol);

            if (!sourceDerives || !destinationDerives)
            {
                report?.Invoke(Diagnostic.Create(
                    Diagnostics.MapIncludeNotDerived,
                    declaration.MethodHostSymbol.Locations.FirstOrDefault() ?? Location.None,
                    include.DerivedSourceType.DisplayName,
                    include.DerivedDestinationType.DisplayName,
                    declaration.Source.DisplayName,
                    declaration.Destination.DisplayName));
                continue;
            }

            if (!graph.TryGetMapping(
                    include.DerivedSourceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    include.DerivedDestinationSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    out _))
            {
                report?.Invoke(Diagnostic.Create(
                    Diagnostics.MapIncludeMissingOwnMapping,
                    declaration.MethodHostSymbol.Locations.FirstOrDefault() ?? Location.None,
                    include.DerivedSourceType.DisplayName,
                    include.DerivedDestinationType.DisplayName));
                continue;
            }

            results.Add(new PolymorphicIncludeModel(include.DerivedSourceType, include.DerivedDestinationType));
        }

        return results.Count > 0 ? results : null;
    }
}

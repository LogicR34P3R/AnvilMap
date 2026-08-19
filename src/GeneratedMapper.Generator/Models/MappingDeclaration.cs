using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace GeneratedMapper.Generator;

// What MappingDiscovery reads off the user's attributes for one [MapTo] - unresolved,
// unvalidated. MappingResolver turns this into a MappingModel; nothing here is checked
// against the destination type yet.
internal sealed record MappingDeclaration(
    TypeModel Source,
    TypeModel Destination,
    INamedTypeSymbol SourceSymbol,
    INamedTypeSymbol DestinationSymbol,
    bool GenerateReverse,
    IReadOnlyList<ExplicitPropertyMapping> ExplicitProperties,
    IReadOnlyList<ExplicitConditionMapping> ExplicitConditions,
    IReadOnlyList<ExplicitConverterMapping> ExplicitConverters,
    IReadOnlyList<ExplicitDefaultMapping> ExplicitDefaults,
    int MaxDepth = 0)
{
    // Declares each field's reversal behavior next to the record itself, so a future field
    // addition has to be a visible, greppable decision here rather than a silent gap in
    // MappingGraph's reverse-construction call site. Conditions/converters/defaults reset to
    // empty - all three are tied to the original source type, so a reverse mapping needs its
    // own [MapCondition]/[MapUsing]/[MapDefault]. MaxDepth carries over unchanged.
    public MappingDeclaration ToReverse() => this with
    {
        Source = Destination,
        Destination = Source,
        SourceSymbol = DestinationSymbol,
        DestinationSymbol = SourceSymbol,
        GenerateReverse = false,
        ExplicitProperties = ExplicitProperties
            .Select(x => new ExplicitPropertyMapping(x.DestinationProperty, x.SourceProperty))
            .ToArray(),
        ExplicitConditions = Array.Empty<ExplicitConditionMapping>(),
        ExplicitConverters = Array.Empty<ExplicitConverterMapping>(),
        ExplicitDefaults = Array.Empty<ExplicitDefaultMapping>(),
    };
}

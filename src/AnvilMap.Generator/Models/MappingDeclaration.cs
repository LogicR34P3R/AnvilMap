using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace AnvilMap.Generator;

// What MappingDiscovery reads off the user's attributes for one [MapTo] - unresolved,
// unvalidated. MappingResolver turns this into a MappingModel; nothing here is checked
// against the destination type yet.
internal sealed record MappingDeclaration(
    TypeModel Source,
    TypeModel Destination,
    INamedTypeSymbol SourceSymbol,
    INamedTypeSymbol DestinationSymbol,
    // The type that physically carries [MapTo]/[MapFrom] (and any companion attributes) for
    // this declaration - SourceSymbol for a [MapTo]-declared mapping, DestinationSymbol for a
    // [MapFrom]-declared one. [MapCondition]/[MapUsing] methods are looked up here, not
    // necessarily on SourceSymbol, so that a [MapFrom]-declared mapping's methods can live on
    // the destination (e.g. a DTO that's allowed to know about the entity) instead of forcing
    // the entity to know about the DTO.
    INamedTypeSymbol MethodHostSymbol,
    bool GenerateReverse,
    IReadOnlyList<ExplicitPropertyMapping> ExplicitProperties,
    IReadOnlyList<ExplicitConditionMapping> ExplicitConditions,
    IReadOnlyList<ExplicitConverterMapping> ExplicitConverters,
    IReadOnlyList<ExplicitDefaultMapping> ExplicitDefaults,
    int MaxDepth = 0,
    IReadOnlyList<ExplicitIncludeMapping>? ExplicitIncludes = null)
{
    // Declares each field's reversal behavior next to the record itself, so a future field
    // addition has to be a visible, greppable decision here rather than a silent gap in
    // MappingGraph's reverse-construction call site. Conditions/converters/defaults/includes
    // reset to empty - all are tied to the original source type, so a reverse mapping needs its
    // own [MapCondition]/[MapUsing]/[MapDefault]/[MapInclude]. MaxDepth carries over unchanged.
    // MethodHostSymbol is left unchanged (still the original declaring type) - inert either
    // way since the explicit collections below are wiped, but it's the closest thing to "where
    // a reverse-direction attribute would be declared" if one existed.
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
        ExplicitIncludes = Array.Empty<ExplicitIncludeMapping>(),
    };
}

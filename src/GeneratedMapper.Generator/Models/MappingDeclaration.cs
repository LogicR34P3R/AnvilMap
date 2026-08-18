using System.Collections.Generic;
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
    int MaxDepth = 0);

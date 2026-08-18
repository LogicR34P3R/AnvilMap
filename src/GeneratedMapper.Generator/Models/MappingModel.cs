using System.Collections.Generic;

namespace GeneratedMapper.Generator;

// The fully-resolved result of MappingResolver.Resolve - only successfully matched
// properties. The only input MappingEmitter reads; it never looks back at Roslyn symbols.
internal sealed record MappingModel(
    TypeModel Source,
    TypeModel Destination,
    IReadOnlyList<PropertyMappingModel> Properties,
    bool DestinationHasParameterlessConstructor = true,
    int MaxDepth = 0);

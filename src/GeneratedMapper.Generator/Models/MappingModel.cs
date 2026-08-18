using System.Collections.Generic;

namespace GeneratedMapper.Generator;

// The fully-resolved result of MappingResolver.Resolve: one instance per [MapTo] declaration,
// carrying only properties that were successfully matched (anything that couldn't be was
// already reported as a diagnostic and dropped). This is the only input MappingEmitter reads -
// it never looks back at Roslyn symbols, so codegen can't accidentally depend on something
// resolution didn't already validate.
internal sealed record MappingModel(
    TypeModel Source,
    TypeModel Destination,
    IReadOnlyList<PropertyMappingModel> Properties,
    bool DestinationHasParameterlessConstructor = true,
    int MaxDepth = 0);

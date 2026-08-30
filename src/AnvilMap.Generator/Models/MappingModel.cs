using System.Collections.Generic;

namespace AnvilMap.Generator;

// The fully-resolved result of MappingResolver.Resolve - only successfully matched
// properties. The only input MappingEmitter reads; it never looks back at Roslyn symbols.
// ConstructorParameterProperties is null unless DestinationHasParameterlessConstructor is
// false and MappingResolver found a constructor whose parameters (in this order) all match
// already-resolved, unconditioned properties by name and type - e.g. a positional record's
// synthesized constructor. When set, MappingEmitter builds `new Dest(args...) { rest... }`
// instead of `new Dest() { ... }` / `new Dest { ... }`. Includes is null/empty for every
// non-polymorphic mapping, set only when the declaration carried at least one validated
// [MapInclude].
internal sealed record MappingModel(
    TypeModel Source,
    TypeModel Destination,
    IReadOnlyList<PropertyMappingModel> Properties,
    bool DestinationHasParameterlessConstructor = true,
    int MaxDepth = 0,
    IReadOnlyList<string>? ConstructorParameterProperties = null,
    IReadOnlyList<PolymorphicIncludeModel>? Includes = null);

using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace GeneratedMapper.Generator;

// What MappingDiscovery reads straight off the user's attributes for one [MapTo] declaration -
// unresolved and unvalidated. MappingResolver.Resolve turns this into a MappingModel by
// matching every destination property against SourceSymbol/DestinationSymbol's actual members;
// nothing here has been checked against the destination type yet (that's deliberately kept out
// of this stage - see MappingResolver.cs).
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

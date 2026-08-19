namespace GeneratedMapper.Generator;

// DefaultValueLiteral is pre-formatted C# source (e.g. "\"Unknown\"", "42") by
// MappingDiscovery.FormatDefaultValueLiteral - MappingResolver/MappingEmitter just splice it in.
internal sealed record ExplicitDefaultMapping(
    string DestinationProperty,
    string DefaultValueLiteral);

namespace GeneratedMapper.Generator;

// DefaultValueLiteral is pre-formatted C# source (e.g. "\"Unknown\"", "42") by
// MappingDiscovery.FormatDefaultValueLiteral - MappingResolver/MappingEmitter just splice it in.
// Null means a [MapDefault] was declared for this property but its value wasn't a literal Roslyn
// could format (see FormatDefaultValueLiteral) - kept (not dropped) so MappingResolver can still
// report GM019 for it instead of behaving as if no [MapDefault] existed at all.
internal sealed record ExplicitDefaultMapping(
    string DestinationProperty,
    string? DefaultValueLiteral);

namespace GeneratedMapper.Generator;

// How a destination property's value is produced. `Ignored` is declared but never assigned -
// ignored/unmatched properties are dropped from the property list entirely instead.
internal enum PropertyMappingKind
{
    Direct,
    Nested,
    Enumerable,
    Converted,
    Ignored
}

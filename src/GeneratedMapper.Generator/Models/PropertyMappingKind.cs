namespace GeneratedMapper.Generator;

// How a destination property's value is produced - see MappingResolver.ResolveKind for how
// Direct/Nested/Enumerable are chosen, and MappingResolver.Resolve for Converted
// ([MapUsing]). `Ignored` is declared for completeness but never actually assigned:
// [MapIgnore]'d and unmatched properties are dropped from the property list entirely rather
// than kept with this Kind, so every switch over Kind in MappingEmitter.*.cs only needs to
// handle the four real cases (plus a `_ => null`/default arm as a safety net).
internal enum PropertyMappingKind
{
    Direct,
    Nested,
    Enumerable,
    Converted,
    Ignored
}

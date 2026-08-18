namespace GeneratedMapper.Generator;

// One resolved destination property. This is a flat record covering every Kind (Direct,
// Nested, Enumerable, Converted, Ignored) rather than a separate type per kind, so several
// fields below are only meaningful for specific Kind values - ElementSourceType/
// ElementDestinationType/DestinationCollectionShape only apply when Kind == Enumerable,
// ConverterMethodName only when Kind == Converted, and so on. MappingResolver is careful to
// only set the fields that apply to the Kind it's constructing; every switch over `Kind` in
// MappingEmitter.*.cs implicitly depends on that discipline holding. See docs/roadmapv2.md
// AD3 for the tradeoff around splitting this into a proper discriminated union instead.
internal sealed record PropertyMappingModel(
    string SourcePropertyName,
    string DestinationPropertyName,
    PropertyMappingKind Kind,
    TypeModel SourceType,
    TypeModel DestinationType,
    TypeModel? ElementSourceType = null,
    TypeModel? ElementDestinationType = null,
    bool SourceIsNullable = false,
    string? ConditionMethodName = null,
    bool ConditionAcceptsDestination = false,
    bool DestinationIsInitOnly = false,
    CollectionShape DestinationCollectionShape = CollectionShape.List,
    string? ConverterMethodName = null);

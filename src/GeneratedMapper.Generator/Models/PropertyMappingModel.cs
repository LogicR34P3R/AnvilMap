namespace GeneratedMapper.Generator;

// One resolved destination property. A flat record covering every Kind, so several fields
// are only meaningful for specific Kind values (Element*/DestinationCollectionShape for
// Enumerable, ConverterMethodName for Converted). MappingResolver only sets what applies;
// every `Kind` switch in MappingEmitter.*.cs depends on that holding.
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

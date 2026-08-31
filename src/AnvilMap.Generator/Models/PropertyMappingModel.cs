namespace AnvilMap.Generator;

// One resolved destination property. A flat record covering every Kind, so several fields
// are only meaningful for specific Kind values (Element*/DestinationCollectionShape for
// Enumerable, ConverterMethodName for Converted). MappingResolver only sets what applies;
// every `Kind` switch in MappingEmitter.*.cs depends on that holding. DefaultValueLiteral is
// only ever set for Direct/Converted - MappingResolver already checked the value's type can
// hold null, so MappingEmitter just splices it into a `?? literal` suffix unconditionally.
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
    bool DestinationIsRequired = false,
    CollectionShape DestinationCollectionShape = CollectionShape.List,
    string? ConverterMethodName = null,
    string? DefaultValueLiteral = null,
    // Converter body text with its parameter replaced by a placeholder; null falls back to a
    // plain method call. Set only when InlineInProjection was eligible.
    string? InlinedConverterProjectionTemplate = null,
    // The type ConditionMethodName/ConverterMethodName is actually declared on - the source
    // type for a [MapTo]-declared mapping, but possibly the destination type for a
    // [MapFrom]-declared one. Set whenever either method name is set; MappingEmitter qualifies
    // the generated call with this instead of assuming the mapping's source type.
    TypeModel? MethodHostType = null,
    // Set only for Kind == EnumConversion - which built-in conversion to emit.
    EnumConversionKind? EnumConversion = null,
    // Set only for Kind == Enumerable - None unless the source property's own declared type is
    // exactly List<T> or an array, whose Count/Length is cheap without enumerating.
    SourceCountAccessor SourceCountAccessor = SourceCountAccessor.None);

namespace AnvilMap.Generator;

internal sealed record ExplicitConverterMapping(
    string DestinationProperty,
    string ConverterMethodName,
    bool InlineInProjection);

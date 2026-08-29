namespace AnvilMap.Generator;

// Which built-in conversion PropertyMappingKind.EnumConversion applies. ToUnderlyingType is a
// numeric cast (projection-safe); ToString is a method call (excluded from SQL projections -
// see MappingEmitter.Projection.cs).
internal enum EnumConversionKind
{
    ToUnderlyingType,
    ToString
}

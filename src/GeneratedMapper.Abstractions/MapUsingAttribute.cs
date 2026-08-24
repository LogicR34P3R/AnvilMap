namespace GeneratedMapper;

/// <summary>
/// Maps a single destination property through a static conversion function instead of a
/// direct/nested/enumerable match. The named converter method must be a static
/// method declared on the source type with signature <c>TDestinationProperty Method(TSource)</c>
/// (an implicitly convertible return type is also accepted). Honored by both the imperative
/// mapper and SQL projections — for projections, the call is inlined as-is, so it's the
/// caller's responsibility to keep the method translatable by EF Core's query provider.
/// Not auto-reversed by <see cref="MapToAttribute.GenerateReverse"/>: declare a separate
/// <see cref="MapUsingAttribute"/> on the destination type if the reverse direction needs one.
/// </summary>
/// <remarks>
/// Can also be placed on a destination type decorated with <see cref="MapFromAttribute"/>
/// instead of on the source - in that case, pass the source type as <c>destinationType</c>,
/// and the converter method is looked up on the destination type (the one this attribute is
/// declared on) instead of the source.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class MapUsingAttribute : Attribute
{
    /// <summary>
    /// Declares that <paramref name="destinationProperty"/> on <paramref name="destinationType"/>
    /// is computed by calling the static method named <paramref name="converterMethod"/> on the
    /// decorated source type, instead of being matched to a source property.
    /// </summary>
    public MapUsingAttribute(Type destinationType, string destinationProperty, string converterMethod)
    {
        DestinationType = destinationType;
        DestinationProperty = destinationProperty;
        ConverterMethod = converterMethod;
    }

    /// <summary>The mapping this converter applies to.</summary>
    public Type DestinationType { get; }

    /// <summary>The property on <see cref="DestinationType"/> the converter's return value is assigned to.</summary>
    public string DestinationProperty { get; }

    /// <summary>
    /// The name of the static <c>TDestinationProperty Method(TSource)</c> method, declared on
    /// the decorated source type, that computes <see cref="DestinationProperty"/>'s value.
    /// </summary>
    public string ConverterMethod { get; }
}

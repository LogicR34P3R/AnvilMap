namespace AnvilMap;

/// <summary>
/// Gates mapping of a single destination property on a condition, evaluated at map time.
/// The named condition method must be a static bool method declared on the source
/// type with signature <c>(TSource)</c> or <c>(TSource, TDestination?)</c>.
/// Not honored by SQL projections (<c>ProjectTo*</c>) — the property is left at its default
/// there instead, since the condition can't be translated into the query.
/// </summary>
/// <remarks>
/// Can also be placed on a destination type decorated with <see cref="MapFromAttribute"/>
/// instead of on the source - in that case, pass the source type as <c>destinationType</c>,
/// and the condition method is looked up on the destination type (the one this attribute is
/// declared on) instead of the source.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class MapConditionAttribute : Attribute
{
    /// <summary>
    /// Declares that <paramref name="destinationProperty"/> on <paramref name="destinationType"/>
    /// is only mapped when the static method named <paramref name="conditionMethod"/>, called on
    /// the decorated source type, returns <see langword="true"/>.
    /// </summary>
    public MapConditionAttribute(Type destinationType, string destinationProperty, string conditionMethod)
    {
        DestinationType = destinationType;
        DestinationProperty = destinationProperty;
        ConditionMethod = conditionMethod;
    }

    /// <summary>The mapping this condition applies to.</summary>
    public Type DestinationType { get; }

    /// <summary>The property on <see cref="DestinationType"/> whose mapping is gated.</summary>
    public string DestinationProperty { get; }

    /// <summary>
    /// The name of the static <c>bool Method(TSource)</c> or
    /// <c>bool Method(TSource, TDestination?)</c> method, declared on the decorated source
    /// type, that decides whether <see cref="DestinationProperty"/> gets mapped.
    /// </summary>
    public string ConditionMethod { get; }
}

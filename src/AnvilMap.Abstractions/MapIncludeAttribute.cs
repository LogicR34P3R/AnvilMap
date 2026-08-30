namespace AnvilMap;

/// <summary>
/// Declares that a runtime instance of a derived source type should produce a correspondingly
/// derived destination type instead of the base destination when mapped through the decorated
/// type's own <c>To{Dest}()</c> method. Placed on the base source type alongside its own
/// <see cref="MapToAttribute"/> (or on the base destination type alongside
/// <see cref="MapFromAttribute"/>) - one <see cref="MapIncludeAttribute"/> per derived pair. Both
/// the derived source and derived destination type must independently carry their own
/// <see cref="MapToAttribute"/>/<see cref="MapFromAttribute"/> mapping, and each must derive
/// directly (single level only) from the decorated type's own source/destination respectively.
/// </summary>
/// <remarks>
/// Can also be placed on a destination type decorated with <see cref="MapFromAttribute"/>
/// instead of on the source - in that case, pass the source type as <c>destinationType</c>,
/// matching <see cref="MapFromAttribute"/>'s own argument (the same convention
/// <see cref="MapPropertyAttribute"/>/<see cref="MapConditionAttribute"/>/
/// <see cref="MapUsingAttribute"/>/<see cref="MapDefaultAttribute"/> already use).
///
/// A mapping carrying at least one <see cref="MapIncludeAttribute"/> does not generate a
/// two-argument <c>To{Dest}(source, destination)</c> overload - there's no way to populate a
/// caller-supplied base-typed instance as if it were a derived-typed instance instead. It also
/// does not generate a <c>ProjectTo{Dest}()</c> SQL projection - a runtime type-switch can't be
/// expressed as a query-provider-translatable expression tree. Combining
/// <see cref="MapToAttribute.GenerateReverse"/> with <see cref="MapIncludeAttribute"/> is not
/// supported either, since reversing a type-switch has no runtime-type signal to switch back on
/// without a discriminator property.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class MapIncludeAttribute : Attribute
{
    /// <summary>
    /// Declares that <paramref name="derivedSourceType"/> should map to
    /// <paramref name="derivedDestinationType"/> instead of <paramref name="destinationType"/>
    /// whenever the decorated type's runtime instance is actually a
    /// <paramref name="derivedSourceType"/>.
    /// </summary>
    public MapIncludeAttribute(Type destinationType, Type derivedSourceType, Type derivedDestinationType)
    {
        DestinationType = destinationType;
        DerivedSourceType = derivedSourceType;
        DerivedDestinationType = derivedDestinationType;
    }

    /// <summary>The mapping this include augments (see the remarks above for the <see cref="MapFromAttribute"/> case).</summary>
    public Type DestinationType { get; }

    /// <summary>The derived source type dispatched to at runtime.</summary>
    public Type DerivedSourceType { get; }

    /// <summary>The derived destination type <see cref="DerivedSourceType"/> maps to.</summary>
    public Type DerivedDestinationType { get; }
}

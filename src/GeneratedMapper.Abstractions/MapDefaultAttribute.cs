namespace GeneratedMapper;

/// <summary>
/// Substitutes a constant value when the matched source property is <see langword="null"/>,
/// instead of assigning <see langword="null"/> through. Emits <c>source.Prop ?? defaultValue</c>
/// in place of the plain property access, for both the imperative mapper and SQL projections
/// (translated as <c>COALESCE</c>).
/// </summary>
/// <remarks>
/// Only takes effect on a directly-matched property, or one computed via
/// <see cref="MapUsingAttribute"/> — and only when the value's type can actually hold
/// <see langword="null"/> (a reference type, or <c>Nullable&lt;T&gt;</c>); it has no effect on a
/// nested/enumerable property or a non-nullable value type. The default value is an attribute
/// constructor argument, so it's limited to what Roslyn allows there: a numeric,
/// string, bool, char, or enum constant — not an arbitrary expression. Not auto-reversed by
/// <see cref="MapToAttribute.GenerateReverse"/>: declare a separate <see cref="MapDefaultAttribute"/>
/// on the destination type if the reverse direction needs one.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class MapDefaultAttribute : Attribute
{
    /// <summary>
    /// Declares that <paramref name="destinationProperty"/> on <paramref name="destinationType"/>
    /// falls back to <paramref name="defaultValue"/> whenever its matched source value would
    /// otherwise be <see langword="null"/>.
    /// </summary>
    public MapDefaultAttribute(Type destinationType, string destinationProperty, object? defaultValue)
    {
        DestinationType = destinationType;
        DestinationProperty = destinationProperty;
        DefaultValue = defaultValue;
    }

    /// <summary>The mapping this default applies to.</summary>
    public Type DestinationType { get; }

    /// <summary>The property on <see cref="DestinationType"/> that gets the fallback value.</summary>
    public string DestinationProperty { get; }

    /// <summary>The constant substituted when the matched value would otherwise be <see langword="null"/>.</summary>
    public object? DefaultValue { get; }
}

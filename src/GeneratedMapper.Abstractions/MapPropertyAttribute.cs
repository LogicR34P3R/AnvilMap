namespace GeneratedMapper;

/// <summary>
/// Explicitly maps a named property on the decorated source type to a named property on
/// the destination type, overriding the generator's default exact-name match. Use this when
/// the destination property name
/// doesn't match the source property name (e.g. <c>OwnerEmail</c> on the source mapping to
/// <c>Author</c> on the destination).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class MapPropertyAttribute : Attribute
{
    /// <summary>
    /// Declares that <paramref name="sourceProperty"/> maps to
    /// <paramref name="destinationProperty"/> on <paramref name="destinationType"/>.
    /// </summary>
    public MapPropertyAttribute(Type destinationType, string sourceProperty, string destinationProperty)
    {
        DestinationType = destinationType;
        SourceProperty = sourceProperty;
        DestinationProperty = destinationProperty;
    }

    /// <summary>The mapping this override applies to.</summary>
    public Type DestinationType { get; }

    /// <summary>The property on the decorated source type to read from.</summary>
    public string SourceProperty { get; }

    /// <summary>The property on <see cref="DestinationType"/> to write to.</summary>
    public string DestinationProperty { get; }
}

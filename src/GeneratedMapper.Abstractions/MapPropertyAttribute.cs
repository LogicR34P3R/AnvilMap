namespace GeneratedMapper;

/// <summary>
/// Explicitly maps a named property on the decorated source type to a named property on
/// the destination type, overriding the generator's default exact-name match. Use this when
/// the destination property name
/// doesn't match the source property name (e.g. <c>OwnerEmail</c> on the source mapping to
/// <c>Author</c> on the destination).
/// </summary>
/// <remarks>
/// Can also be placed on a destination type decorated with <see cref="MapFromAttribute"/>
/// instead of on the source - in that case, pass the source type as
/// <c>destinationType</c> (naming the mapping this override applies to, same as when declared
/// on the source side); <c>sourceProperty</c> and <c>destinationProperty</c> keep their usual
/// meaning either way.
/// </remarks>
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

namespace GeneratedMapper;

/// <summary>
/// Excludes the decorated destination property from a generated mapping into its declaring
/// type - the property is left at its default (or whatever the destination's own
/// constructor/object-initializer set) rather than being reported as unmapped
/// (diagnostic GM001).
/// </summary>
/// <remarks>
/// With no constructor argument, the property is excluded from every generated mapping into
/// its declaring type, regardless of source. Pass a source type to scope the exclusion to just
/// that mapping, leaving the property mapped normally for every other source - e.g. decorate
/// the same property with two <see cref="MapIgnoreAttribute"/> instances (one per source type)
/// to ignore it for one source but not the other. Repeatable for exactly this reason. Since
/// this attribute always lives on the receiving (destination) property, the same property is
/// simply never inspected while resolving a mapping in which its declaring type is the source
/// instead - a reverse mapping generated via <c>GenerateReverse</c> needs its own
/// <see cref="MapIgnoreAttribute"/> on the other type's property if it should be excluded too.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
public sealed class MapIgnoreAttribute : Attribute
{
    /// <summary>Excludes the decorated property from every generated mapping into its declaring type.</summary>
    public MapIgnoreAttribute()
    {
    }

    /// <summary>
    /// Excludes the decorated property only from mappings whose source is
    /// <paramref name="sourceType"/>; mappings from any other source still map it normally.
    /// </summary>
    public MapIgnoreAttribute(Type sourceType) => SourceType = sourceType;

    /// <summary>
    /// The mapping this exclusion is scoped to, or <see langword="null"/> to exclude the
    /// property from every mapping into its declaring type regardless of source.
    /// </summary>
    public Type? SourceType { get; }
}

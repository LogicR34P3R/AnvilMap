namespace GeneratedMapper;

/// <summary>
/// Excludes the decorated destination property from every generated mapping into its
/// declaring type - the property is left at its default (or whatever the destination's own
/// constructor/object-initializer set) rather than being reported as unmapped
/// (diagnostic GM001).
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class MapIgnoreAttribute : Attribute
{
}

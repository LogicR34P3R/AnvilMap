namespace GeneratedMapper;

/// <summary>
/// Declares that the decorated destination type (e.g. a DTO or view model) is generated from
/// the source type passed to the constructor: a <c>To{Dest}()</c> extension method on the
/// source, a SQL-projection <c>ProjectTo{Dest}()</c> extension method (for
/// <c>IQueryable&lt;TSource&gt;</c>), and an entry in the generated dispatcher and
/// <see cref="IMapper"/> service - functionally identical to declaring the same mapping with
/// <see cref="MapToAttribute"/> on the source type, just placed on the other side. Use this
/// when the source type shouldn't reference the destination type (e.g. a domain entity that
/// shouldn't know about the DTOs/view models built from it) - the destination type is usually
/// already allowed to reference the source. <see cref="MapPropertyAttribute"/>,
/// <see cref="MapConditionAttribute"/>, <see cref="MapUsingAttribute"/>, and
/// <see cref="MapDefaultAttribute"/> can all be placed alongside this attribute on the same
/// destination type, in which case their <c>Type</c> constructor argument identifies the
/// source type (matching this attribute's <see cref="SourceType"/>) instead of a destination
/// type, and any named condition/converter method is looked up on the destination type instead
/// of the source. Repeatable - decorate the same destination type with multiple
/// <see cref="MapFromAttribute"/> instances to generate mappings from multiple source types.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class MapFromAttribute : Attribute
{
    /// <summary>Declares a mapping from <paramref name="sourceType"/> to the decorated type.</summary>
    public MapFromAttribute(Type sourceType) => SourceType = sourceType;

    /// <summary>The type this mapping is produced from.</summary>
    public Type SourceType { get; }

    /// <summary>
    /// When <see langword="true"/>, also generates the reverse mapping (from the decorated
    /// type back to <see cref="SourceType"/>).
    /// <see cref="MapConditionAttribute"/> and <see cref="MapUsingAttribute"/> declarations are
    /// not carried over to the reverse mapping, since their named methods are tied to the
    /// original source type - declare a separate attribute on the source type if the reverse
    /// direction needs one.
    /// </summary>
    public bool GenerateReverse { get; set; }

    /// <summary>
    /// Guards a mapping that directly maps into itself against unbounded recursion on a cyclic
    /// runtime object graph. 0 (the default) means unlimited. See
    /// <see cref="MapToAttribute.MaxDepth"/> for the full explanation - behaves identically here.
    /// </summary>
    public int MaxDepth { get; set; }
}

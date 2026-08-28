namespace AnvilMap;

/// <summary>
/// Declares that the decorated source type generates a mapping to the destination type
/// passed to the constructor: a <c>To{Dest}()</c> extension method, a SQL-projection
/// <c>ProjectTo{Dest}()</c> extension method (for <c>IQueryable&lt;TSource&gt;</c>), and an
/// entry in the generated dispatcher and <see cref="IMapper"/> service. Repeatable - decorate
/// the same source type with multiple <see cref="MapToAttribute"/> instances to generate
/// mappings to multiple destination types.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class MapToAttribute : Attribute
{
    /// <summary>Declares a mapping from the decorated type to <paramref name="destinationType"/>.</summary>
    public MapToAttribute(Type destinationType) => DestinationType = destinationType;

    /// <summary>The type this mapping produces.</summary>
    public Type DestinationType { get; }

    /// <summary>
    /// When <see langword="true"/>, also generates the reverse mapping (from
    /// <see cref="DestinationType"/> back to the decorated source type).
    /// <see cref="MapConditionAttribute"/> and <see cref="MapUsingAttribute"/> declarations are
    /// not carried over to the reverse mapping, since their named methods are tied to the
    /// original source type - declare a separate attribute on the destination type if the
    /// reverse direction needs one.
    /// </summary>
    public bool GenerateReverse { get; set; }

    /// <summary>
    /// Guards a mapping that directly maps into itself (e.g. a <c>Category</c> whose
    /// <c>Children</c>/<c>Parent</c> also map to <c>Category</c>) against unbounded
    /// recursion on a cyclic runtime object graph. 0 (the default) means unlimited,
    /// matching prior behavior. Only guards properties whose type is this exact
    /// source/destination pair — it does not detect indirect cycles across multiple
    /// different <see cref="MapToAttribute"/> declarations.
    /// </summary>
    public int MaxDepth { get; set; }
}

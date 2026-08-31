namespace AnvilMap.Generator;

// Whether a source collection's element count is known up front without enumerating, and which
// member gives it - None means "not cheaply known", so MappingEmitter falls back to the existing
// LINQ-expression materialization instead of a presized loop.
internal enum SourceCountAccessor
{
    None,
    Count,
    Length
}

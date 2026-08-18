using Microsoft.CodeAnalysis;

namespace GeneratedMapper.Generator;

// A small, equatable snapshot of an ITypeSymbol's three name forms this generator actually
// needs, taken once during discovery/resolution. Roslyn symbols themselves (ITypeSymbol,
// INamedTypeSymbol, ...) are tied to a specific Compilation and aren't safely comparable
// across incremental-generator runs, so they can't be cached inside an incremental pipeline
// value the way this plain record can - keeping this record's shape stable is what lets
// Roslyn's IncrementalValuesProvider cache correctly between edits.
internal sealed record TypeModel(string FullyQualifiedName, string DisplayName, string SimpleName)
{
    public static TypeModel From(ITypeSymbol symbol)
        => new(
            symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            symbol.ToDisplayString(),
            symbol.Name);
}

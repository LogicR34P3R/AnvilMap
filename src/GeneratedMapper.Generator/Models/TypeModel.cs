using Microsoft.CodeAnalysis;

namespace GeneratedMapper.Generator;

// A small, equatable snapshot of an ITypeSymbol's name forms. Roslyn symbols aren't safely
// comparable across incremental-generator runs, so this plain record is what actually gets
// cached inside the pipeline.
internal sealed record TypeModel(string FullyQualifiedName, string DisplayName, string SimpleName)
{
    public static TypeModel From(ITypeSymbol symbol)
        => new(
            symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            symbol.ToDisplayString(),
            symbol.Name);
}

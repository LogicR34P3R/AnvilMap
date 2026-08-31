using Microsoft.CodeAnalysis;

namespace AnvilMap.Generator;

internal sealed record ExplicitIncludeMapping(
    TypeModel DerivedSourceType,
    TypeModel DerivedDestinationType,
    INamedTypeSymbol DerivedSourceSymbol,
    INamedTypeSymbol DerivedDestinationSymbol)
{
    // Same treatment as MappingDeclaration's own Equals/GetHashCode override - the raw symbol
    // fields aren't safely comparable across incremental-generator runs, so equality is scoped
    // to the equatable TypeModel pair only.
    public bool Equals(ExplicitIncludeMapping? other) =>
        other is not null
        && DerivedSourceType == other.DerivedSourceType
        && DerivedDestinationType == other.DerivedDestinationType;

    public override int GetHashCode() => HashCombine.Combine(DerivedSourceType, DerivedDestinationType);
}

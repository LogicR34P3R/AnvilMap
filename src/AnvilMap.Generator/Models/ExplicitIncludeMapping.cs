using Microsoft.CodeAnalysis;

namespace AnvilMap.Generator;

internal sealed record ExplicitIncludeMapping(
    TypeModel DerivedSourceType,
    TypeModel DerivedDestinationType,
    INamedTypeSymbol DerivedSourceSymbol,
    INamedTypeSymbol DerivedDestinationSymbol);

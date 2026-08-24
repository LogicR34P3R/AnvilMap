using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace GeneratedMapper.Generator;

// [MapCondition] resolution: finds the static bool method it names on the method-host type
// (the type that physically carries [MapTo]/[MapFrom] and its companion attributes - the
// source type for a [MapTo]-declared mapping, but possibly the destination type for a
// [MapFrom]-declared one), in either the one-arg or two-arg accepted signature.
internal static partial class MappingResolver
{
    // Prefers the two-arg (TSource, TDestination?) signature over one-arg when both exist, so
    // a condition can inspect the destination's current state. The method itself is looked up
    // on methodHost, but its parameter types must still match source/destination exactly -
    // methodHost only decides *where* the method may be declared, not what it's shaped like.
    private static ConditionResolution ResolveCondition(
        INamedTypeSymbol methodHost,
        INamedTypeSymbol source,
        INamedTypeSymbol destination,
        IPropertySymbol destinationProperty,
        IReadOnlyDictionary<string, string> explicitConditions,
        Action<Diagnostic>? report)
    {
        if (!explicitConditions.TryGetValue(destinationProperty.Name, out var conditionName))
            return new ConditionResolution(true, null, false);

        var candidates = methodHost.GetMembers(conditionName)
            .OfType<IMethodSymbol>()
            .Where(m => m.IsStatic && m.ReturnType.SpecialType == SpecialType.System_Boolean)
            .ToArray();

        var twoArg = candidates.FirstOrDefault(m =>
            m.Parameters.Length == 2 &&
            SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, source) &&
            SymbolEqualityComparer.Default.Equals(m.Parameters[1].Type, destination));

        if (twoArg is not null)
            return new ConditionResolution(true, conditionName, true);

        var oneArg = candidates.FirstOrDefault(m =>
            m.Parameters.Length == 1 &&
            SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, source));

        if (oneArg is not null)
            return new ConditionResolution(true, conditionName, false);

        // Properties let GeneratedMapper.CodeFixes locate the method-host type (where the stub
        // is inserted) and the source type (the stub's required parameter type - these differ
        // for a [MapFrom]-declared mapping, where the method lives on the destination but its
        // first parameter is still TSource) without parsing the message text.
        report?.Invoke(Diagnostic.Create(
            Diagnostics.ConditionMethodNotFound,
            destinationProperty.Locations.FirstOrDefault() ?? Location.None,
            ImmutableDictionary<string, string?>.Empty
                .Add("MethodHostMetadataName", GetMetadataName(methodHost))
                .Add("SourceMetadataName", GetMetadataName(source))
                .Add("MethodName", conditionName),
            source.ToDisplayString(),
            destinationProperty.Name,
            conditionName,
            destination.ToDisplayString()));

        return new ConditionResolution(false, null, false);
    }

    // Success = false means GM004 was already reported; caller skips the property.
    private readonly record struct ConditionResolution(
        bool Success,
        string? MethodName,
        bool AcceptsDestination);
}

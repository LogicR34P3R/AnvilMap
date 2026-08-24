using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace GeneratedMapper.Generator;

// [MapUsing] resolution: finds the static converter method it names on the method-host type
// (the type that physically carries [MapTo]/[MapFrom] and its companion attributes - the
// source type for a [MapTo]-declared mapping, but possibly the destination type for a
// [MapFrom]-declared one).
internal static partial class MappingResolver
{
    // Prefers an exact return-type match, falling back to an implicit conversion. The method
    // itself is looked up on methodHost, but its parameter type must still be exactly source -
    // methodHost only decides *where* the method may be declared, not what it's shaped like.
    private static string? ResolveConverter(
        Compilation compilation,
        INamedTypeSymbol methodHost,
        INamedTypeSymbol source,
        IPropertySymbol destinationProperty,
        string converterMethodName,
        Action<Diagnostic>? report)
    {
        var candidates = methodHost.GetMembers(converterMethodName)
            .OfType<IMethodSymbol>()
            .Where(m => m.IsStatic &&
                m.Parameters.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, source))
            .ToArray();

        var match = candidates.FirstOrDefault(m =>
            SymbolEqualityComparer.Default.Equals(m.ReturnType, destinationProperty.Type));

        if (match is null && compilation is CSharpCompilation csharpCompilation)
        {
            match = candidates.FirstOrDefault(m =>
                csharpCompilation.ClassifyConversion(m.ReturnType, destinationProperty.Type).IsImplicit);
        }

        if (match is not null)
            return converterMethodName;

        report?.Invoke(Diagnostic.Create(
            Diagnostics.ConverterMethodNotFound,
            destinationProperty.Locations.FirstOrDefault() ?? Location.None,
            ImmutableDictionary<string, string?>.Empty
                .Add("MethodHostMetadataName", GetMetadataName(methodHost))
                .Add("SourceMetadataName", GetMetadataName(source))
                .Add("MethodName", converterMethodName)
                .Add("ReturnType", destinationProperty.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),
            source.ToDisplayString(),
            destinationProperty.Name,
            converterMethodName,
            destinationProperty.Type.ToDisplayString()));

        return null;
    }
}

using System;
using System.Linq;
using AnvilMap.CodeFixContracts;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AnvilMap.Generator;

// [MapUsing] resolution: finds the static converter method it names on the method-host type
// (the type that physically carries [MapTo]/[MapFrom] and its companion attributes - the
// source type for a [MapTo]-declared mapping, but possibly the destination type for a
// [MapFrom]-declared one).
internal static partial class MappingResolver
{
    // Prefers an exact return-type match, falling back to an implicit conversion. The method
    // itself is looked up on methodHost, but its parameter type must still be exactly source -
    // methodHost only decides *where* the method may be declared, not what it's shaped like.
    private static IMethodSymbol? ResolveConverter(
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
        {
            return match;
        }

        report?.Invoke(Diagnostic.Create(
            Diagnostics.ConverterMethodNotFound,
            destinationProperty.Locations.FirstOrDefault() ?? Location.None,
            new StubMethodDiagnosticProperties(
                    GetMetadataName(methodHost),
                    GetMetadataName(source),
                    converterMethodName,
                    destinationProperty.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .ToImmutableDictionary(),
            source.ToDisplayString(),
            destinationProperty.Name,
            converterMethodName,
            destinationProperty.Type.ToDisplayString()));

        return null;
    }
}

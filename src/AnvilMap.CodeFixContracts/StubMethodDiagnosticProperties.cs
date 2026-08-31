using System.Collections.Immutable;

namespace AnvilMap.CodeFixContracts;

// The AM004/AM009 diagnostic-property contract between AnvilMap.Generator (reporting side) and
// AnvilMap.CodeFixes' GenerateStubMethodCodeFixProvider (reading side) - replaces a raw
// string-keyed dictionary both ends used to build/read by hand, with no compiler-checked link.
public sealed record StubMethodDiagnosticProperties(
    string MethodHostMetadataName,
    string SourceMetadataName,
    string MethodName,
    string ReturnType)
{
    private const string MethodHostMetadataNameKey = "MethodHostMetadataName";
    private const string SourceMetadataNameKey = "SourceMetadataName";
    private const string MethodNameKey = "MethodName";
    private const string ReturnTypeKey = "ReturnType";

    public ImmutableDictionary<string, string?> ToImmutableDictionary()
        => ImmutableDictionary<string, string?>.Empty
            .Add(MethodHostMetadataNameKey, MethodHostMetadataName)
            .Add(SourceMetadataNameKey, SourceMetadataName)
            .Add(MethodNameKey, MethodName)
            .Add(ReturnTypeKey, ReturnType);

    public static StubMethodDiagnosticProperties? TryParse(ImmutableDictionary<string, string?> properties)
    {
        if (properties.TryGetValue(MethodHostMetadataNameKey, out var methodHost) && methodHost is not null &&
            properties.TryGetValue(SourceMetadataNameKey, out var source) && source is not null &&
            properties.TryGetValue(MethodNameKey, out var methodName) && methodName is not null &&
            properties.TryGetValue(ReturnTypeKey, out var returnType) && returnType is not null)
        {
            return new StubMethodDiagnosticProperties(methodHost, source, methodName, returnType);
        }

        return null;
    }
}

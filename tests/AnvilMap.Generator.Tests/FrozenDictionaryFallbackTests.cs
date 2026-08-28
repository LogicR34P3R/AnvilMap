using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AnvilMap.Generator.Tests;

// System.Collections.Frozen.FrozenDictionary only exists on .NET 8+. The generator itself
// targets netstandard2.0 so it can run against any consumer's compilation, including one
// targeting something older than net8.0 - for those, the dispatcher must fall back to a plain
// Dictionary instead of emitting a type reference the consumer can't resolve. The test host
// here is net8.0, so simulating an older consumer means removing whichever assembly actually
// defines FrozenDictionary<,> from the reference set, rather than hardcoding a TFM.
public sealed class FrozenDictionaryFallbackTests
{
    private const string Source = @"
using AnvilMap;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}

public sealed class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}
";

    private static MetadataReference[] ReferencesWithoutFrozenDictionary()
    {
        var probe = CSharpCompilation.Create(
            "Probe",
            references: GeneratorTestHelper.PlatformReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var frozenDictionarySymbol = probe.GetTypeByMetadataName("System.Collections.Frozen.FrozenDictionary`2");
        Assert.NotNull(frozenDictionarySymbol);

        var frozenAssemblyName = frozenDictionarySymbol!.ContainingAssembly.Identity.Name;

        return GeneratorTestHelper.PlatformReferences
            .Where(r => !(r.Display ?? "").Contains(frozenAssemblyName, StringComparison.OrdinalIgnoreCase))
            .Append(MetadataReference.CreateFromFile(typeof(MapToAttribute).Assembly.Location))
            .ToArray();
    }

    [Fact]
    public void FrozenDictionaryUnavailable_DispatcherFallsBackToPlainDictionaryAndStillCompiles()
    {
        var references = ReferencesWithoutFrozenDictionary();

        // Confirm the removal actually worked before trusting anything else below - otherwise
        // this test would pass vacuously against the normal, FrozenDictionary-available path.
        var check = CSharpCompilation.Create(
            "Check",
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.Null(check.GetTypeByMetadataName("System.Collections.Frozen.FrozenDictionary`2"));

        var result = GeneratorTestHelper.Run(Source, references);

        Assert.NotNull(result.GeneratedSource);
        // Precise checks on the actual field declarations/using directive, not a blanket
        // "does the word FrozenDictionary appear anywhere" - the fallback path's own generated
        // comment legitimately names FrozenDictionary to explain why it *wasn't* used.
        Assert.DoesNotContain("readonly FrozenDictionary<", result.GeneratedSource);
        Assert.DoesNotContain("using System.Collections.Frozen;", result.GeneratedSource);
        Assert.Contains(
            "private static readonly Dictionary<(Type Source, Type Destination), Func<object, object>> _map =",
            result.GeneratedSource);
        Assert.Contains(
            "private static readonly Dictionary<(Type Source, Type Destination), Func<object, object, object>> _mapInto =",
            result.GeneratedSource);

        var errors = result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors.Select(e => e.ToString())));
    }

    [Fact]
    public void FrozenDictionaryAvailable_DispatcherStillUsesFrozenDictionary()
    {
        var result = GeneratorTestHelper.Run(Source);

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("using System.Collections.Frozen;", result.GeneratedSource);
        Assert.Contains(
            "private static readonly FrozenDictionary<(Type Source, Type Destination), Func<object, object>> _map =",
            result.GeneratedSource);
    }
}

using System;
using System.Linq;
using System.IO;
using System.Reflection;
using GeneratedMapper.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace GeneratedMapper.Generator.Tests;

internal static class GeneratorTestHelper
{
    // Exposed (not private) so tests that need to simulate a consumer compilation missing a
    // given BCL type - e.g. FrozenDictionaryFallbackTests, which removes whichever assembly
    // defines System.Collections.Frozen.FrozenDictionary to simulate a pre-.NET 8 consumer -
    // can start from the same base reference set instead of duplicating this logic.
    public static readonly MetadataReference[] PlatformReferences =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToArray();

    public static GeneratorTestResult Run(string source)
        => Run(source, PlatformReferences.Append(MetadataReference.CreateFromFile(typeof(MapToAttribute).Assembly.Location)).ToArray());

    public static GeneratorTestResult Run(string source, MetadataReference[] references)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));

        var compilation = CSharpCompilation.Create(
            "GeneratedMapper.Generator.Tests.Target",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver.Create(new MappingSourceGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        var runResult = driver.GetRunResult();

        var generatedSource = runResult.Results
            .SelectMany(r => r.GeneratedSources)
            .FirstOrDefault(s => s.HintName == "GeneratedMappings.g.cs")
            .SourceText?.ToString();

        var compilationDiagnostics = outputCompilation.GetDiagnostics();
        Assembly? assembly = null;

        if (!compilationDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
        {
            using var stream = new MemoryStream();
            var emitResult = outputCompilation.Emit(stream);

            if (emitResult.Success)
                assembly = Assembly.Load(stream.ToArray());
        }

        return new GeneratorTestResult(
            generatedSource,
            runResult.Diagnostics,
            compilationDiagnostics,
            assembly);
    }
}

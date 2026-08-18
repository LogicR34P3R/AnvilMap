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
        => Run(source, references, new CSharpParseOptions(LanguageVersion.Latest));

    // The parseOptions overload exists for tests that need to simulate an older consumer
    // LangVersion (e.g. LanguageVersionFallbackTests) - it's passed to *both* the input syntax
    // tree and the generator driver, so the generated source is itself parsed/compiled under
    // that same version, matching what a real consumer's build would actually experience
    // (their compiler parses every file in the compilation, generator-added or not, under one
    // project-wide LangVersion).
    public static GeneratorTestResult Run(string source, MetadataReference[] references, CSharpParseOptions parseOptions)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);

        // NullableContextOptions.Enable is itself invalid (CS8630) below C# 8 - mirrors the
        // same constraint the generator's own useNullableReferenceTypes detection is built
        // around, just applied to this test compilation's options rather than generated output.
        var nullableContextOptions = parseOptions.LanguageVersion >= LanguageVersion.CSharp8
            ? NullableContextOptions.Enable
            : NullableContextOptions.Disable;

        var compilation = CSharpCompilation.Create(
            "GeneratedMapper.Generator.Tests.Target",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: nullableContextOptions));

        var driver = CSharpGeneratorDriver.Create(
                new[] { new MappingSourceGenerator().AsSourceGenerator() },
                parseOptions: parseOptions)
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

using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace AnvilMap.Generator.Tests;

internal sealed record GeneratorTestResult(
    string? GeneratedSource,
    ImmutableArray<Diagnostic> GeneratorDiagnostics,
    ImmutableArray<Diagnostic> CompilationDiagnostics,
    Assembly? Assembly);

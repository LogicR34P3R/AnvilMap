using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace GeneratedMapper.Generator.Tests;

// #nullable enable, the null-forgiving `!` operator, file-scoped namespaces, and `is null`
// pattern-matched against an open generic type parameter all require specific C# language
// versions - a consumer without an explicit <LangVersion> gets whatever their TargetFramework
// defaults to, and netstandard2.0 (and net5.0, for the file-scoped-namespace case specifically)
// default to versions below what earlier generated output required. These tests compile the
// generated file itself under a pinned LanguageVersion, the same way a real consumer's build
// would, rather than just asserting on emitted text.
public sealed class LanguageVersionFallbackTests
{
    // No `?`/nullable syntax anywhere in this fixture - deliberately, so it parses under any
    // LangVersion including 7.3, and any compile error surfaced can only come from the
    // generator's own output, not from this test's input being unparseable itself.
    private const string PlainNestedSource = @"
using GeneratedMapper;

namespace Sample
{

[MapTo(typeof(OrderDto))]
public sealed class Order
{
    public int Id { get; set; }
    public Customer Customer { get; set; }
}

[MapTo(typeof(CustomerDto))]
public sealed class Customer
{
    public string Name { get; set; }
}

public sealed class OrderDto
{
    public int Id { get; set; }
    public CustomerDto Customer { get; set; }
}

public sealed class CustomerDto
{
    public string Name { get; set; }
}
}
";

    private const string NullableNestedSource = @"
using GeneratedMapper;

namespace Sample
{

[MapTo(typeof(OrderDto))]
public sealed class Order
{
    public int Id { get; set; }
    public Customer? Customer { get; set; }
}

[MapTo(typeof(CustomerDto))]
public sealed class Customer
{
    public string Name { get; set; } = """";
}

public sealed class OrderDto
{
    public int Id { get; set; }
    public CustomerDto? Customer { get; set; }
}

public sealed class CustomerDto
{
    public string Name { get; set; } = """";
}
}
";

    private static MetadataReference[] References()
        => GeneratorTestHelper.PlatformReferences
            .Append(MetadataReference.CreateFromFile(typeof(MapToAttribute).Assembly.Location))
            .ToArray();

    [Fact]
    public void FullOutput_CompilesCleanlyUnderCSharp7_3()
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp7_3);

        var result = GeneratorTestHelper.Run(PlainNestedSource, References(), parseOptions);

        Assert.NotNull(result.GeneratedSource);
        // The three C# 7.3-incompatible constructs this fixes: no nullable pragma, no
        // file-scoped namespace, and the generic dispatcher uses ReferenceEquals instead of
        // `is null` against TSource/TDestination.
        Assert.DoesNotContain("#nullable", result.GeneratedSource);
        Assert.DoesNotContain("namespace GeneratedMapper;", result.GeneratedSource);
        Assert.Contains("namespace GeneratedMapper" + Environment.NewLine + "{", result.GeneratedSource);
        Assert.Contains("object.ReferenceEquals(source, null)", result.GeneratedSource);

        var errors = result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors.Select(e => e.ToString())));
    }

    [Fact]
    public void NullableNestedProperty_UnderCSharp8_StillEmitsNullForgivingOperatorAndCompiles()
    {
        // C# 8 is the exact boundary - the lowest version where #nullable/`!`/`Type?` are all
        // valid - so this doubles as confirming useNullableReferenceTypes' `>=` comparison is
        // right at the edge, not just "true somewhere far above the minimum".
        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp8);

        var result = GeneratorTestHelper.Run(NullableNestedSource, References(), parseOptions);

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("#nullable enable", result.GeneratedSource);
        Assert.Contains("source.Customer?.ToCustomerDto()!", result.GeneratedSource);
        Assert.Contains("namespace GeneratedMapper" + Environment.NewLine + "{", result.GeneratedSource);
        Assert.Contains("object.ReferenceEquals(source, null)", result.GeneratedSource);

        var errors = result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors.Select(e => e.ToString())));
    }
}

using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace GeneratedMapper.Generator.Tests;

// The trim-suppression attributes only exist on net6+, not netstandard2.0 - emitting them
// unconditionally broke compilation for those consumers (verified: CS0234). Unlike FrozenDictionary,
// this attribute lives in System.Private.CoreLib itself, so FrozenDictionaryFallbackTests' "strip
// one assembly" trick would break everything, not just this type. Instead, this builds a genuinely
// minimal netstandard2.0 compilation from the real installed NETStandard.Library.Ref pack plus this
// repo's own netstandard2.0 build of GeneratedMapper.Abstractions.dll.
public sealed class TrimSuppressionFallbackTests
{
    private const string Source = @"
using GeneratedMapper;

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

    private static MetadataReference[] NetStandard20References()
    {
        // System.Private.CoreLib.dll lives at <dotnetRoot>/shared/Microsoft.NETCore.App/<version>/ -
        // three directories up from there is the dotnet root, regardless of machine/install path.
        var coreLibDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var dotnetRoot = Path.GetFullPath(Path.Combine(coreLibDir, "..", "..", ".."));

        var netstandardDll = Directory.GetFiles(
                Path.Combine(dotnetRoot, "packs", "NETStandard.Library.Ref"),
                "netstandard.dll",
                SearchOption.AllDirectories)
            .OrderByDescending(f => f)
            .First();

        // Not hardcoded to Debug or Release - whichever configuration actually built this
        // solution (CI builds Release; a local run is usually Debug) is the one that exists.
        var abstractionsBinDir = Path.Combine(FindRepoRoot(), "src", "GeneratedMapper.Abstractions", "bin");
        var abstractionsNetStandardDll = Directory.GetFiles(abstractionsBinDir, "GeneratedMapper.Abstractions.dll", SearchOption.AllDirectories)
            .FirstOrDefault(f => f.Contains(Path.Combine("netstandard2.0", "GeneratedMapper.Abstractions.dll")));

        Assert.True(abstractionsNetStandardDll is not null, $"Expected to find a netstandard2.0 GeneratedMapper.Abstractions.dll under {abstractionsBinDir} - build GeneratedMapper.Abstractions (netstandard2.0) first.");

        return new[]
        {
            MetadataReference.CreateFromFile(netstandardDll),
            MetadataReference.CreateFromFile(abstractionsNetStandardDll!),
        };
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent!)
        {
            if (File.Exists(Path.Combine(dir.FullName, "GeneratedMapper.sln")))
                return dir.FullName;
        }

        throw new InvalidOperationException("Could not find GeneratedMapper.sln walking up from " + AppContext.BaseDirectory);
    }

    [Fact]
    public void TrimAttributesUnavailable_ProjectionOmitsThemAndStillCompiles()
    {
        var references = NetStandard20References();

        // Confirm this reference set genuinely lacks the type before trusting anything else
        // below - otherwise this test would pass vacuously against the normal, available path.
        var check = CSharpCompilation.Create(
            "Check",
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.Null(check.GetTypeByMetadataName("System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessageAttribute"));

        var result = GeneratorTestHelper.Run(Source, references);

        Assert.NotNull(result.GeneratedSource);
        var source = result.GeneratedSource!;

        Assert.DoesNotContain("UnconditionalSuppressMessage", source);
        Assert.DoesNotContain("DynamicDependency", source);
        // The explicit-static-constructor structure itself is plain C#, unaffected by capability -
        // still used (not reverted to an inline field initializer) even without the attributes.
        Assert.Contains(
            "public static readonly Expression<Func<global::Sample.User, global::Sample.UserDto>> UserToUserDtoProjection;",
            source);
        Assert.Contains("static GeneratedMappings()", source);
        Assert.Contains(
            "UserToUserDtoProjection = source => new global::Sample.UserDto { Id = source.Id, Name = source.Name };",
            source);

        var errors = result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors.Select(e => e.ToString())));
    }

    [Fact]
    public void TrimAttributesAvailable_ProjectionEmitsSuppressionAndDynamicDependency()
    {
        var result = GeneratorTestHelper.Run(Source);

        Assert.NotNull(result.GeneratedSource);
        var source = result.GeneratedSource!;

        Assert.Contains(
            "[System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties, typeof(global::Sample.UserDto))]",
            source);
        Assert.Contains("[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(\"Trimming\", \"IL2026\"", source);

        var errors = result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors.Select(e => e.ToString())));
    }

    [Fact]
    public void FullyConstructorCoveredDestination_ProjectionUsesNoObjectInitializer_EmitsNoAttributes()
    {
        // A positional record with every property covered by its constructor compiles to a pure
        // Expression.New(ctor, args) - no trailing `{ Prop = value }`, so no Expression.Bind call
        // and nothing for the trimmer to warn about. The attributes exist to protect a real risk;
        // a file where every mapping's projection happens to be constructor-only shouldn't carry
        // them at all.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}

public sealed record UserDto(int Id, string Name);
");

        Assert.NotNull(result.GeneratedSource);
        var source = result.GeneratedSource!;

        Assert.Contains("source => new global::Sample.UserDto(source.Id, source.Name);", source);
        Assert.DoesNotContain("DynamicDependency", source);
        Assert.DoesNotContain("UnconditionalSuppressMessage", source);
        // Still uses the explicit-static-constructor structure regardless - that part is
        // unconditional, plain C#, independent of whether any attribute is needed on top of it.
        Assert.Contains("static GeneratedMappings()", source);

        var errors = result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors.Select(e => e.ToString())));
    }

    [Fact]
    public void MixOfConstructorOnlyAndPlainDestinations_OnlyThePlainOneGetsTheAttributes()
    {
        // A file with both shapes at once - the constructor-only mapping's destination must not
        // appear in the (still correctly emitted) attributes for the other, plain mapping.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(OrderDto))]
public sealed class Order
{
    public int Id { get; set; }
    public string Reference { get; set; } = """";
}

[MapTo(typeof(LineDto))]
public sealed class Line
{
    public int Id { get; set; }
    public string Sku { get; set; } = """";
}

public sealed class OrderDto
{
    public int Id { get; set; }
    public string Reference { get; set; } = """";
}

public sealed record LineDto(int Id, string Sku);
");

        Assert.NotNull(result.GeneratedSource);
        var source = result.GeneratedSource!;

        Assert.Contains(
            "[System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties, typeof(global::Sample.OrderDto))]",
            source);
        // LineDto legitimately appears elsewhere (the dispatcher's (Type Source, Type
        // Destination) tuple keys) regardless of trim-attribute concerns - the precise thing to
        // rule out is a DynamicDependency naming it specifically.
        Assert.DoesNotContain(
            "DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties, typeof(global::Sample.LineDto))",
            source);
        Assert.Contains("source => new global::Sample.LineDto(source.Id, source.Sku);", source);

        var errors = result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors.Select(e => e.ToString())));
    }
}

using System.Runtime.CompilerServices;

namespace GeneratedMapper.InterceptorSmokeTests;

// Verifies interception end-to-end through a REAL MSBuild build - not the in-memory
// GeneratorTestHelper harness GeneratedMapper.Generator.Tests uses elsewhere. That harness proves
// the generator's own logic is correct; this project proves the whole real toolchain wires up
// correctly too (InterceptorsNamespaces, the real SDK/Roslyn, the real generated file on disk).
public class InterceptorSmokeTests
{
    [Fact]
    public void OneArgAndTwoArgClosedGenericCalls_ProduceCorrectResults()
    {
        var widget = new Widget { Id = 1, Name = "Widget" };

        var oneArgResult = Caller.CallOneArg(widget);
        Assert.Equal(1, oneArgResult.Id);
        Assert.Equal("Widget", oneArgResult.Name);

        var existing = new WidgetDto();
        var twoArgResult = Caller.CallTwoArg(widget, existing);
        Assert.Same(existing, twoArgResult);
        Assert.Equal(1, twoArgResult.Id);
        Assert.Equal("Widget", twoArgResult.Name);
    }

#if NET10_0_OR_GREATER
    [Fact]
    public void RealBuild_OnNet10_GeneratedOutputContainsRealInterceptorsForBothCallSites()
    {
        var text = ReadGeneratedMappingsSource();

        Assert.Contains("InterceptsLocation(", text);
        Assert.Contains("class Interceptors", text);
        // Both call sites (Caller.CallOneArg/CallTwoArg) share the (Widget, WidgetDto) pair but
        // different shapes, so they land in two distinct generated methods, not stacked together.
        Assert.Contains("Intercepted_WidgetDto_0(", text);
        Assert.Contains("Intercepted_WidgetDto_1(", text);
    }
#else
    [Fact]
    public void RealBuild_BelowNet10_GeneratedOutputHasNoInterceptor()
    {
        // Real-build control: proves the InterceptorsNamespaces wiring being present in this
        // project's own .csproj doesn't cause anything to be intercepted on a pre-C#14 target -
        // ConsumerCapabilities.UseCSharp14 alone gates emission.
        var text = ReadGeneratedMappingsSource();

        Assert.DoesNotContain("InterceptsLocation", text);
        Assert.DoesNotContain("class Interceptors", text);
    }
#endif

    private static string ReadGeneratedMappingsSource()
    {
        var path = Path.Combine(
            ProjectDirectory(),
            "Generated",
#if NET10_0_OR_GREATER
            "net10.0",
#else
            "net8.0",
#endif
            "GeneratedMapper.Generator",
            "GeneratedMapper.Generator.MappingSourceGenerator",
            "GeneratedMappings.g.cs");

        Assert.True(File.Exists(path), $"Expected the real generated file at {path} - EmitCompilerGeneratedFiles should have produced it during this project's own build.");

        return File.ReadAllText(path);
    }

    private static string ProjectDirectory([CallerFilePath] string here = "")
        => Path.GetDirectoryName(here)!;
}

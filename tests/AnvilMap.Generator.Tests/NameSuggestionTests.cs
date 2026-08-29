using Microsoft.CodeAnalysis;

namespace AnvilMap.Generator.Tests;

// AM001's "Did you mean 'X'?" suggestion (Q1 in docs/roadmapv4.md). NameSuggestion itself is
// internal with no InternalsVisibleTo, so - matching every other test in this project - this
// exercises it only through the generator's own public, observable output (the AM001 diagnostic
// message), never by calling the helper directly.
public class NameSuggestionTests
{
    [Fact]
    public void SingleCharacterTypo_SuggestsTheCloseSourceProperty()
    {
        var result = GeneratorTestHelper.Run(@"
using AnvilMap;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public string FirstName { get; set; } = """";
}

public sealed class UserDto
{
    public string FirstNam { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d =>
            d.Id == "AM001" && d.GetMessage().Contains("FirstNam") && d.GetMessage().Contains("Did you mean 'FirstName'?"));
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void UnrelatedName_NoSuggestionOffered()
    {
        // Mirrors NamingFlatteningTests.NoPossibleSplit_StillReportsAM001's fixture - genuinely
        // unrelated names shouldn't get a "did you mean" hint at all, only a plain AM001.
        var result = GeneratorTestHelper.Run(@"
using AnvilMap;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public string Id { get; set; } = """";
}

public sealed class UserDto
{
    public string CompletelyUnrelatedName { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d =>
            d.Id == "AM001" && d.GetMessage().Contains("CompletelyUnrelatedName"));
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "AM001" && d.GetMessage().Contains("Did you mean"));
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MultipleCandidates_SuggestsTheClosestOne()
    {
        var result = GeneratorTestHelper.Run(@"
using AnvilMap;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public string Email { get; set; } = """";
    public string EmailAddress { get; set; } = """";
}

public sealed class UserDto
{
    public string EmailAddres { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d =>
            d.Id == "AM001" && d.GetMessage().Contains("Did you mean 'EmailAddress'?"));
        AssertNoCompileErrors(result);
    }

    private static void AssertNoCompileErrors(GeneratorTestResult result)
    {
        var errors = result.CompilationDiagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.True(errors.Count == 0, string.Join("\n", errors.Select(e => e.ToString())));
    }
}

using Microsoft.CodeAnalysis;

namespace AnvilMap.Generator.Tests;

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
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void UnrelatedName_NoSuggestionOffered()
    {
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
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
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
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }
}

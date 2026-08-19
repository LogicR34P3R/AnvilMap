using System.Threading.Tasks;

namespace GeneratedMapper.CodeFixes.Tests;

public class GenerateStubMethodCodeFixProviderTests
{
    private const string ConditionSourceSingleFile = @"
using GeneratedMapper;

namespace TestNamespace;

[MapTo(typeof(PersonDto))]
[MapCondition(typeof(PersonDto), nameof(PersonDto.Name), nameof(ShouldMapName))]
public class Person
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}

public class PersonDto
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}
";

    [Fact]
    public async Task GM004_GeneratesBoolStubOnSourceType()
    {
        var solution = await CodeFixTestHelper.ApplyFixAsync(
            new GenerateStubMethodCodeFixProvider(), "GM004", ConditionSourceSingleFile);
        var text = await CodeFixTestHelper.GetDocumentTextAsync(solution, "Source0.cs");

        Assert.Contains("public static bool ShouldMapName(global::TestNamespace.Person source)", text);
        Assert.Contains("throw new global::System.NotImplementedException();", text);
    }

    private const string ConditionSourcePersonFile = @"
using GeneratedMapper;

namespace TestNamespace;

[MapTo(typeof(PersonDto))]
[MapCondition(typeof(PersonDto), nameof(PersonDto.Name), nameof(ShouldMapName))]
public class Person
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}
";

    private const string ConditionSourceDtoFile = @"
namespace TestNamespace;

public class PersonDto
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}
";

    [Fact]
    public async Task GM004_InsertsStubIntoTheSourceTypesOwnDocument_WhenDeclaredInADifferentFile()
    {
        var solution = await CodeFixTestHelper.ApplyFixAsync(
            new GenerateStubMethodCodeFixProvider(), "GM004", ConditionSourcePersonFile, ConditionSourceDtoFile);

        var personText = await CodeFixTestHelper.GetDocumentTextAsync(solution, "Source0.cs");
        var dtoText = await CodeFixTestHelper.GetDocumentTextAsync(solution, "Source1.cs");

        Assert.Contains("public static bool ShouldMapName(global::TestNamespace.Person source)", personText);
        Assert.DoesNotContain("ShouldMapName", dtoText);
    }

    private const string ConverterSourceSingleFile = @"
using GeneratedMapper;

namespace TestNamespace;

[MapTo(typeof(PersonDto))]
[MapUsing(typeof(PersonDto), nameof(PersonDto.Name), nameof(ComputeName))]
public class Person
{
    public int Id { get; set; }
}

public class PersonDto
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}
";

    [Fact]
    public async Task GM009_GeneratesStubReturningTheDestinationPropertysType()
    {
        var solution = await CodeFixTestHelper.ApplyFixAsync(
            new GenerateStubMethodCodeFixProvider(), "GM009", ConverterSourceSingleFile);
        var text = await CodeFixTestHelper.GetDocumentTextAsync(solution, "Source0.cs");

        Assert.Contains("public static string ComputeName(global::TestNamespace.Person source)", text);
        Assert.Contains("throw new global::System.NotImplementedException();", text);
    }

    private const string ConverterSourcePersonFile = @"
using GeneratedMapper;

namespace TestNamespace;

[MapTo(typeof(PersonDto))]
[MapUsing(typeof(PersonDto), nameof(PersonDto.Name), nameof(ComputeName))]
public class Person
{
    public int Id { get; set; }
}
";

    private const string ConverterSourceDtoFile = @"
namespace TestNamespace;

public class PersonDto
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}
";

    [Fact]
    public async Task GM009_InsertsStubIntoTheSourceTypesOwnDocument_WhenDeclaredInADifferentFile()
    {
        var solution = await CodeFixTestHelper.ApplyFixAsync(
            new GenerateStubMethodCodeFixProvider(), "GM009", ConverterSourcePersonFile, ConverterSourceDtoFile);

        var personText = await CodeFixTestHelper.GetDocumentTextAsync(solution, "Source0.cs");
        var dtoText = await CodeFixTestHelper.GetDocumentTextAsync(solution, "Source1.cs");

        Assert.Contains("public static string ComputeName(global::TestNamespace.Person source)", personText);
        Assert.DoesNotContain("ComputeName", dtoText);
    }
}

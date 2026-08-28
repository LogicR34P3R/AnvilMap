namespace AnvilMap.CodeFixes.Tests;

public class GenerateStubMethodCodeFixProviderTests
{
    private const string ConditionSourceSingleFile = @"
using AnvilMap;

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
    public async Task AM004_GeneratesBoolStubOnSourceType()
    {
        var solution = await CodeFixTestHelper.ApplyFixAsync(
            new GenerateStubMethodCodeFixProvider(), "AM004", ConditionSourceSingleFile);
        var text = await CodeFixTestHelper.GetDocumentTextAsync(solution, "Source0.cs");

        Assert.Contains("public static bool ShouldMapName(global::TestNamespace.Person source)", text);
        Assert.Contains("throw new global::System.NotImplementedException();", text);
    }

    private const string ConditionSourcePersonFile = @"
using AnvilMap;

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
    public async Task AM004_InsertsStubIntoTheSourceTypesOwnDocument_WhenDeclaredInADifferentFile()
    {
        var solution = await CodeFixTestHelper.ApplyFixAsync(
            new GenerateStubMethodCodeFixProvider(), "AM004", ConditionSourcePersonFile, ConditionSourceDtoFile);

        var personText = await CodeFixTestHelper.GetDocumentTextAsync(solution, "Source0.cs");
        var dtoText = await CodeFixTestHelper.GetDocumentTextAsync(solution, "Source1.cs");

        Assert.Contains("public static bool ShouldMapName(global::TestNamespace.Person source)", personText);
        Assert.DoesNotContain("ShouldMapName", dtoText);
    }

    private const string ConverterSourceSingleFile = @"
using AnvilMap;

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
    public async Task AM009_GeneratesStubReturningTheDestinationPropertysType()
    {
        var solution = await CodeFixTestHelper.ApplyFixAsync(
            new GenerateStubMethodCodeFixProvider(), "AM009", ConverterSourceSingleFile);
        var text = await CodeFixTestHelper.GetDocumentTextAsync(solution, "Source0.cs");

        Assert.Contains("public static string ComputeName(global::TestNamespace.Person source)", text);
        Assert.Contains("throw new global::System.NotImplementedException();", text);
    }

    private const string ConverterSourcePersonFile = @"
using AnvilMap;

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
    public async Task AM009_InsertsStubIntoTheSourceTypesOwnDocument_WhenDeclaredInADifferentFile()
    {
        var solution = await CodeFixTestHelper.ApplyFixAsync(
            new GenerateStubMethodCodeFixProvider(), "AM009", ConverterSourcePersonFile, ConverterSourceDtoFile);

        var personText = await CodeFixTestHelper.GetDocumentTextAsync(solution, "Source0.cs");
        var dtoText = await CodeFixTestHelper.GetDocumentTextAsync(solution, "Source1.cs");

        Assert.Contains("public static string ComputeName(global::TestNamespace.Person source)", personText);
        Assert.DoesNotContain("ComputeName", dtoText);
    }

    // [MapFrom] flips which type physically carries the attributes - Person (the source) has
    // no idea PersonDto exists, and no static method referencing it. The stub belongs on
    // PersonDto (the method host), not Person, even though its parameter type is still Person.
    private const string MapFromConditionSourceFile = @"
namespace TestNamespace;

public class Person
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}
";

    private const string MapFromConditionDtoFile = @"
using AnvilMap;

namespace TestNamespace;

[MapFrom(typeof(Person))]
[MapCondition(typeof(Person), nameof(Name), nameof(ShouldMapName))]
public class PersonDto
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}
";

    [Fact]
    public async Task AM004_MapFrom_InsertsStubOnTheDestinationType_WithSourceTypeAsParameter()
    {
        var solution = await CodeFixTestHelper.ApplyFixAsync(
            new GenerateStubMethodCodeFixProvider(), "AM004", MapFromConditionSourceFile, MapFromConditionDtoFile);

        var personText = await CodeFixTestHelper.GetDocumentTextAsync(solution, "Source0.cs");
        var dtoText = await CodeFixTestHelper.GetDocumentTextAsync(solution, "Source1.cs");

        Assert.DoesNotContain("ShouldMapName", personText);
        Assert.Contains("public static bool ShouldMapName(global::TestNamespace.Person source)", dtoText);
        Assert.Contains("throw new global::System.NotImplementedException();", dtoText);
    }
}

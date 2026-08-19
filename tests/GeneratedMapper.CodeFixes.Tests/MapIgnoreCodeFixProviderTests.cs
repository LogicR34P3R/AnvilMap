using System.Threading.Tasks;

namespace GeneratedMapper.CodeFixes.Tests;

public class MapIgnoreCodeFixProviderTests
{
    private const string Source = @"
using GeneratedMapper;

namespace TestNamespace;

[MapTo(typeof(PersonDto))]
public class Person
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}

public class PersonDto
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
    public string Extra { get; set; } = """";
}
";

    [Fact]
    public async Task AddsMapIgnoreAboveTheUnmappedProperty()
    {
        var solution = await CodeFixTestHelper.ApplyFixAsync(new MapIgnoreCodeFixProvider(), "GM001", Source);
        var text = await CodeFixTestHelper.GetDocumentTextAsync(solution, "Source0.cs");

        Assert.Contains("[global::GeneratedMapper.MapIgnoreAttribute]", text);

        var attributeIndex = text.IndexOf("[global::GeneratedMapper.MapIgnoreAttribute]");
        var propertyIndex = text.IndexOf("public string Extra");
        Assert.True(attributeIndex >= 0 && attributeIndex < propertyIndex);
    }

    [Fact]
    public async Task LeavesOtherPropertiesUntouched()
    {
        var solution = await CodeFixTestHelper.ApplyFixAsync(new MapIgnoreCodeFixProvider(), "GM001", Source);
        var text = await CodeFixTestHelper.GetDocumentTextAsync(solution, "Source0.cs");

        Assert.Contains("public int Id { get; set; }", text);
        Assert.Contains("public string Name { get; set; }", text);
        Assert.DoesNotContain("[global::GeneratedMapper.MapIgnoreAttribute]\n    public int Id", text);
    }
}

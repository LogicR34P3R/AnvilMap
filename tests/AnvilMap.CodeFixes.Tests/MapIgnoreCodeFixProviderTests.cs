namespace AnvilMap.CodeFixes.Tests;

public class MapIgnoreCodeFixProviderTests
{
    private const string Source = @"
using AnvilMap;

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
        var solution = await CodeFixTestHelper.ApplyFixAsync(new MapIgnoreCodeFixProvider(), "AM001", Source);
        var text = await CodeFixTestHelper.GetDocumentTextAsync(solution, "Source0.cs");

        Assert.Contains("[global::AnvilMap.MapIgnoreAttribute]", text);

        var attributeIndex = text.IndexOf("[global::AnvilMap.MapIgnoreAttribute]");
        var propertyIndex = text.IndexOf("public string Extra");
        Assert.True(attributeIndex >= 0 && attributeIndex < propertyIndex);
    }

    [Fact]
    public async Task LeavesOtherPropertiesUntouched()
    {
        var solution = await CodeFixTestHelper.ApplyFixAsync(new MapIgnoreCodeFixProvider(), "AM001", Source);
        var text = await CodeFixTestHelper.GetDocumentTextAsync(solution, "Source0.cs");

        Assert.Contains("public int Id { get; set; }", text);
        Assert.Contains("public string Name { get; set; }", text);
        Assert.DoesNotContain("[global::AnvilMap.MapIgnoreAttribute]\n    public int Id", text);
    }
}

namespace AnvilMap.Sample.Aot.ViewModels;

public sealed class CategoryDto
{
    public string Name { get; set; } = "";
    public List<CategoryDto> Children { get; set; } = new();
}

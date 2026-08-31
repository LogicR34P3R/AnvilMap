using AnvilMap;

[MapTo(typeof(CategoryDto), MaxDepth = 2)]
public sealed class Category
{
    public string Name { get; set; } = "";
    public List<Category> Children { get; set; } = new();
}

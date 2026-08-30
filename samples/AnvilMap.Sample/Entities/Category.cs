using AnvilMap.Sample.ViewModels;

namespace AnvilMap.Sample.Entities;

// MaxDepth = 2: Children is followed two levels deep, then left empty rather than recursing further.
[MapTo(typeof(CategoryDto), MaxDepth = 2)]
public sealed class Category
{
    public string Name { get; set; } = "";
    public List<Category> Children { get; set; } = new();
}

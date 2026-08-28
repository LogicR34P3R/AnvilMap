namespace AnvilMap.Benchmarks.Models;

public sealed partial class GraphBlog
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public List<GraphPost> Posts { get; set; } = new();
}

namespace GeneratedMapper.Benchmarks.Models;

public sealed class GraphBlogDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public List<GraphPostDto> Posts { get; set; } = new();
}

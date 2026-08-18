namespace GeneratedMapper.Benchmarks.Models;

public sealed partial class GraphPost
{
    public int Id { get; set; }
    public int BlogId { get; set; }
    public string Headline { get; set; } = "";
    public List<GraphComment> Comments { get; set; } = new();
}

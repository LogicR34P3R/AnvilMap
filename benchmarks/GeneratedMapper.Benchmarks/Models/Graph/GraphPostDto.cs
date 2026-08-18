namespace GeneratedMapper.Benchmarks.Models;

public sealed class GraphPostDto
{
    public int Id { get; set; }
    public string Headline { get; set; } = "";
    public List<GraphCommentDto> Comments { get; set; } = new();
}

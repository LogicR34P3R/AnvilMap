namespace GeneratedMapper.Benchmarks.Models;

// Realistic 3-level graph: Blog -> List<Post> -> List<Comment>, deliberately similar in
// shape to samples/GeneratedMapper.Sample's Blog/Post entities.
public sealed partial class GraphComment
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public string Author { get; set; } = "";
    public string Text { get; set; } = "";
}

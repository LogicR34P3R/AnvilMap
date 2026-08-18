using System.Collections.Generic;
using GeneratedMapper;

namespace GeneratedMapper.Sample.ViewModels;

public sealed class BlogDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public List<PostDto> Posts { get; set; } = new();

    [MapIgnore]
    public int PostCount { get; set; }
}

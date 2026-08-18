using GeneratedMapper;
using GeneratedMapper.Sample.ViewModels;

namespace GeneratedMapper.Sample.Entities;

[MapTo(typeof(PostDto), GenerateReverse = true)]
[MapCondition(typeof(PostDto), nameof(PostDto.Body), nameof(ShouldMapBody))]
[MapTo(typeof(PostSummaryDto))]
public sealed class Post
{
    public int Id { get; set; }
    public int BlogId { get; set; }
    public string Headline { get; set; } = "";
    public string Body { get; set; } = "";
    public bool IsDraft { get; set; }

    public static bool ShouldMapBody(Post source) => !source.IsDraft;
}

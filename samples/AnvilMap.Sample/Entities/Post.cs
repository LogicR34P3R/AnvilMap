using AnvilMap.Sample.ViewModels;

namespace AnvilMap.Sample.Entities;

[MapTo(typeof(PostDto), GenerateReverse = true)]
[MapCondition(typeof(PostDto), nameof(PostDto.Body), nameof(ShouldMapBody))]
[MapDefault(typeof(PostDto), nameof(PostDto.Subtitle), "Untitled")]
[MapTo(typeof(PostSummaryDto))]
[MapProperty(typeof(PostSummaryDto), nameof(Status), nameof(PostSummaryDto.StatusCode))]
public sealed class Post
{
    public int Id { get; set; }
    public int BlogId { get; set; }
    public string Headline { get; set; } = "";
    public string? Subtitle { get; set; }
    public string Body { get; set; } = "";

    // Scoped to the PostDto -> Post reverse direction: string -> enum isn't a built-in
    // conversion, so this side is excluded rather than reporting AM003.
    [MapIgnore(typeof(PostDto))]
    public PostStatus Status { get; set; }

    public PostAuthor Author { get; set; } = new();

    public static bool ShouldMapBody(Post source) => source.Status != PostStatus.Draft;
}

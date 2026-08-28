namespace AnvilMap.Sample.ViewModels;

public sealed class PostDto
{
    public int Id { get; set; }
    public string Headline { get; set; } = "";
    public string Body { get; set; } = "";

    // Not an explicit [MapProperty] rename - resolved by naming-convention flattening against
    // Post.Author.DisplayName, since no top-level "AuthorDisplayName" property exists on Post.
    public string AuthorDisplayName { get; set; } = "";
}

namespace GeneratedMapper.Sample.Entities;

// EF Core owned type - configured via OwnsOne in SampleDbContext, stored inline on the Posts
// table. Exists to demonstrate naming-convention flattening (PostDto.AuthorDisplayName <-
// Post.Author.DisplayName), which needs a real nested type to flatten through, not just a
// same-table scalar column.
public sealed class PostAuthor
{
    public string DisplayName { get; set; } = "";
}

using AnvilMap;
using AnvilMap.Sample;
using AnvilMap.Sample.Entities;
using AnvilMap.Sample.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using var connection = new SqliteConnection("DataSource=:memory:");
connection.Open();

var options = new DbContextOptionsBuilder<SampleDbContext>()
    .UseSqlite(connection)
    .Options;

using (var setup = new SampleDbContext(options))
{
    setup.Database.EnsureCreated();

    setup.Blogs.Add(new Blog
    {
        Title = "AnvilMap",
        OwnerEmail = "owner@example.com",
        Posts =
        {
            new Post { Headline = "Compile-time mapping", Subtitle = "Why no reflection at runtime", Body = "No reflection at runtime.", Status = PostStatus.Published, Author = new() { DisplayName = "Ada" } },
            new Post { Headline = "Upcoming projection work", Body = "Draft notes.", Status = PostStatus.Draft, Author = new() { DisplayName = "Grace" } },
        },
    });

    setup.SaveChanges();
}

using var db = new SampleDbContext(options);

var projection = db.Blogs.ProjectToBlogDto();

Console.WriteLine("Generated SQL for ProjectToBlogDto():");
Console.WriteLine(projection.ToQueryString());
Console.WriteLine();

var projectedBlogs = projection.ToList();

foreach (var blogDto in projectedBlogs)
{
    blogDto.PostCount = blogDto.Posts.Count;
    Console.WriteLine($"[projection] {blogDto.Title} by {blogDto.Author} ({blogDto.PostCount} posts)");
    foreach (var post in blogDto.Posts)
    {
        Console.WriteLine($"    - {post.Headline}");
    }
}

Console.WriteLine();

var entity = db.Blogs.Include(b => b.Posts).First();
var imperativeDto = entity.ToBlogDto();
imperativeDto.PostCount = imperativeDto.Posts.Count;
Console.WriteLine($"[imperative] {imperativeDto.Title} by {imperativeDto.Author} ({imperativeDto.PostCount} posts)");

var roundTripped = imperativeDto.ToBlog();
Console.WriteLine($"[reverse]    OwnerEmail round-tripped back to '{roundTripped.OwnerEmail}'");

IMapper mapper = new AnvilMapService();
var viaMapper = mapper.Map<Blog, AnvilMap.Sample.ViewModels.BlogDto>(entity);
Console.WriteLine($"[IMapper]    {viaMapper.Title} by {viaMapper.Author}");

Console.WriteLine();

foreach (var post in entity.Posts)
{
    var imperativePostDto = post.ToPostDto();
    var viaMapperPostDto = mapper.Map<Post, AnvilMap.Sample.ViewModels.PostDto>(post);
    Console.WriteLine($"[imperative] '{post.Headline}' (status={post.Status}) -> Body='{imperativePostDto.Body}'");
    Console.WriteLine($"[IMapper]    '{post.Headline}' (status={post.Status}) -> Body='{viaMapperPostDto.Body}'");
    Console.WriteLine($"[imperative] '{post.Headline}' Subtitle='{imperativePostDto.Subtitle}'");
    Console.WriteLine($"[imperative] '{post.Headline}' Status='{imperativePostDto.Status}'");
}

foreach (var post in projectedBlogs.SelectMany(b => b.Posts))
{
    Console.WriteLine($"[projection] '{post.Headline}' -> Body='{post.Body}' (excluded from the SQL projection)");
}

Console.WriteLine();

foreach (var post in projectedBlogs.SelectMany(b => b.Posts))
{
    Console.WriteLine($"[projection] '{post.Headline}' -> Status='{post.Status}' (excluded from the SQL projection)");
}

Console.WriteLine();

foreach (var post in projectedBlogs.SelectMany(b => b.Posts))
{
    Console.WriteLine($"[projection] '{post.Headline}' -> AuthorDisplayName='{post.AuthorDisplayName}'");
}

Console.WriteLine();

var summary = entity.Posts.First().ToPostSummaryDto();
Console.WriteLine($"[imperative] positional record: PostSummaryDto({summary.Id}, '{summary.Headline}', {summary.StatusCode})");

var projectedSummaries = db.Posts.ProjectToPostSummaryDto().ToList();
Console.WriteLine("Generated SQL for ProjectToPostSummaryDto():");
Console.WriteLine(db.Posts.ProjectToPostSummaryDto().ToQueryString());
foreach (var s in projectedSummaries)
{
    Console.WriteLine($"[projection] PostSummaryDto({s.Id}, '{s.Headline}', {s.StatusCode})");
}

Console.WriteLine();

var postRoundTrip = entity.Posts.First().ToPostDto().ToPost();
Console.WriteLine($"[reverse]    Headline round-tripped back to '{postRoundTrip.Headline}' (Status left at default: {postRoundTrip.Status})");

Console.WriteLine();

var gallery = new Gallery
{
    Name = "Launch assets",
    Tags = { "release", "launch", "release" },
    RecentViewCounts = { 120, 340, 512 },
    Photos = { new Photo { Url = "https://example.com/1.png" }, new Photo { Url = "https://example.com/2.png" } },
};

var galleryDto = gallery.ToGalleryDto();
Console.WriteLine($"[imperative] Gallery '{galleryDto.Name}': {galleryDto.Tags.Count} unique tag(s), " +
    $"{galleryDto.RecentViewCounts.Length} view-count(s) (ImmutableArray<int>), " +
    $"{galleryDto.Photos.Count} photo(s) (ObservableCollection<PhotoDto>), " +
    $"PhotoCount={galleryDto.PhotoCount} (via [MapUsing])");

Console.WriteLine();

var root = new Category
{
    Name = "Root",
    Children =
    {
        new Category
        {
            Name = "Level 1",
            Children = { new Category { Name = "Level 2", Children = { new Category { Name = "Level 3 (cut off)" } } } },
        },
    },
};

var rootDto = root.ToCategoryDto();
Console.WriteLine($"[imperative] {rootDto.Name} -> {rootDto.Children[0].Name} -> {rootDto.Children[0].Children[0].Name} -> " +
    $"(Children.Count={rootDto.Children[0].Children[0].Children.Count}, cut off by MaxDepth)");

Console.WriteLine();

var attachments = new List<Attachment>
{
    new ImageAttachment { FileName = "cover.png", Width = 1920, Height = 1080 },
    new VideoAttachment { FileName = "demo.mp4", DurationSeconds = 42 },
    new Attachment { FileName = "notes.txt" },
};

foreach (var attachment in attachments)
{
    var attachmentDto = attachment.ToAttachmentDto();
    Console.WriteLine(attachmentDto switch
    {
        ImageAttachmentDto image => $"[imperative] '{image.FileName}' -> ImageAttachmentDto {image.Width}x{image.Height}",
        VideoAttachmentDto video => $"[imperative] '{video.FileName}' -> VideoAttachmentDto {video.DurationSeconds}s",
        _ => $"[imperative] '{attachmentDto.FileName}' -> AttachmentDto (base mapping, no derived match)",
    });
}

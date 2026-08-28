using AnvilMap;
using AnvilMap.Sample;
using AnvilMap.Sample.Entities;
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
            new Post { Headline = "Compile-time mapping", Body = "No reflection at runtime.", IsDraft = false, Author = new() { DisplayName = "Ada" } },
            new Post { Headline = "Upcoming projection work", Body = "Draft notes.", IsDraft = true, Author = new() { DisplayName = "Grace" } },
        },
    });

    setup.SaveChanges();
}

using var db = new SampleDbContext(options);

// SQL-side projection: only the mapped columns are selected, no client-side evaluation.
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

// Imperative in-memory mapping, e.g. after loading an already-tracked entity.
var entity = db.Blogs.Include(b => b.Posts).First();
var imperativeDto = entity.ToBlogDto();
imperativeDto.PostCount = imperativeDto.Posts.Count;
Console.WriteLine($"[imperative] {imperativeDto.Title} by {imperativeDto.Author} ({imperativeDto.PostCount} posts)");

// Reverse mapping, generated because [MapTo(..., GenerateReverse = true)] was set on Blog.
var roundTripped = imperativeDto.ToBlog();
Console.WriteLine($"[reverse]    OwnerEmail round-tripped back to '{roundTripped.OwnerEmail}'");

// IMapper-based usage, for DI scenarios (services.AddSingleton<IMapper, AnvilMapService>()).
IMapper mapper = new AnvilMapService();
var viaMapper = mapper.Map<Blog, AnvilMap.Sample.ViewModels.BlogDto>(entity);
Console.WriteLine($"[IMapper]    {viaMapper.Title} by {viaMapper.Author}");

Console.WriteLine();

// [MapCondition] on Post.Body: the imperative mapper and IMapper both skip Body for drafts...
foreach (var post in entity.Posts)
{
    var imperativePostDto = post.ToPostDto();
    var viaMapperPostDto = mapper.Map<Post, AnvilMap.Sample.ViewModels.PostDto>(post);
    Console.WriteLine($"[imperative] '{post.Headline}' (draft={post.IsDraft}) -> Body='{imperativePostDto.Body}'");
    Console.WriteLine($"[IMapper]    '{post.Headline}' (draft={post.IsDraft}) -> Body='{viaMapperPostDto.Body}'");
}

// ...but the SQL projection can't translate an arbitrary condition method, so it's excluded
// there (AM005 at build time) and Body comes through for every row, draft or not.
foreach (var post in projectedBlogs.SelectMany(b => b.Posts))
{
    Console.WriteLine($"[projection] '{post.Headline}' -> Body='{post.Body}'");
}

Console.WriteLine();

// PostDto.AuthorDisplayName has no matching top-level source property on Post - resolved by
// naming-convention flattening against Post.Author.DisplayName (an EF Core owned type).
foreach (var post in projectedBlogs.SelectMany(b => b.Posts))
{
    Console.WriteLine($"[projection] '{post.Headline}' -> AuthorDisplayName='{post.AuthorDisplayName}'");
}

Console.WriteLine();

// PostSummaryDto is a positional record with no parameterless constructor - the generator
// builds it via constructor arguments instead of object-initializer syntax, both imperatively
// and in the SQL projection.
var summary = entity.Posts.First().ToPostSummaryDto();
Console.WriteLine($"[imperative] positional record: PostSummaryDto({summary.Id}, '{summary.Headline}')");

var projectedSummaries = db.Posts.ProjectToPostSummaryDto().ToList();
Console.WriteLine("Generated SQL for ProjectToPostSummaryDto():");
Console.WriteLine(db.Posts.ProjectToPostSummaryDto().ToQueryString());
foreach (var s in projectedSummaries)
{
    Console.WriteLine($"[projection] PostSummaryDto({s.Id}, '{s.Headline}')");
}
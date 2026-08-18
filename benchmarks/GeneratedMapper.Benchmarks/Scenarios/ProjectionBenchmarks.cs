using AutoMapper;
using AutoMapper.QueryableExtensions;
using BenchmarkDotNet.Attributes;
using GeneratedMapper.Benchmarks.AutoMapperConfig;
using GeneratedMapper.Benchmarks.Db;
using GeneratedMapper.Benchmarks.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GeneratedMapper.Benchmarks.Scenarios;

// EF Core against a real SQLite provider (in-memory), mirroring
// samples/GeneratedMapper.Sample - only the timing question lives here.
// Translation-correctness (generated SQL shape, client-eval fallback) is a regular test
// in GeneratedMapper.Benchmarks.ParityTests/ProjectionTranslationTests.cs, not a
// BenchmarkDotNet benchmark.
[MemoryDiagnoser]
public class ProjectionBenchmarks
{
    private const int PostsPerBlog = 2;
    private const int CommentsPerPost = 2;

    [Params(10, 1_000, 100_000)]
    public int BlogCount { get; set; }

    private SqliteConnection _connection = null!;
    private DbContextOptions<BenchmarkDbContext> _options = null!;
    private IConfigurationProvider _autoMapperConfig = null!;

    [GlobalSetup]
    public void Setup()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<BenchmarkDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var setup = new BenchmarkDbContext(_options);
        setup.Database.EnsureCreated();
        setup.ChangeTracker.AutoDetectChangesEnabled = false;

        for (var b = 0; b < BlogCount; b++)
        {
            var posts = new List<GraphPost>(PostsPerBlog);
            for (var p = 0; p < PostsPerBlog; p++)
            {
                var comments = new List<GraphComment>(CommentsPerPost);
                for (var c = 0; c < CommentsPerPost; c++)
                    comments.Add(new GraphComment { Author = "Reader", Text = "Nice post!" });

                posts.Add(new GraphPost { Headline = $"Post {p}", Comments = comments });
            }

            setup.Blogs.Add(new GraphBlog { Title = $"Blog {b}", Posts = posts });
        }

        setup.SaveChanges();

        _autoMapperConfig = BenchmarkMapperFactory.CreateConfiguration();
    }

    [GlobalCleanup]
    public void Cleanup() => _connection.Dispose();

    [Benchmark(Baseline = true, Description = "GeneratedMapper (ProjectToGraphBlogDto)")]
    public List<GraphBlogDto> GeneratedMapper_ProjectTo()
    {
        using var db = new BenchmarkDbContext(_options);
        return db.Blogs.ProjectToGraphBlogDto().ToList();
    }

    [Benchmark(Description = "AutoMapper (ProjectTo<GraphBlogDto>)")]
    public List<GraphBlogDto> AutoMapper_ProjectTo()
    {
        using var db = new BenchmarkDbContext(_options);
        return db.Blogs.ProjectTo<GraphBlogDto>(_autoMapperConfig).ToList();
    }
}

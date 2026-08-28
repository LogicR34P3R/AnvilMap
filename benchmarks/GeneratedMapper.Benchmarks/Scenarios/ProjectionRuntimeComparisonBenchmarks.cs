using AutoMapper;
using AutoMapper.QueryableExtensions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using GeneratedMapper.Benchmarks.AutoMapperConfig;
using GeneratedMapper.Benchmarks.Db;
using GeneratedMapper.Benchmarks.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GeneratedMapper.Benchmarks.Scenarios;

// See FlatRuntimeComparisonBenchmarks for why this class (and its siblings) exist. Projection
// shape: EF Core against SQLite (in-memory), mirroring ProjectionBenchmarks (both mappers'
// generated SQL is a statistical tie on net8.0 - this checks whether that holds across runtimes
// too). Two differences from every other *RuntimeComparisonBenchmarks sibling:
//   - Only net8.0/net10.0, not net6.0 - Microsoft.EntityFrameworkCore.Sqlite has no
//     net6.0-compatible asset (see GeneratedMapper.Benchmarks.csproj's own comment on that
//     PackageReference), so this file is excluded from the net6.0 build entirely, the same way
//     ProjectionBenchmarks.cs/BenchmarkDbContext.cs already are.
//   - Capped at 1,000 blogs, not 100,000 - see GraphRuntimeComparisonBenchmarks' comment for the
//     same reasoning (the 100,000-row case already takes over a second per call on one runtime;
//     three runtimes' worth of iterations of that isn't worth this comparison's own run time).
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
public class ProjectionRuntimeComparisonBenchmarks
{
    private const int PostsPerBlog = 2;
    private const int CommentsPerPost = 2;

    [Params(10, 1_000)]
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
                {
                    comments.Add(new GraphComment { Author = "Reader", Text = "Nice post!" });
                }

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
    public List<GraphBlogDto> ProjectTo()
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

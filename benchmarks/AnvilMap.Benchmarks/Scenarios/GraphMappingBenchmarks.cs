using BenchmarkDotNet.Attributes;
using AnvilMap.Benchmarks.AutoMapperConfig;
using AnvilMap.Benchmarks.Models;

namespace AnvilMap.Benchmarks.Scenarios;

[MemoryDiagnoser]
public class GraphMappingBenchmarks
{
    private const int CommentsPerPost = 3;

    // Realistic-small, realistic-large, and stress-test post counts per blog; each post
    // carries a fixed small number of comments so the graph stays 3 levels deep.
    [Params(10, 1_000, 100_000)]
    public int PostCount { get; set; }

    private GraphBlog _source = null!;
    private AutoMapper.IMapper _autoMapper = null!;

    [GlobalSetup]
    public void Setup()
    {
        var posts = new List<GraphPost>(PostCount);
        for (var i = 0; i < PostCount; i++)
        {
            var comments = new List<GraphComment>(CommentsPerPost);
            for (var c = 0; c < CommentsPerPost; c++)
            {
                comments.Add(new GraphComment { Id = c, PostId = i, Author = "Reader", Text = "Nice post!" });
            }

            posts.Add(new GraphPost { Id = i, BlogId = 1, Headline = $"Post {i}", Comments = comments });
        }

        _source = new GraphBlog { Id = 1, Title = "Benchmarks", Posts = posts };

        _autoMapper = BenchmarkMapperFactory.CreateMapper();
    }

    [Benchmark(Baseline = true, Description = "AnvilMap (extension method)")]
    public GraphBlogDto AnvilMap_Extension() => _source.ToGraphBlogDto();

    [Benchmark(Description = "AnvilMap (dispatcher)")]
    public GraphBlogDto AnvilMap_Dispatcher() => GeneratedMappings.Map<GraphBlogDto>(_source);

    [Benchmark(Description = "AutoMapper")]
    public GraphBlogDto AutoMapper() => _autoMapper.Map<GraphBlogDto>(_source);
}

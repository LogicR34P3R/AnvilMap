using AutoMapper;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using GeneratedMapper.Benchmarks.AutoMapperConfig;
using GeneratedMapper.Benchmarks.Models;

namespace GeneratedMapper.Benchmarks.Scenarios;

// See FlatRuntimeComparisonBenchmarks for why this class (and its siblings) exist. Graph shape:
// 3-level graph (Blog -> List<Post> -> List<Comment>). Capped at 1,000 posts, deliberately -
// GraphMappingBenchmarks also runs a 100,000-post stress case, but that one already takes tens
// of milliseconds per call on a single runtime; multiplying it by three runtimes' worth of
// [SimpleJob] iterations would balloon this class's own run time for a data point this
// comparison doesn't need (the runtime-vs-runtime gap is already visible at 1,000). Also worth
// noting: GraphMappingBenchmarks (net8.0 only) is the one scenario where AutoMapper wins outright -
// this class is where that finding gets checked across runtimes too.
[SimpleJob(RuntimeMoniker.Net60)]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
public class GraphRuntimeComparisonBenchmarks
{
    private const int CommentsPerPost = 3;

    [Params(10, 1_000)]
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
                comments.Add(new GraphComment { Id = c, PostId = i, Author = "Reader", Text = "Nice post!" });

            posts.Add(new GraphPost { Id = i, BlogId = 1, Headline = $"Post {i}", Comments = comments });
        }

        _source = new GraphBlog { Id = 1, Title = "Benchmarks", Posts = posts };

        _autoMapper = BenchmarkMapperFactory.CreateMapper();
    }

    [Benchmark(Baseline = true, Description = "GeneratedMapper (extension method)")]
    public GraphBlogDto Extension() => _source.ToGraphBlogDto();

    [Benchmark(Description = "GeneratedMapper (dispatcher)")]
    public GraphBlogDto Dispatcher() => GeneratedMappings.Map<GraphBlogDto>(_source);

    [Benchmark(Description = "AutoMapper")]
    public GraphBlogDto AutoMapper() => _autoMapper.Map<GraphBlogDto>(_source);
}

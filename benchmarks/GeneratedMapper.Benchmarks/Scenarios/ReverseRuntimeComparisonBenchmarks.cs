using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using GeneratedMapper.Benchmarks.Models;

namespace GeneratedMapper.Benchmarks.Scenarios;

// See FlatRuntimeComparisonBenchmarks for why this class (and its siblings) exist. Reverse
// shape: [MapTo(GenerateReverse = true)]'s generated FlatDto -> FlatSource direction.
[SimpleJob(RuntimeMoniker.Net60)]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
public class ReverseRuntimeComparisonBenchmarks
{
    private FlatDto _dto = null!;

    [GlobalSetup]
    public void Setup()
    {
        _dto = new FlatDto
        {
            Id = 1,
            Name = "Widget",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            Amount = 19.99m,
        };
    }

    [Benchmark(Baseline = true, Description = "GeneratedMapper (GenerateReverse extension method)")]
    public FlatSource Extension() => _dto.ToFlatSource();

    [Benchmark(Description = "GeneratedMapper (dispatcher)")]
    public FlatSource Dispatcher() => GeneratedMappings.Map<FlatSource>(_dto);
}

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using GeneratedMapper.Benchmarks.Models;

namespace GeneratedMapper.Benchmarks.Scenarios;

// GeneratedMapper's own throughput across the TFMs its generated code actually branches on -
// not a mapper-vs-mapper comparison (that's every other scenario in this folder), a
// runtime-vs-runtime one. net6.0 exercises the Dictionary-dispatch fallback (no
// FrozenDictionary below .NET 8; net6.0 stands in for netstandard2.0 since that TFM can't run
// as an executable), net8.0 is today's baseline, net10.0 is the same generated code running on
// the newer runtime. One BenchmarkDotNet report, one column per runtime, via [SimpleJob]
// stacking - the project multi-targets net6.0;net8.0;net10.0 specifically so this class (and its
// *RuntimeComparisonBenchmarks siblings, one per existing scenario shape) only needs these three
// attributes, no other project plumbing. Flat is the shape this pattern was first verified
// against (a real run, not just a build check) before being rolled out to the rest.
[SimpleJob(RuntimeMoniker.Net60)]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
public class FlatRuntimeComparisonBenchmarks
{
    private FlatSource _source = null!;

    [GlobalSetup]
    public void Setup()
    {
        _source = new FlatSource
        {
            Id = 1,
            Name = "Widget",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            Amount = 19.99m,
        };
    }

    [Benchmark(Baseline = true, Description = "GeneratedMapper (extension method)")]
    public FlatDto Extension() => _source.ToFlatDto();

    [Benchmark(Description = "GeneratedMapper (dispatcher)")]
    public FlatDto Dispatcher() => GeneratedMappings.Map<FlatDto>(_source);
}

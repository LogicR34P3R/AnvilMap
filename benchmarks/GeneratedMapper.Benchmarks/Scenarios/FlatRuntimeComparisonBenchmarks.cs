using AutoMapper;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using GeneratedMapper.Benchmarks.AutoMapperConfig;
using GeneratedMapper.Benchmarks.Models;

namespace GeneratedMapper.Benchmarks.Scenarios;

// Both mappers' throughput across the TFMs GeneratedMapper's own capability detection branches
// on - the same shapes docs/benchmarks.md already compares on net8.0 alone (see
// FlatMappingBenchmarks), extended to net6.0/net10.0 too so AutoMapper's own relative standing
// can be checked across runtimes as well, not just GeneratedMapper's dispatcher path. net6.0
// exercises the Dictionary-dispatch fallback (no FrozenDictionary below .NET 8; net6.0 stands in
// for netstandard2.0 since that TFM can't run as an executable), net8.0 is today's baseline,
// net10.0 is the same generated code running on the newer runtime. One BenchmarkDotNet report,
// one column per runtime, via [SimpleJob] stacking - the project multi-targets
// net6.0;net8.0;net10.0 specifically so this class (and its *RuntimeComparisonBenchmarks
// siblings, one per existing scenario shape) only needs these three attributes, no other project
// plumbing. Flat is the shape this pattern was first verified against (a real run, not just a
// build check) before being rolled out to the rest.
[SimpleJob(RuntimeMoniker.Net60)]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
public class FlatRuntimeComparisonBenchmarks
{
    private FlatSource _source = null!;
    private AutoMapper.IMapper _autoMapper = null!;

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

        _autoMapper = BenchmarkMapperFactory.CreateMapper();
    }

    [Benchmark(Baseline = true, Description = "GeneratedMapper (extension method)")]
    public FlatDto Extension() => _source.ToFlatDto();

    [Benchmark(Description = "GeneratedMapper (dispatcher)")]
    public FlatDto Dispatcher() => GeneratedMappings.Map<FlatDto>(_source);

    [Benchmark(Description = "AutoMapper")]
    public FlatDto AutoMapper() => _autoMapper.Map<FlatDto>(_source);
}

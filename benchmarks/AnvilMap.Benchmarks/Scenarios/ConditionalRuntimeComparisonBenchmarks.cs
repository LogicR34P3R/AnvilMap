using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using AnvilMap.Benchmarks.AutoMapperConfig;
using AnvilMap.Benchmarks.Models;

namespace AnvilMap.Benchmarks.Scenarios;

// See FlatRuntimeComparisonBenchmarks for why this class (and its siblings) exist. Conditional
// shape: [MapCondition] guarding a property assignment.
[SimpleJob(RuntimeMoniker.Net60)]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
public class ConditionalRuntimeComparisonBenchmarks
{
    private ConditionalSource _source = null!;
    private AutoMapper.IMapper _autoMapper = null!;

    [GlobalSetup]
    public void Setup()
    {
        _source = new ConditionalSource { Id = 1, Name = "Record", Secret = "classified", IsRestricted = false };
        _autoMapper = BenchmarkMapperFactory.CreateMapper();
    }

    [Benchmark(Baseline = true, Description = "AnvilMap (extension method, [MapCondition])")]
    public ConditionalDto Extension() => _source.ToConditionalDto();

    [Benchmark(Description = "AnvilMap (dispatcher, [MapCondition])")]
    public ConditionalDto Dispatcher() => GeneratedMappings.Map<ConditionalDto>(_source);

    [Benchmark(Description = "AutoMapper (.Condition(...))")]
    public ConditionalDto AutoMapper() => _autoMapper.Map<ConditionalDto>(_source);
}

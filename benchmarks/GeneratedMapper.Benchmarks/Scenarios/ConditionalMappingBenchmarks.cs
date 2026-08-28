using BenchmarkDotNet.Attributes;
using GeneratedMapper.Benchmarks.AutoMapperConfig;
using GeneratedMapper.Benchmarks.Models;

namespace GeneratedMapper.Benchmarks.Scenarios;

[MemoryDiagnoser]
public class ConditionalMappingBenchmarks
{
    private ConditionalSource _source = null!;
    private AutoMapper.IMapper _autoMapper = null!;

    [GlobalSetup]
    public void Setup()
    {
        _source = new ConditionalSource { Id = 1, Name = "Record", Secret = "classified", IsRestricted = false };
        _autoMapper = BenchmarkMapperFactory.CreateMapper();
    }

    [Benchmark(Baseline = true, Description = "GeneratedMapper (extension method, [MapCondition])")]
    public ConditionalDto GeneratedMapper_Extension() => _source.ToConditionalDto();

    [Benchmark(Description = "GeneratedMapper (dispatcher, [MapCondition])")]
    public ConditionalDto GeneratedMapper_Dispatcher() => GeneratedMappings.Map<ConditionalDto>(_source);

    [Benchmark(Description = "AutoMapper (.Condition(...))")]
    public ConditionalDto AutoMapper() => _autoMapper.Map<ConditionalDto>(_source);
}

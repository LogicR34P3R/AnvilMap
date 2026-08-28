using BenchmarkDotNet.Attributes;
using AnvilMap.Benchmarks.AutoMapperConfig;
using AnvilMap.Benchmarks.Models;

namespace AnvilMap.Benchmarks.Scenarios;

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

    [Benchmark(Baseline = true, Description = "AnvilMap (extension method, [MapCondition])")]
    public ConditionalDto AnvilMap_Extension() => _source.ToConditionalDto();

    [Benchmark(Description = "AnvilMap (dispatcher, [MapCondition])")]
    public ConditionalDto AnvilMap_Dispatcher() => GeneratedMappings.Map<ConditionalDto>(_source);

    [Benchmark(Description = "AutoMapper (.Condition(...))")]
    public ConditionalDto AutoMapper() => _autoMapper.Map<ConditionalDto>(_source);
}

using AutoMapper;
using BenchmarkDotNet.Attributes;
using GeneratedMapper.Benchmarks.AutoMapperConfig;
using GeneratedMapper.Benchmarks.Models;

namespace GeneratedMapper.Benchmarks.Scenarios;

[MemoryDiagnoser]
public class FlatMappingBenchmarks
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
    public FlatDto GeneratedMapper_Extension() => _source.ToFlatDto();

    [Benchmark(Description = "GeneratedMapper (dispatcher)")]
    public FlatDto GeneratedMapper_Dispatcher() => GeneratedMappings.Map<FlatDto>(_source);

    [Benchmark(Description = "AutoMapper")]
    public FlatDto AutoMapper() => _autoMapper.Map<FlatDto>(_source);
}

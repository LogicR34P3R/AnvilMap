using BenchmarkDotNet.Attributes;
using AnvilMap.Benchmarks.AutoMapperConfig;
using AnvilMap.Benchmarks.Models;

namespace AnvilMap.Benchmarks.Scenarios;

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

    [Benchmark(Baseline = true, Description = "AnvilMap (extension method)")]
    public FlatDto AnvilMap_Extension() => _source.ToFlatDto();

    [Benchmark(Description = "AnvilMap (dispatcher)")]
    public FlatDto AnvilMap_Dispatcher() => GeneratedMappings.Map<FlatDto>(_source);

    [Benchmark(Description = "AutoMapper")]
    public FlatDto AutoMapper() => _autoMapper.Map<FlatDto>(_source);
}

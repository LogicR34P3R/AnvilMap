using AutoMapper;
using BenchmarkDotNet.Attributes;
using GeneratedMapper.Benchmarks.AutoMapperConfig;
using GeneratedMapper.Benchmarks.Models;

namespace GeneratedMapper.Benchmarks.Scenarios;

// [MapTo(typeof(FlatDto), GenerateReverse = true)] on FlatSource vs. a second, explicit
// CreateMap<FlatDto, FlatSource>() in BenchmarkProfile.
[MemoryDiagnoser]
public class ReverseMappingBenchmarks
{
    private FlatDto _dto = null!;
    private AutoMapper.IMapper _autoMapper = null!;

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

        _autoMapper = BenchmarkMapperFactory.CreateMapper();
    }

    [Benchmark(Baseline = true, Description = "GeneratedMapper (GenerateReverse extension method)")]
    public FlatSource GeneratedMapper_Extension() => _dto.ToFlatSource();

    [Benchmark(Description = "GeneratedMapper (dispatcher)")]
    public FlatSource GeneratedMapper_Dispatcher() => GeneratedMappings.Map<FlatSource>(_dto);

    [Benchmark(Description = "AutoMapper (second CreateMap<TDest, TSource>())")]
    public FlatSource AutoMapper() => _autoMapper.Map<FlatSource>(_dto);
}

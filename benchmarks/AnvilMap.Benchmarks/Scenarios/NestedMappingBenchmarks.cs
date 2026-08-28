using BenchmarkDotNet.Attributes;
using AnvilMap.Benchmarks.AutoMapperConfig;
using AnvilMap.Benchmarks.Models;

namespace AnvilMap.Benchmarks.Scenarios;

[MemoryDiagnoser]
public class NestedMappingBenchmarks
{
    private OrderSource _source = null!;
    private AutoMapper.IMapper _autoMapper = null!;

    [GlobalSetup]
    public void Setup()
    {
        _source = new OrderSource
        {
            Id = 1,
            PlacedAt = DateTime.UtcNow,
            Total = 249.5m,
            Customer = new CustomerSource { Id = 1, Name = "Ada Lovelace", Email = "ada@example.com" },
        };

        _autoMapper = BenchmarkMapperFactory.CreateMapper();
    }

    [Benchmark(Baseline = true, Description = "AnvilMap (extension method)")]
    public OrderDto AnvilMap_Extension() => _source.ToOrderDto();

    [Benchmark(Description = "AnvilMap (dispatcher)")]
    public OrderDto AnvilMap_Dispatcher() => GeneratedMappings.Map<OrderDto>(_source);

    [Benchmark(Description = "AutoMapper")]
    public OrderDto AutoMapper() => _autoMapper.Map<OrderDto>(_source);
}

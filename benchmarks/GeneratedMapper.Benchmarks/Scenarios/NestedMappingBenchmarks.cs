using AutoMapper;
using BenchmarkDotNet.Attributes;
using GeneratedMapper.Benchmarks.AutoMapperConfig;
using GeneratedMapper.Benchmarks.Models;

namespace GeneratedMapper.Benchmarks.Scenarios;

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

    [Benchmark(Baseline = true, Description = "GeneratedMapper (extension method)")]
    public OrderDto GeneratedMapper_Extension() => _source.ToOrderDto();

    [Benchmark(Description = "GeneratedMapper (dispatcher)")]
    public OrderDto GeneratedMapper_Dispatcher() => GeneratedMappings.Map<OrderDto>(_source);

    [Benchmark(Description = "AutoMapper")]
    public OrderDto AutoMapper() => _autoMapper.Map<OrderDto>(_source);
}

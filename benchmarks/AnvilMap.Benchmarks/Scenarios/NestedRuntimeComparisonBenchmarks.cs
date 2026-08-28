using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using AnvilMap.Benchmarks.AutoMapperConfig;
using AnvilMap.Benchmarks.Models;

namespace AnvilMap.Benchmarks.Scenarios;

// See FlatRuntimeComparisonBenchmarks for why this class (and its siblings) exist. Nested shape:
// one level of nested reference mapping (Order -> Customer).
[SimpleJob(RuntimeMoniker.Net60)]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
public class NestedRuntimeComparisonBenchmarks
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
    public OrderDto Extension() => _source.ToOrderDto();

    [Benchmark(Description = "AnvilMap (dispatcher)")]
    public OrderDto Dispatcher() => GeneratedMappings.Map<OrderDto>(_source);

    [Benchmark(Description = "AutoMapper")]
    public OrderDto AutoMapper() => _autoMapper.Map<OrderDto>(_source);
}

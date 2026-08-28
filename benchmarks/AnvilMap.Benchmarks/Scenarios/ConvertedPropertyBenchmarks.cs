using BenchmarkDotNet.Attributes;
using AnvilMap.Benchmarks.AutoMapperConfig;
using AnvilMap.Benchmarks.Models;

namespace AnvilMap.Benchmarks.Scenarios;

[MemoryDiagnoser]
public class ConvertedPropertyBenchmarks
{
    private ConvertedSource _source = null!;
    private AutoMapper.IMapper _autoMapper = null!;

    [GlobalSetup]
    public void Setup()
    {
        _source = new ConvertedSource { Id = 1, FirstName = "Ada", LastName = "Lovelace" };
        _autoMapper = BenchmarkMapperFactory.CreateMapper();
    }

    [Benchmark(Baseline = true, Description = "AnvilMap (extension method, [MapUsing])")]
    public ConvertedDto AnvilMap_Extension() => _source.ToConvertedDto();

    [Benchmark(Description = "AnvilMap (dispatcher, [MapUsing])")]
    public ConvertedDto AnvilMap_Dispatcher() => GeneratedMappings.Map<ConvertedDto>(_source);

    [Benchmark(Description = "AutoMapper (.MapFrom(...))")]
    public ConvertedDto AutoMapper() => _autoMapper.Map<ConvertedDto>(_source);
}

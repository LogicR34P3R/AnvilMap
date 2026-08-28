using BenchmarkDotNet.Attributes;
using GeneratedMapper.Benchmarks.AutoMapperConfig;
using GeneratedMapper.Benchmarks.Models;

namespace GeneratedMapper.Benchmarks.Scenarios;

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

    [Benchmark(Baseline = true, Description = "GeneratedMapper (extension method, [MapUsing])")]
    public ConvertedDto GeneratedMapper_Extension() => _source.ToConvertedDto();

    [Benchmark(Description = "GeneratedMapper (dispatcher, [MapUsing])")]
    public ConvertedDto GeneratedMapper_Dispatcher() => GeneratedMappings.Map<ConvertedDto>(_source);

    [Benchmark(Description = "AutoMapper (.MapFrom(...))")]
    public ConvertedDto AutoMapper() => _autoMapper.Map<ConvertedDto>(_source);
}

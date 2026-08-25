using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using GeneratedMapper.Benchmarks.Models;

namespace GeneratedMapper.Benchmarks.Scenarios;

// See FlatRuntimeComparisonBenchmarks for why this class (and its siblings) exist. Converted
// shape: [MapUsing] running a converter method against the property value.
[SimpleJob(RuntimeMoniker.Net60)]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
public class ConvertedRuntimeComparisonBenchmarks
{
    private ConvertedSource _source = null!;

    [GlobalSetup]
    public void Setup()
    {
        _source = new ConvertedSource { Id = 1, FirstName = "Ada", LastName = "Lovelace" };
    }

    [Benchmark(Baseline = true, Description = "GeneratedMapper (extension method, [MapUsing])")]
    public ConvertedDto Extension() => _source.ToConvertedDto();

    [Benchmark(Description = "GeneratedMapper (dispatcher, [MapUsing])")]
    public ConvertedDto Dispatcher() => GeneratedMappings.Map<ConvertedDto>(_source);
}

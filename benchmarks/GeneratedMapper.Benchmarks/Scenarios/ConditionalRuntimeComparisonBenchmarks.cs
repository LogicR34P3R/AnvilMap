using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using GeneratedMapper.Benchmarks.Models;

namespace GeneratedMapper.Benchmarks.Scenarios;

// See FlatRuntimeComparisonBenchmarks for why this class (and its siblings) exist. Conditional
// shape: [MapCondition] guarding a property assignment.
[SimpleJob(RuntimeMoniker.Net60)]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
public class ConditionalRuntimeComparisonBenchmarks
{
    private ConditionalSource _source = null!;

    [GlobalSetup]
    public void Setup()
    {
        _source = new ConditionalSource { Id = 1, Name = "Record", Secret = "classified", IsRestricted = false };
    }

    [Benchmark(Baseline = true, Description = "GeneratedMapper (extension method, [MapCondition])")]
    public ConditionalDto Extension() => _source.ToConditionalDto();

    [Benchmark(Description = "GeneratedMapper (dispatcher, [MapCondition])")]
    public ConditionalDto Dispatcher() => GeneratedMappings.Map<ConditionalDto>(_source);
}

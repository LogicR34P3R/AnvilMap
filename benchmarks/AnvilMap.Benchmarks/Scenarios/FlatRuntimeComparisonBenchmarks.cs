using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using AnvilMap.Benchmarks.AutoMapperConfig;
using AnvilMap.Benchmarks.Models;

namespace AnvilMap.Benchmarks.Scenarios;

// Both mappers' throughput across the TFMs AnvilMap's own capability detection branches on,
// so AutoMapper's relative standing can be checked across runtimes too, not just the dispatcher
// path. net6.0 exercises the Dictionary-dispatch fallback (no FrozenDictionary below .NET 8; net6.0
// stands in for netstandard2.0 since that TFM can't run as an executable), net8.0 is today's
// baseline, net10.0 is the same generated code on the newer runtime. [SimpleJob] stacking gives
// one BenchmarkDotNet report with one column per runtime - the project multi-targets
// net6.0;net8.0;net10.0 specifically so this class needs only these three attributes.
[SimpleJob(RuntimeMoniker.Net60)]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
public class FlatRuntimeComparisonBenchmarks
{
    private FlatSource _source = null!;
    private FlatDto _existingDestination = null!;
    private AutoMapper.IMapper _autoMapper = null!;
    private AnvilMap.IMapper _generatedIMapper = null!;

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
        _existingDestination = new FlatDto();

        _autoMapper = BenchmarkMapperFactory.CreateMapper();
        _generatedIMapper = new AnvilMapService();
    }

    [Benchmark(Baseline = true, Description = "AnvilMap (extension method)")]
    public FlatDto Extension() => _source.ToFlatDto();

    [Benchmark(Description = "AnvilMap (dispatcher)")]
    public FlatDto Dispatcher() => GeneratedMappings.Map<FlatDto>(_source);

    // Unlike Dispatcher() above (type-erased, can never be intercepted - only the destination is
    // statically known there), this closed two-type-argument call is the shape interceptor
    // discovery targets: on net10.0 the compiler redirects it straight to FlatSource.ToFlatDto(),
    // skipping the dictionary lookup; on net6.0/net8.0 it's identical to Dispatcher() above.
    [Benchmark(Description = "AnvilMap (dispatcher, closed generic)")]
    public FlatDto DispatcherClosedGeneric() => GeneratedMappings.Map<FlatSource, FlatDto>(_source);

    // Same interceptable shape, but the two-arg overload (maps into an existing instance).
    [Benchmark(Description = "AnvilMap (dispatcher, closed generic, two-arg)")]
    public FlatDto DispatcherClosedGenericTwoArg() => GeneratedMappings.Map<FlatSource, FlatDto>(_source, _existingDestination);

    // Control group: same closed pair as DispatcherClosedGeneric(), called through IMapper
    // instead of the concrete static class. Interceptors redirect by source location, so an
    // interface-typed receiver is never a candidate regardless of C# version - this number should
    // NOT drop on net10.0 the way DispatcherClosedGeneric()'s did.
    [Benchmark(Description = "AnvilMap (IMapper, closed generic)")]
    public FlatDto IMapperDispatcher() => _generatedIMapper.Map<FlatSource, FlatDto>(_source);

    // Same control, IMapper's two-arg overload - stays on the dictionary path for the same
    // reason, so it gets the two-arg shape's no-allocation advantage but not interception's.
    [Benchmark(Description = "AnvilMap (IMapper, closed generic, two-arg)")]
    public FlatDto IMapperDispatcherTwoArg() => _generatedIMapper.Map<FlatSource, FlatDto>(_source, _existingDestination);

    [Benchmark(Description = "AutoMapper")]
    public FlatDto AutoMapper() => _autoMapper.Map<FlatDto>(_source);
}

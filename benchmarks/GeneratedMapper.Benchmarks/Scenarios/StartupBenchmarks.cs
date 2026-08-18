using AutoMapper;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using GeneratedMapper.Benchmarks.AutoMapperConfig;
using GeneratedMapper.Benchmarks.Models;

namespace GeneratedMapper.Benchmarks.Scenarios;

// One-time setup cost, not steady-state throughput: ColdStart with a single launch/iteration
// per invocation, since the default multi-iteration warmup would measure repeated
// configuration builds hitting internal caches, not the real first-run cost an app pays
// exactly once at its own startup.
[SimpleJob(RunStrategy.ColdStart, launchCount: 5, warmupCount: 0, iterationCount: 1)]
[MemoryDiagnoser]
public class StartupBenchmarks
{
    // The honest number here is close to "zero, same as calling any other static C#
    // method" - there is no runtime configuration step at all - but measure it rather
    // than asserting it.
    [Benchmark(Baseline = true, Description = "GeneratedMapper (first call, no configuration step)")]
    public FlatDto GeneratedMapper_FirstCall()
    {
        var source = new FlatSource { Id = 1, Name = "Widget", CreatedAt = DateTime.UtcNow, IsActive = true, Amount = 19.99m };
        return source.ToFlatDto();
    }

    [Benchmark(Description = "AutoMapper (build MapperConfiguration)")]
    public MapperConfiguration AutoMapper_BuildConfiguration()
        => BenchmarkMapperFactory.CreateConfiguration();

    [Benchmark(Description = "AutoMapper (build + AssertConfigurationIsValid)")]
    public MapperConfiguration AutoMapper_BuildAndValidateConfiguration()
    {
        var config = BenchmarkMapperFactory.CreateConfiguration();
        config.AssertConfigurationIsValid();
        return config;
    }
}

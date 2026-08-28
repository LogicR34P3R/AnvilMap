using AutoMapper;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using AnvilMap.Benchmarks.AutoMapperConfig;
using AnvilMap.Benchmarks.Models;

namespace AnvilMap.Benchmarks.Scenarios;

// SimpleJobAttribute has no single constructor combining a RuntimeMoniker with a non-default
// RunStrategy - take the Job each single-argument [SimpleJob(moniker)] would have produced (via
// its own IConfigSource.Config) and layer ColdStart's settings on top, one job per targeted
// runtime.
internal sealed class ColdStartRuntimeComparisonConfig : ManualConfig
{
    public ColdStartRuntimeComparisonConfig()
    {
        foreach (var moniker in new[] { RuntimeMoniker.Net60, RuntimeMoniker.Net80, RuntimeMoniker.Net10_0 })
        {
            var runtimeJob = ((IConfigSource)new SimpleJobAttribute(moniker)).Config.GetJobs().Single();
            AddJob(runtimeJob
                .WithStrategy(RunStrategy.ColdStart)
                .WithLaunchCount(5)
                .WithWarmupCount(0)
                .WithIterationCount(1)
                .WithId(moniker.ToString()));
        }
    }
}

// See FlatRuntimeComparisonBenchmarks for why this class (and its siblings) exist. Startup
// shape: mirrors StartupBenchmarks' ColdStart methodology (single launch/iteration, no warmup -
// see that class's own comment for why) rather than the default multi-iteration strategy the
// other *RuntimeComparisonBenchmarks siblings use, applied per runtime instead of just once.
[Config(typeof(ColdStartRuntimeComparisonConfig))]
[MemoryDiagnoser]
public class StartupRuntimeComparisonBenchmarks
{
    [Benchmark(Baseline = true, Description = "AnvilMap (first call, no configuration step)")]
    public FlatDto FirstCall()
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

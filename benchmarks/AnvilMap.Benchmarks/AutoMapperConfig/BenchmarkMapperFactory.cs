using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnvilMap.Benchmarks.AutoMapperConfig;

// AutoMapper 15+'s MapperConfiguration constructor requires an ILoggerFactory; benchmarks
// don't care about its diagnostic logging, so every call site shares this no-op factory.
public static class BenchmarkMapperFactory
{
    public static MapperConfiguration CreateConfiguration()
        => new(cfg => cfg.AddProfile<BenchmarkProfile>(), NullLoggerFactory.Instance);

    public static AutoMapper.IMapper CreateMapper() => CreateConfiguration().CreateMapper();
}

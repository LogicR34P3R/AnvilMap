namespace AnvilMap.Benchmarks.Models;

// GenerateReverse feeds ReverseMappingBenchmarks (AnvilMap's reverse-generation
// path vs. a second, explicit CreateMap<TDest, TSource>() on the AutoMapper side).
[MapTo(typeof(FlatDto), GenerateReverse = true)]
public sealed partial class FlatSource
{
}

using BenchmarkDotNet.Attributes;
using AnvilMap.Benchmarks.AutoMapperConfig;
using AnvilMap.Benchmarks.Models;

namespace AnvilMap.Benchmarks.Scenarios;

// [MapInclude] vs. AutoMapper's own inheritance mapping (CreateMap<Base,BaseDto>()
// .Include<Derived,DerivedDto>()). _source is declared as the base Animal but holds a Dog at
// runtime, so every benchmark method actually exercises runtime-type dispatch.
[MemoryDiagnoser]
public class PolymorphicMappingBenchmarks
{
    private Animal _source = null!;
    private AutoMapper.IMapper _autoMapper = null!;

    [GlobalSetup]
    public void Setup()
    {
        _source = new Dog { Name = "Rex", Breed = "Labrador" };
        _autoMapper = BenchmarkMapperFactory.CreateMapper();
    }

    [Benchmark(Baseline = true, Description = "AnvilMap (extension method)")]
    public AnimalDto AnvilMap_Extension() => _source.ToAnimalDto();

    [Benchmark(Description = "AnvilMap (dispatcher)")]
    public AnimalDto AnvilMap_Dispatcher() => GeneratedMappings.Map<AnimalDto>(_source);

    [Benchmark(Description = "AutoMapper")]
    public AnimalDto AutoMapper() => _autoMapper.Map<AnimalDto>(_source);
}

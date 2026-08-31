namespace AnvilMap.Benchmarks.Models;

[MapTo(typeof(AnimalDto))]
[MapInclude(typeof(AnimalDto), typeof(Dog), typeof(DogDto))]
[MapInclude(typeof(AnimalDto), typeof(Cat), typeof(CatDto))]
public partial class Animal
{
}

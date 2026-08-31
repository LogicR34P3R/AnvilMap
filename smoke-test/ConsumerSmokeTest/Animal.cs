using AnvilMap;

[MapTo(typeof(AnimalDto))]
[MapInclude(typeof(AnimalDto), typeof(Dog), typeof(DogDto))]
public class Animal
{
    public string Name { get; set; } = "";
}

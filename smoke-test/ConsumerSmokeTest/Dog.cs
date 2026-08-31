using AnvilMap;

[MapTo(typeof(DogDto))]
public sealed class Dog : Animal
{
    public string Breed { get; set; } = "";
}

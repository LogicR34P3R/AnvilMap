namespace AnvilMap.Benchmarks.Models;

// Base of a small inheritance hierarchy for the polymorphic-mapping scenario - both mappers
// dispatch a Dog/Cat instance handed in as an Animal to the richer derived DTO.
public partial class Animal
{
    public string Name { get; set; } = "";
}

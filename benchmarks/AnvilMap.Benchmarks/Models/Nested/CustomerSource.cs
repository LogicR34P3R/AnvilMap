namespace AnvilMap.Benchmarks.Models;

// One level of nested reference-type mapping: Order -> Customer.
public sealed partial class CustomerSource
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
}

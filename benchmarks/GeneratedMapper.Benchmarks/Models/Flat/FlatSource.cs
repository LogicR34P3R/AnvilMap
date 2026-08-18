namespace GeneratedMapper.Benchmarks.Models;

// Simplest realistic case: ~5 scalar properties, no nesting.
public sealed partial class FlatSource
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public decimal Amount { get; set; }
}

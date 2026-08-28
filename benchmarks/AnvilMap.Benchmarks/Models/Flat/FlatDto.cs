namespace AnvilMap.Benchmarks.Models;

public sealed class FlatDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public decimal Amount { get; set; }
}

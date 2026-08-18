namespace GeneratedMapper.Benchmarks.Models;

public sealed partial class OrderSource
{
    public int Id { get; set; }
    public DateTime PlacedAt { get; set; }
    public decimal Total { get; set; }
    public CustomerSource Customer { get; set; } = new();
}

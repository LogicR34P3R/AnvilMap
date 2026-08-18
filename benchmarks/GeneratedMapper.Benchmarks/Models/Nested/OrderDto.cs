namespace GeneratedMapper.Benchmarks.Models;

public sealed class OrderDto
{
    public int Id { get; set; }
    public DateTime PlacedAt { get; set; }
    public decimal Total { get; set; }
    public CustomerDto Customer { get; set; } = new();
}

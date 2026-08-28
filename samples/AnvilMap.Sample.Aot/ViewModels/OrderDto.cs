namespace AnvilMap.Sample.Aot.ViewModels;

public sealed class OrderDto
{
    public int Id { get; set; }
    public string Reference { get; set; } = "";
    public string InternalNotes { get; set; } = "";
    public decimal Total { get; set; }
    public string CustomerEmail { get; set; } = "";
    public CustomerDto Customer { get; set; } = new();
    public List<LineItemDto> LineItems { get; set; } = new();
}

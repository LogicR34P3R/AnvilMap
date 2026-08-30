using AnvilMap.Sample.Aot.Models;

namespace AnvilMap.Sample.Aot.ViewModels;

// Declared from the destination side instead of [MapTo] on LineItem.
[MapFrom(typeof(LineItem))]
public sealed class LineItemDto
{
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

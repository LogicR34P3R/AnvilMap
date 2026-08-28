using AnvilMap.Sample.Aot.ViewModels;

namespace AnvilMap.Sample.Aot.Models;

[MapTo(typeof(LineItemDto))]
public sealed class LineItem
{
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

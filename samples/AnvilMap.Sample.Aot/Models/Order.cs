using AnvilMap.Sample.Aot.ViewModels;

namespace AnvilMap.Sample.Aot.Models;

[MapTo(typeof(OrderDto))]
[MapCondition(typeof(OrderDto), nameof(OrderDto.InternalNotes), nameof(ShouldMapInternalNotes))]
[MapUsing(typeof(OrderDto), nameof(OrderDto.Total), nameof(ComputeTotal))]
[MapProperty(typeof(OrderDto), "Customer.Email", nameof(OrderDto.CustomerEmail))]
public sealed class Order
{
    public int Id { get; set; }
    public string Reference { get; set; } = "";
    public bool IsInternal { get; set; }
    public string InternalNotes { get; set; } = "";
    public Customer Customer { get; set; } = new();
    public List<LineItem> LineItems { get; set; } = new();

    public static bool ShouldMapInternalNotes(Order source) => source.IsInternal;

    public static decimal ComputeTotal(Order source) =>
        source.LineItems.Sum(item => item.Quantity * item.UnitPrice);
}

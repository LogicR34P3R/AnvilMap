using System.Collections.Generic;
using System.Linq;
using GeneratedMapper;
using GeneratedMapper.Sample.Aot.ViewModels;

namespace GeneratedMapper.Sample.Aot.Models;

[MapTo(typeof(OrderDto))]
[MapCondition(typeof(OrderDto), nameof(OrderDto.InternalNotes), nameof(ShouldMapInternalNotes))]
[MapUsing(typeof(OrderDto), nameof(OrderDto.Total), nameof(ComputeTotal))]
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

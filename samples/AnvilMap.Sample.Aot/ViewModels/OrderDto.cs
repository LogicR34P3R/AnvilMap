using System.Collections.Immutable;
using System.Collections.ObjectModel;

namespace AnvilMap.Sample.Aot.ViewModels;

public sealed class OrderDto
{
    public int Id { get; set; }
    public string Reference { get; set; } = "";
    public string InternalNotes { get; set; } = "";
    public decimal Total { get; set; }
    public string CustomerEmail { get; set; } = "";
    public string Status { get; set; } = "";
    public int StatusCode { get; set; }
    public string PromoCode { get; set; } = "";
    public CustomerDto Customer { get; set; } = new();
    public List<LineItemDto> LineItems { get; set; } = new();

    // Defaults to .Empty, not `default` - an uninitialized ImmutableArray<T> throws on almost
    // any member access.
    public ImmutableArray<string> Tags { get; set; } = ImmutableArray<string>.Empty;

    public ObservableCollection<string> RecentNotes { get; set; } = new();
}

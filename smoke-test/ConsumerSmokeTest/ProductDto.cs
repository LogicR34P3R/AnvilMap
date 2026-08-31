using System.Collections.Immutable;
using System.Collections.ObjectModel;

public sealed class ProductDto
{
    public string Name { get; set; } = "";
    public string DisplayPrice { get; set; } = "";
    public string Description { get; set; } = "";
    public string InternalSku { get; set; } = "";
    public string Status { get; set; } = "";
    public int StatusCode { get; set; }
    public HashSet<string> Tags { get; set; } = new();
    public ImmutableArray<int> RelatedIds { get; set; } = ImmutableArray<int>.Empty;
    public ObservableCollection<string> RecentChanges { get; set; } = new();
}

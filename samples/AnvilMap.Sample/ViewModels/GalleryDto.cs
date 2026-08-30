using System.Collections.Immutable;
using System.Collections.ObjectModel;

namespace AnvilMap.Sample.ViewModels;

public sealed class GalleryDto
{
    public string Name { get; set; } = "";
    public HashSet<string> Tags { get; set; } = new();

    // Defaults to .Empty, not `default` - an uninitialized ImmutableArray<T> throws on almost
    // any member access.
    public ImmutableArray<int> RecentViewCounts { get; set; } = ImmutableArray<int>.Empty;

    public ObservableCollection<PhotoDto> Photos { get; set; } = new();
    public int PhotoCount { get; set; }
}

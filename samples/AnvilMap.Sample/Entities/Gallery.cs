using AnvilMap.Sample.ViewModels;

namespace AnvilMap.Sample.Entities;

// Not an EF Core entity - mapped only imperatively, demonstrating collection shapes and
// [MapUsing] without touching SQL translatability.
[MapTo(typeof(GalleryDto))]
[MapUsing(typeof(GalleryDto), nameof(GalleryDto.PhotoCount), nameof(ComputePhotoCount))]
public sealed class Gallery
{
    public string Name { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public List<int> RecentViewCounts { get; set; } = new();
    public List<Photo> Photos { get; set; } = new();

    public static int ComputePhotoCount(Gallery source) => source.Photos.Count;
}

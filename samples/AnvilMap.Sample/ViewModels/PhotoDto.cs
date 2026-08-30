using AnvilMap.Sample.Entities;

namespace AnvilMap.Sample.ViewModels;

// Declared from the destination side instead of [MapTo] on Photo.
[MapFrom(typeof(Photo))]
public sealed class PhotoDto
{
    public string Url { get; set; } = "";
}

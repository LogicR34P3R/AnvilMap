namespace AnvilMap.Sample.Aot.ViewModels;

public class AttachmentDto
{
    public string FileName { get; set; } = "";
}

public sealed class ImageAttachmentDto : AttachmentDto
{
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed class VideoAttachmentDto : AttachmentDto
{
    public int DurationSeconds { get; set; }
}

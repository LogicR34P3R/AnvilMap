using AnvilMap.Sample.ViewModels;

namespace AnvilMap.Sample.Entities;

[MapTo(typeof(AttachmentDto))]
[MapInclude(typeof(AttachmentDto), typeof(ImageAttachment), typeof(ImageAttachmentDto))]
[MapInclude(typeof(AttachmentDto), typeof(VideoAttachment), typeof(VideoAttachmentDto))]
public class Attachment
{
    public string FileName { get; set; } = "";
}

[MapTo(typeof(ImageAttachmentDto))]
public sealed class ImageAttachment : Attachment
{
    public int Width { get; set; }
    public int Height { get; set; }
}

[MapTo(typeof(VideoAttachmentDto))]
public sealed class VideoAttachment : Attachment
{
    public int DurationSeconds { get; set; }
}

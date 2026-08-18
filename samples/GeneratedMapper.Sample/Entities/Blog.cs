using System.Collections.Generic;
using GeneratedMapper;
using GeneratedMapper.Sample.ViewModels;

namespace GeneratedMapper.Sample.Entities;

[MapTo(typeof(BlogDto), GenerateReverse = true)]
[MapProperty(typeof(BlogDto), nameof(OwnerEmail), nameof(BlogDto.Author))]
public sealed class Blog
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string OwnerEmail { get; set; } = "";
    public List<Post> Posts { get; set; } = new();
}

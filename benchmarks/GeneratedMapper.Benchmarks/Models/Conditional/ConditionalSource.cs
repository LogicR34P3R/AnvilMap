namespace GeneratedMapper.Benchmarks.Models;

// One property that should only map under a condition, mirroring [MapCondition]'s
// existing test scenarios in MappingSourceGeneratorTests.cs (Post.Body/IsDraft).
public sealed partial class ConditionalSource
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Secret { get; set; } = "";
    public bool IsRestricted { get; set; }
}

namespace AnvilMap.Benchmarks.Models;

[MapTo(typeof(ConditionalDto))]
[MapCondition(typeof(ConditionalDto), nameof(ConditionalDto.Secret), nameof(ShouldMapSecret))]
public sealed partial class ConditionalSource
{
    public static bool ShouldMapSecret(ConditionalSource source) => !source.IsRestricted;
}

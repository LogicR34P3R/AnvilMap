using GeneratedMapper;

namespace GeneratedMapper.Benchmarks.Models;

[MapTo(typeof(ConvertedDto))]
[MapUsing(typeof(ConvertedDto), nameof(ConvertedDto.FullName), nameof(ComputeFullName))]
public sealed partial class ConvertedSource
{
    public static string ComputeFullName(ConvertedSource source) => $"{source.FirstName} {source.LastName}";
}

namespace AnvilMap.Benchmarks.Models;

// HeadlineLength's [MapUsing] opts into InlineInProjection - single-expression, so it's spliced
// into ProjectToGraphBlogDto()'s SQL instead of staying an opaque method call.
[MapTo(typeof(GraphPostDto))]
[MapUsing(typeof(GraphPostDto), nameof(GraphPostDto.HeadlineLength), nameof(ComputeHeadlineLength), InlineInProjection = true)]
public sealed partial class GraphPost
{
    public static int ComputeHeadlineLength(GraphPost source) => source.Headline.Length;
}

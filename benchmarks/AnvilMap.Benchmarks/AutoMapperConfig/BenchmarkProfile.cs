using AutoMapper;
using AnvilMap.Benchmarks.Models;

namespace AnvilMap.Benchmarks.AutoMapperConfig;

// Mirrors every [MapTo]/[MapCondition]/[MapUsing] declaration in AnvilMapConfig/,
// against the exact same POCOs in Models/, so both mappers are compared on identical shapes.
public sealed class BenchmarkProfile : Profile
{
    public BenchmarkProfile()
    {
        CreateMap<FlatSource, FlatDto>();
        CreateMap<FlatDto, FlatSource>();

        CreateMap<CustomerSource, CustomerDto>();
        CreateMap<OrderSource, OrderDto>();

        CreateMap<GraphComment, GraphCommentDto>();
        CreateMap<GraphPost, GraphPostDto>();
        CreateMap<GraphBlog, GraphBlogDto>();

        CreateMap<ConditionalSource, ConditionalDto>()
            .ForMember(d => d.Secret, opt => opt.Condition(src => !src.IsRestricted));

        CreateMap<ConvertedSource, ConvertedDto>()
            .ForMember(d => d.FullName, opt => opt.MapFrom(src => $"{src.FirstName} {src.LastName}"));
    }
}

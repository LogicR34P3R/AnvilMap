using AnvilMap.Benchmarks.AutoMapperConfig;
using AnvilMap.Benchmarks.Models;

namespace AnvilMap.Benchmarks.ParityTests;

// A benchmark comparing two implementations that don't actually produce equivalent
// output is worse than no benchmark - it would silently validate a bug instead of
// catching one. This must pass before any benchmark numbers are treated as meaningful.
public sealed class ParityTests
{
    private readonly AutoMapper.IMapper _autoMapper = BenchmarkMapperFactory.CreateMapper();

    [Fact]
    public void Flat_ProducesEquivalentOutput()
    {
        var source = new FlatSource
        {
            Id = 1,
            Name = "Widget",
            CreatedAt = new DateTime(2026, 1, 1),
            IsActive = true,
            Amount = 19.99m,
        };

        var generated = source.ToFlatDto();
        var auto = _autoMapper.Map<FlatDto>(source);

        Assert.Equal(generated.Id, auto.Id);
        Assert.Equal(generated.Name, auto.Name);
        Assert.Equal(generated.CreatedAt, auto.CreatedAt);
        Assert.Equal(generated.IsActive, auto.IsActive);
        Assert.Equal(generated.Amount, auto.Amount);
    }

    [Fact]
    public void Flat_ReverseMapping_ProducesEquivalentOutput()
    {
        var dto = new FlatDto
        {
            Id = 1,
            Name = "Widget",
            CreatedAt = new DateTime(2026, 1, 1),
            IsActive = true,
            Amount = 19.99m,
        };

        var generated = dto.ToFlatSource();
        var auto = _autoMapper.Map<FlatSource>(dto);

        Assert.Equal(generated.Id, auto.Id);
        Assert.Equal(generated.Name, auto.Name);
        Assert.Equal(generated.CreatedAt, auto.CreatedAt);
        Assert.Equal(generated.IsActive, auto.IsActive);
        Assert.Equal(generated.Amount, auto.Amount);
    }

    [Fact]
    public void Nested_ProducesEquivalentOutput()
    {
        var source = new OrderSource
        {
            Id = 1,
            PlacedAt = new DateTime(2026, 1, 1),
            Total = 249.5m,
            Customer = new CustomerSource { Id = 1, Name = "Ada Lovelace", Email = "ada@example.com" },
        };

        var generated = source.ToOrderDto();
        var auto = _autoMapper.Map<OrderDto>(source);

        Assert.Equal(generated.Id, auto.Id);
        Assert.Equal(generated.PlacedAt, auto.PlacedAt);
        Assert.Equal(generated.Total, auto.Total);
        Assert.Equal(generated.Customer.Id, auto.Customer.Id);
        Assert.Equal(generated.Customer.Name, auto.Customer.Name);
        Assert.Equal(generated.Customer.Email, auto.Customer.Email);
    }

    [Fact]
    public void Graph_ProducesEquivalentOutput()
    {
        var source = new GraphBlog
        {
            Id = 1,
            Title = "Benchmarks",
            Posts =
            {
                new GraphPost
                {
                    Id = 1,
                    Headline = "Post 1",
                    Comments = { new GraphComment { Id = 1, Author = "Reader", Text = "Nice post!" } },
                },
            },
        };

        var generated = source.ToGraphBlogDto();
        var auto = _autoMapper.Map<GraphBlogDto>(source);

        Assert.Equal(generated.Id, auto.Id);
        Assert.Equal(generated.Title, auto.Title);
        Assert.Equal(generated.Posts.Count, auto.Posts.Count);
        Assert.Equal(generated.Posts[0].Headline, auto.Posts[0].Headline);
        Assert.Equal(generated.Posts[0].Comments.Count, auto.Posts[0].Comments.Count);
        Assert.Equal(generated.Posts[0].Comments[0].Author, auto.Posts[0].Comments[0].Author);
        Assert.Equal(generated.Posts[0].Comments[0].Text, auto.Posts[0].Comments[0].Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Conditional_ProducesEquivalentOutput(bool isRestricted)
    {
        var source = new ConditionalSource { Id = 1, Name = "Record", Secret = "classified", IsRestricted = isRestricted };

        var generated = source.ToConditionalDto();
        var auto = _autoMapper.Map<ConditionalDto>(source);

        Assert.Equal(generated.Id, auto.Id);
        Assert.Equal(generated.Name, auto.Name);
        Assert.Equal(generated.Secret, auto.Secret);
        Assert.Equal(isRestricted ? "" : "classified", generated.Secret);
    }

    [Fact]
    public void Converted_ProducesEquivalentOutput()
    {
        var source = new ConvertedSource { Id = 1, FirstName = "Ada", LastName = "Lovelace" };

        var generated = source.ToConvertedDto();
        var auto = _autoMapper.Map<ConvertedDto>(source);

        Assert.Equal(generated.Id, auto.Id);
        Assert.Equal(generated.FullName, auto.FullName);
        Assert.Equal("Ada Lovelace", generated.FullName);
    }
}

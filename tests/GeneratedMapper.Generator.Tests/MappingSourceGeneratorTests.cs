using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Xunit;

namespace GeneratedMapper.Generator.Tests;

public class MappingSourceGeneratorTests
{
    [Fact]
    public void DirectMapping_GeneratesExtensionMethods()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}

public sealed class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("public static global::Sample.UserDto ToUserDto(this global::Sample.User source)", result.GeneratedSource);
        Assert.Contains("destination.Id = source.Id;", result.GeneratedSource);
        Assert.Contains("destination.Name = source.Name;", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapProperty_RenamesDestinationProperty()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapProperty(typeof(UserDto), nameof(Email), nameof(UserDto.EmailAddress))]
public sealed class User
{
    public string Email { get; set; } = """";
}

public sealed class UserDto
{
    public string EmailAddress { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.EmailAddress = source.Email;", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapIgnore_ExcludesDestinationProperty()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
}

public sealed class UserDto
{
    public int Id { get; set; }

    [MapIgnore]
    public int ComputedOnly { get; set; }
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.DoesNotContain("ComputedOnly", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void GenerateReverse_GeneratesBothDirections()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto), GenerateReverse = true)]
public sealed class User
{
    public int Id { get; set; }
}

public sealed class UserDto
{
    public int Id { get; set; }
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("ToUserDto(this global::Sample.User source)", result.GeneratedSource);
        Assert.Contains("ToUser(this global::Sample.UserDto source)", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void NestedMapping_CallsNestedExtensionMethod()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(AddressDto))]
public sealed class Address
{
    public string City { get; set; } = """";
}

public sealed class AddressDto
{
    public string City { get; set; } = """";
}

[MapTo(typeof(UserDto))]
public sealed class User
{
    public Address? HomeAddress { get; set; }
}

public sealed class UserDto
{
    public AddressDto? HomeAddress { get; set; }
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.HomeAddress = source.HomeAddress?.ToAddressDto()!;", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void EnumerableMapping_GeneratesSelectToList()
    {
        var result = GeneratorTestHelper.Run(@"
using System.Collections.Generic;
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(ItemDto))]
public sealed class Item
{
    public int Id { get; set; }
}

public sealed class ItemDto
{
    public int Id { get; set; }
}

[MapTo(typeof(BasketDto))]
public sealed class Basket
{
    public List<Item> Items { get; set; } = new();
}

public sealed class BasketDto
{
    public List<ItemDto> Items { get; set; } = new();
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.Items = source.Items.Select(x => x.ToItemDto()).ToList();", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void Projection_UsesInlinedObjectInitializersNotMethodCalls()
    {
        var result = GeneratorTestHelper.Run(@"
using System.Collections.Generic;
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(ItemDto))]
public sealed class Item
{
    public int Id { get; set; }
}

public sealed class ItemDto
{
    public int Id { get; set; }
}

[MapTo(typeof(BasketDto))]
public sealed class Basket
{
    public List<Item> Items { get; set; } = new();
}

public sealed class BasketDto
{
    public List<ItemDto> Items { get; set; } = new();
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("Expression<Func<global::Sample.Basket, global::Sample.BasketDto>> BasketToBasketDtoProjection", result.GeneratedSource);
        Assert.Contains("Items = source.Items.Select(x => new global::Sample.ItemDto { Id = x.Id }).ToList()", result.GeneratedSource);
        Assert.Contains("ProjectToBasketDto(this IQueryable<global::Sample.Basket> source)", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void CyclicMapping_SkipsProjectionAndReportsGM002()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(ADto))]
public sealed class A
{
    public int Id { get; set; }
    public B? Nested { get; set; }
}

[MapTo(typeof(BDto))]
public sealed class B
{
    public int Id { get; set; }
    public A? Parent { get; set; }
}

public sealed class ADto
{
    public int Id { get; set; }
    public BDto? Nested { get; set; }
}

public sealed class BDto
{
    public int Id { get; set; }
    public ADto? Parent { get; set; }
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM002");
        Assert.DoesNotContain("ToADtoProjection", result.GeneratedSource);
        Assert.DoesNotContain("ToBDtoProjection", result.GeneratedSource);

        // The imperative (non-projection) mappings still work for acyclic data at runtime.
        Assert.Contains("ToADto(this global::Sample.A source)", result.GeneratedSource);
        Assert.Contains("ToBDto(this global::Sample.B source)", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void IncompatibleTypes_ReportsGM003AndOmitsProperty()
    {
        var result = GeneratorTestHelper.Run(@"
using System;
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Code { get; set; }
}

public sealed class UserDto
{
    public Guid Code { get; set; }
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM003" && d.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain("destination.Code", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void Condition_GatesImperativeAssignment()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(PostDto))]
[MapCondition(typeof(PostDto), nameof(PostDto.Body), nameof(ShouldMapBody))]
public sealed class Post
{
    public string Body { get; set; } = """";
    public bool IsDraft { get; set; }

    public static bool ShouldMapBody(Post source) => !source.IsDraft;
}

public sealed class PostDto
{
    public string Body { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("if (global::Sample.Post.ShouldMapBody(source))", result.GeneratedSource);
        Assert.Contains("destination.Body = source.Body;", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void Condition_ResolvesTwoArgOverload()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(PostDto))]
[MapCondition(typeof(PostDto), nameof(PostDto.Body), nameof(ShouldMapBody))]
public sealed class Post
{
    public string Body { get; set; } = """";

    public static bool ShouldMapBody(Post source, PostDto? destination) => string.IsNullOrEmpty(destination?.Body);
}

public sealed class PostDto
{
    public string Body { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("if (global::Sample.Post.ShouldMapBody(source, destination))", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void Condition_MissingMethod_ReportsGM004()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(PostDto))]
[MapCondition(typeof(PostDto), nameof(PostDto.Body), ""DoesNotExist"")]
public sealed class Post
{
    public string Body { get; set; } = """";
}

public sealed class PostDto
{
    public string Body { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM004" && d.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain("destination.Body", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void Condition_ExcludedFromProjectionButRestOfProjectionSurvives()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(PostDto))]
[MapCondition(typeof(PostDto), nameof(PostDto.Body), nameof(ShouldMapBody))]
public sealed class Post
{
    public int Id { get; set; }
    public string Body { get; set; } = """";
    public bool IsDraft { get; set; }

    public static bool ShouldMapBody(Post source) => !source.IsDraft;
}

public sealed class PostDto
{
    public int Id { get; set; }
    public string Body { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM005");
        Assert.Contains("ProjectToPostDto(this IQueryable<global::Sample.Post> source)", result.GeneratedSource);
        // Only Id survives into the projection initializer — Body was excluded, not the whole projection.
        Assert.Contains("source => new global::Sample.PostDto { Id = source.Id };", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void Condition_DispatcherAndMapperServiceUnaffected()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(PostDto))]
[MapCondition(typeof(PostDto), nameof(PostDto.Body), nameof(ShouldMapBody))]
public sealed class Post
{
    public string Body { get; set; } = """";
    public bool IsDraft { get; set; }

    public static bool ShouldMapBody(Post source) => !source.IsDraft;
}

public sealed class PostDto
{
    public string Body { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        // The IMapper dispatcher/service are untouched by this feature — they just call the
        // (now-conditional) ToPostDto extension method, so they honor the condition for free.
        Assert.Contains("[(typeof(global::Sample.Post), typeof(global::Sample.PostDto))] = s => ((global::Sample.Post)s).ToPostDto(),", result.GeneratedSource);
        Assert.Contains("public sealed class GeneratedMapperService : global::GeneratedMapper.IMapper", result.GeneratedSource);
        Assert.Contains("if (global::Sample.Post.ShouldMapBody(source))", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void InitOnlyDestination_NonPositionalRecord_UsesObjectInitializerAndOmitsTwoArgOverload()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}

public sealed record UserDto
{
    public int Id { get; init; }
    public string Name { get; init; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("public static global::Sample.UserDto ToUserDto(this global::Sample.User source)", result.GeneratedSource);
        Assert.Contains("var destination = new global::Sample.UserDto { Id = source.Id, Name = source.Name };", result.GeneratedSource);
        Assert.DoesNotContain("ToUserDto(this global::Sample.User source, global::Sample.UserDto destination)", result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM008");
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void InitOnlyDestination_MixedWithRegularSetProperty_AssignsRegularPropertyAfterConstruction()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}

public sealed class UserDto
{
    public int Id { get; init; }
    public string Name { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("var destination = new global::Sample.UserDto { Id = source.Id };", result.GeneratedSource);
        Assert.Contains("destination.Name = source.Name;", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void PositionalRecordDestination_AllPropertiesMatched_UsesConstructorArguments()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}

public sealed record UserDto(int Id, string Name);
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("public static global::Sample.UserDto ToUserDto(this global::Sample.User source)", result.GeneratedSource);
        Assert.Contains("var destination = new global::Sample.UserDto(source.Id, source.Name);", result.GeneratedSource);
        Assert.Contains("source => new global::Sample.UserDto(source.Id, source.Name);", result.GeneratedSource);
        Assert.DoesNotContain("ToUserDto(this global::Sample.User source, global::Sample.UserDto destination)", result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM008");
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "GM006");
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void PositionalRecordDestination_WithUnmappableConstructorParameter_StillReportsGM006AndSkipsMapping()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
}

public sealed record UserDto(int Id, string Name);
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM006" && d.Severity == DiagnosticSeverity.Warning);
        Assert.DoesNotContain("ToUserDto", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void PositionalRecordDestination_WithMapConditionOnConstructorProperty_StillReportsGM006AndSkipsMapping()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapCondition(typeof(UserDto), nameof(UserDto.Name), nameof(ShouldMapName))]
public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = """";

    public static bool ShouldMapName(User source) => true;
}

public sealed record UserDto(int Id, string Name);
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM006" && d.Severity == DiagnosticSeverity.Warning);
        Assert.DoesNotContain("ToUserDto", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void PositionalRecordDestination_WithExtraRegularProperty_AssignsExtraPropertyAfterConstruction()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
    public string Nickname { get; set; } = """";
}

public sealed record UserDto(int Id, string Name)
{
    public string Nickname { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("var destination = new global::Sample.UserDto(source.Id, source.Name);", result.GeneratedSource);
        Assert.Contains("destination.Nickname = source.Nickname;", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void PositionalRecordDestination_WithExtraInitOnlyProperty_UsesTrailingObjectInitializer()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
    public string Nickname { get; set; } = """";
}

public sealed record UserDto(int Id, string Name)
{
    public string Nickname { get; init; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(
            "var destination = new global::Sample.UserDto(source.Id, source.Name) { Nickname = source.Nickname };",
            result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void InitOnlyDestination_WithMapCondition_ReportsGM007AndOmitsProperty()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(PostDto))]
[MapCondition(typeof(PostDto), nameof(PostDto.Body), nameof(ShouldMapBody))]
public sealed class Post
{
    public string Body { get; set; } = """";
    public bool IsDraft { get; set; }

    public static bool ShouldMapBody(Post source) => !source.IsDraft;
}

public sealed record PostDto
{
    public string Body { get; init; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM007" && d.Severity == DiagnosticSeverity.Warning);
        Assert.Contains("var destination = new global::Sample.PostDto {  };", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void InitOnlyDestination_ProjectionStillGeneratedNormally()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
}

public sealed record UserDto
{
    public int Id { get; init; }
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("Expression<Func<global::Sample.User, global::Sample.UserDto>> UserToUserDtoProjection", result.GeneratedSource);
        Assert.Contains("source => new global::Sample.UserDto { Id = source.Id };", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void InitOnlyDestination_DispatcherOmitsMapIntoEntryButKeepsMapEntry()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
}

public sealed record UserDto
{
    public int Id { get; init; }
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("[(typeof(global::Sample.User), typeof(global::Sample.UserDto))] = s => ((global::Sample.User)s).ToUserDto(),", result.GeneratedSource);
        Assert.DoesNotContain("[(typeof(global::Sample.User), typeof(global::Sample.UserDto))] = (s, d) =>", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void HashSetDestination_GeneratesSelectToHashSet()
    {
        var result = GeneratorTestHelper.Run(@"
using System.Collections.Generic;
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(ItemDto))]
public sealed class Item
{
    public int Id { get; set; }
}

public sealed class ItemDto
{
    public int Id { get; set; }
}

[MapTo(typeof(BasketDto))]
public sealed class Basket
{
    public List<Item> Items { get; set; } = new();
}

public sealed class BasketDto
{
    public HashSet<ItemDto> Items { get; set; } = new();
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.Items = source.Items.Select(x => x.ToItemDto()).ToHashSet();", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void ArrayDestination_SameElementType_GeneratesToArray()
    {
        var result = GeneratorTestHelper.Run(@"
using System.Collections.Generic;
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(BasketDto))]
public sealed class Basket
{
    public List<int> Tags { get; set; } = new();
}

public sealed class BasketDto
{
    public int[] Tags { get; set; } = System.Array.Empty<int>();
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.Tags = source.Tags.ToArray();", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void ISetDestination_ProjectionUsesToHashSet()
    {
        var result = GeneratorTestHelper.Run(@"
using System.Collections.Generic;
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(ItemDto))]
public sealed class Item
{
    public int Id { get; set; }
}

public sealed class ItemDto
{
    public int Id { get; set; }
}

[MapTo(typeof(BasketDto))]
public sealed class Basket
{
    public List<Item> Items { get; set; } = new();
}

public sealed class BasketDto
{
    public ISet<ItemDto> Items { get; set; } = new HashSet<ItemDto>();
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("Items = source.Items.Select(x => new global::Sample.ItemDto { Id = x.Id }).ToHashSet()", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MaxDepth_SelfRecursiveNestedProperty_ThreadsDepthCounter()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(CategoryDto), MaxDepth = 3)]
public sealed class Category
{
    public string Name { get; set; } = """";
    public Category? Parent { get; set; }
}

public sealed class CategoryDto
{
    public string Name { get; set; } = """";
    public CategoryDto? Parent { get; set; }
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("public static global::Sample.CategoryDto ToCategoryDto(this global::Sample.Category source, global::Sample.CategoryDto destination)", result.GeneratedSource);
        Assert.Contains("=> source.ToCategoryDto(destination, 0);", result.GeneratedSource);
        Assert.Contains("private static global::Sample.CategoryDto ToCategoryDto(this global::Sample.Category source, global::Sample.CategoryDto destination, int depth)", result.GeneratedSource);
        Assert.Contains("if (depth < 3)", result.GeneratedSource);
        Assert.Contains("destination.Parent = source.Parent?.ToCategoryDto(new global::Sample.CategoryDto(), depth + 1)!;", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MaxDepth_SelfRecursiveEnumerableProperty_ThreadsDepthCounter()
    {
        var result = GeneratorTestHelper.Run(@"
using System.Collections.Generic;
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(CategoryDto), MaxDepth = 2)]
public sealed class Category
{
    public string Name { get; set; } = """";
    public List<Category> Children { get; set; } = new();
}

public sealed class CategoryDto
{
    public string Name { get; set; } = """";
    public List<CategoryDto> Children { get; set; } = new();
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("if (depth < 2)", result.GeneratedSource);
        Assert.Contains("destination.Children = source.Children.Select(x => x.ToCategoryDto(new global::Sample.CategoryDto(), depth + 1)).ToList();", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MaxDepth_ZeroOrUnset_LeavesRecursionUnguarded()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(CategoryDto))]
public sealed class Category
{
    public string Name { get; set; } = """";
    public Category? Parent { get; set; }
}

public sealed class CategoryDto
{
    public string Name { get; set; } = """";
    public CategoryDto? Parent { get; set; }
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.Parent = source.Parent?.ToCategoryDto()!;", result.GeneratedSource);
        Assert.DoesNotContain("int depth", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapUsing_CallsConverterInImperativeMapper()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapUsing(typeof(UserDto), nameof(UserDto.FullName), nameof(ComputeFullName))]
public sealed class User
{
    public string FirstName { get; set; } = """";
    public string LastName { get; set; } = """";

    public static string ComputeFullName(User source) => $""{source.FirstName} {source.LastName}"";
}

public sealed class UserDto
{
    public string FullName { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.FullName = global::Sample.User.ComputeFullName(source);", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapUsing_InlinedInProjection()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapUsing(typeof(UserDto), nameof(UserDto.FullName), nameof(ComputeFullName))]
public sealed class User
{
    public string FirstName { get; set; } = """";
    public string LastName { get; set; } = """";

    public static string ComputeFullName(User source) => $""{source.FirstName} {source.LastName}"";
}

public sealed class UserDto
{
    public string FullName { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("source => new global::Sample.UserDto { FullName = global::Sample.User.ComputeFullName(source) };", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapUsing_AllowsImplicitlyConvertibleReturnType()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapUsing(typeof(UserDto), nameof(UserDto.Code), nameof(ComputeCode))]
public sealed class User
{
    public int Id { get; set; }

    public static int ComputeCode(User source) => source.Id * 2;
}

public sealed class UserDto
{
    public long Code { get; set; }
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.Code = global::Sample.User.ComputeCode(source);", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapUsing_MissingMethod_ReportsGM009()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapUsing(typeof(UserDto), nameof(UserDto.FullName), ""DoesNotExist"")]
public sealed class User
{
    public string FirstName { get; set; } = """";
}

public sealed class UserDto
{
    public string FullName { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM009" && d.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain("destination.FullName", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapUsing_WrongReturnType_ReportsGM009()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapUsing(typeof(UserDto), nameof(UserDto.FullName), nameof(ComputeFullName))]
public sealed class User
{
    public static int ComputeFullName(User source) => 42;
}

public sealed class UserDto
{
    public string FullName { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM009" && d.Severity == DiagnosticSeverity.Error);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapUsing_NotAutoReversedByGenerateReverse()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto), GenerateReverse = true)]
[MapUsing(typeof(UserDto), nameof(UserDto.FullName), nameof(ComputeFullName))]
public sealed class User
{
    public string FirstName { get; set; } = """";

    public static string ComputeFullName(User source) => source.FirstName;
}

public sealed class UserDto
{
    public string FullName { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        // Forward direction uses the converter.
        Assert.Contains("destination.FullName = global::Sample.User.ComputeFullName(source);", result.GeneratedSource);
        // Reverse direction has no [MapUsing] of its own, and UserDto.FullName has no
        // matching User property by name, so User.FirstName is left unmapped there (GM001) —
        // proving the converter wasn't silently carried over to the reverse mapping.
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM001" && d.GetMessage().Contains("FirstName"));
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapUsing_CombinedWithMapCondition_GatesConvertedValue()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapUsing(typeof(UserDto), nameof(UserDto.FullName), nameof(ComputeFullName))]
[MapCondition(typeof(UserDto), nameof(UserDto.FullName), nameof(ShouldMap))]
public sealed class User
{
    public string FirstName { get; set; } = """";
    public bool IsAnonymous { get; set; }

    public static string ComputeFullName(User source) => source.FirstName;
    public static bool ShouldMap(User source) => !source.IsAnonymous;
}

public sealed class UserDto
{
    public string FullName { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("if (global::Sample.User.ShouldMap(source))", result.GeneratedSource);
        Assert.Contains("destination.FullName = global::Sample.User.ComputeFullName(source);", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void InitOnlyDestination_EnumerableOfInitOnlyElements_UsesElementOneArgMethod()
    {
        var result = GeneratorTestHelper.Run(@"
using System.Collections.Generic;
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(ItemDto))]
public sealed class Item
{
    public int Id { get; set; }
}

public sealed record ItemDto
{
    public int Id { get; init; }
}

[MapTo(typeof(BasketDto))]
public sealed class Basket
{
    public List<Item> Items { get; set; } = new();
}

public sealed class BasketDto
{
    public List<ItemDto> Items { get; set; } = new();
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.Items = source.Items.Select(x => x.ToItemDto()).ToList();", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void InitOnlyDestination_GenerateReverse_ReverseToPlainClassIsUnaffected()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto), GenerateReverse = true)]
public sealed class User
{
    public int Id { get; set; }
}

public sealed record UserDto
{
    public int Id { get; init; }
}
");

        Assert.NotNull(result.GeneratedSource);
        // Forward (User -> record UserDto) omits the two-arg overload.
        Assert.DoesNotContain("ToUserDto(this global::Sample.User source, global::Sample.UserDto destination)", result.GeneratedSource);
        // Reverse (UserDto -> plain class User) keeps it, since User.Id has a regular setter.
        Assert.Contains("public static global::Sample.User ToUser(this global::Sample.UserDto source, global::Sample.User destination)", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void InitOnlyDestination_CombinedWithMapPropertyRename()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapProperty(typeof(UserDto), nameof(Email), nameof(UserDto.EmailAddress))]
public sealed class User
{
    public string Email { get; set; } = """";
}

public sealed record UserDto
{
    public string EmailAddress { get; init; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("var destination = new global::Sample.UserDto { EmailAddress = source.Email };", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void HashSetDestination_SameElementType_UsesToHashSetDirectly()
    {
        var result = GeneratorTestHelper.Run(@"
using System.Collections.Generic;
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(BasketDto))]
public sealed class Basket
{
    public List<int> Tags { get; set; } = new();
}

public sealed class BasketDto
{
    public HashSet<int> Tags { get; set; } = new();
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.Tags = source.Tags.ToHashSet();", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MaxDepth_DoesNotGuardIndirectMutualCycleAcrossDifferentMappings()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(ADto), MaxDepth = 3)]
public sealed class A
{
    public int Id { get; set; }
    public B? Nested { get; set; }
}

[MapTo(typeof(BDto), MaxDepth = 3)]
public sealed class B
{
    public int Id { get; set; }
    public A? Parent { get; set; }
}

public sealed class ADto
{
    public int Id { get; set; }
    public BDto? Nested { get; set; }
}

public sealed class BDto
{
    public int Id { get; set; }
    public ADto? Parent { get; set; }
}
");

        Assert.NotNull(result.GeneratedSource);
        // MaxDepth only guards DIRECT self-reference within one [MapTo] declaration. A's
        // Nested property is type B (a *different* mapping pair), not A itself, so it isn't
        // guarded — no depth parameter should be threaded through either mapping's methods.
        // This documents the current, known limitation rather than a passing feature.
        Assert.DoesNotContain("int depth", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapUsing_MultipleConvertedPropertiesOnSameMapping()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapUsing(typeof(UserDto), nameof(UserDto.FullName), nameof(ComputeFullName))]
[MapUsing(typeof(UserDto), nameof(UserDto.InitialsUpper), nameof(ComputeInitials))]
public sealed class User
{
    public string FirstName { get; set; } = """";
    public string LastName { get; set; } = """";

    public static string ComputeFullName(User source) => $""{source.FirstName} {source.LastName}"";
    public static string ComputeInitials(User source) => $""{source.FirstName[0]}{source.LastName[0]}"".ToUpperInvariant();
}

public sealed class UserDto
{
    public string FullName { get; set; } = """";
    public string InitialsUpper { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.FullName = global::Sample.User.ComputeFullName(source);", result.GeneratedSource);
        Assert.Contains("destination.InitialsUpper = global::Sample.User.ComputeInitials(source);", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapUsing_NonStaticCandidate_IsIgnoredAndReportsGM009()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapUsing(typeof(UserDto), nameof(UserDto.FullName), nameof(ComputeFullName))]
public sealed class User
{
    public string FirstName { get; set; } = """";

    // Instance method — not a valid [MapUsing] target even though the name/shape otherwise match.
    public string ComputeFullName(User source) => source.FirstName;
}

public sealed class UserDto
{
    public string FullName { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM009" && d.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain("destination.FullName", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapDefault_SubstitutesNullOnDirectMapping_ImperativeAndProjection()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapDefault(typeof(UserDto), nameof(UserDto.Name), ""Unknown"")]
public sealed class User
{
    public string? Name { get; set; }
}

public sealed class UserDto
{
    public string Name { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.Name = source.Name ?? \"Unknown\";", result.GeneratedSource);
        Assert.Contains("Name = source.Name ?? \"Unknown\"", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapDefault_AppliesToConvertedProperty()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapUsing(typeof(UserDto), nameof(UserDto.FullName), nameof(ComputeFullName))]
[MapDefault(typeof(UserDto), nameof(UserDto.FullName), ""N/A"")]
public sealed class User
{
    public string? FirstName { get; set; }

    public static string? ComputeFullName(User source) => source.FirstName;
}

public sealed class UserDto
{
    public string FullName { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.FullName = global::Sample.User.ComputeFullName(source) ?? \"N/A\";", result.GeneratedSource);
        Assert.Contains("FullName = global::Sample.User.ComputeFullName(source) ?? \"N/A\"", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapDefault_SupportsNullableValueTypeWithNumericDefault()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapDefault(typeof(UserDto), nameof(UserDto.Age), 18)]
public sealed class User
{
    public int? Age { get; set; }
}

public sealed class UserDto
{
    public int? Age { get; set; }
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.Age = source.Age ?? 18;", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapDefault_CombinedWithMapCondition_GatesTheDefaultedValue()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapCondition(typeof(UserDto), nameof(UserDto.Name), nameof(ShouldMapName))]
[MapDefault(typeof(UserDto), nameof(UserDto.Name), ""Unknown"")]
public sealed class User
{
    public string? Name { get; set; }
    public bool IsActive { get; set; }

    public static bool ShouldMapName(User source) => source.IsActive;
}

public sealed class UserDto
{
    public string Name { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("if (global::Sample.User.ShouldMapName(source))", result.GeneratedSource);
        Assert.Contains("destination.Name = source.Name ?? \"Unknown\";", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapDefault_IgnoredWhenConstantIsNotAFormattableKind()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapDefault(typeof(UserDto), nameof(UserDto.Name), typeof(string))]
public sealed class User
{
    public string? Name { get; set; }
}

public sealed class UserDto
{
    public string Name { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.Name = source.Name;", result.GeneratedSource);
        Assert.DoesNotContain("??", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapDefault_IgnoredOnNonNullableValueType()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapDefault(typeof(UserDto), nameof(UserDto.Id), 42)]
public sealed class User
{
    public int Id { get; set; }
}

public sealed class UserDto
{
    public int Id { get; set; }
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.Id = source.Id;", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapDefault_DuplicateOnSameProperty_ReportsGM017AndDoesNotCrashTheGenerator_LastWins()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapDefault(typeof(UserDto), nameof(UserDto.Name), ""First"")]
[MapDefault(typeof(UserDto), nameof(UserDto.Name), ""Second"")]
public sealed class User
{
    public string? Name { get; set; }
}

public sealed class UserDto
{
    public string Name { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "CS8785");
        Assert.Contains("destination.Name = source.Name ?? \"Second\";", result.GeneratedSource);

        var gm017 = Assert.Single(result.GeneratorDiagnostics.Where(d => d.Id == "GM017"));
        Assert.Equal(DiagnosticSeverity.Warning, gm017.Severity);
        var message = gm017.GetMessage();
        Assert.Contains("Name", message);
        Assert.Contains("[MapDefault]", message);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapCondition_DuplicateOnSameProperty_ReportsGM017AndDoesNotCrashTheGenerator_LastWins()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapCondition(typeof(UserDto), nameof(UserDto.Name), nameof(First))]
[MapCondition(typeof(UserDto), nameof(UserDto.Name), nameof(Second))]
public sealed class User
{
    public string Name { get; set; } = """";

    public static bool First(User source) => true;
    public static bool Second(User source) => true;
}

public sealed class UserDto
{
    public string Name { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "CS8785");
        Assert.Contains("global::Sample.User.Second(source)", result.GeneratedSource);
        Assert.DoesNotContain("global::Sample.User.First(source)", result.GeneratedSource);

        var gm017 = Assert.Single(result.GeneratorDiagnostics.Where(d => d.Id == "GM017"));
        Assert.Equal(DiagnosticSeverity.Warning, gm017.Severity);
        var message = gm017.GetMessage();
        Assert.Contains("Name", message);
        Assert.Contains("[MapCondition]", message);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapProperty_DuplicateOnSameProperty_ReportsGM017_LastWins()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapProperty(typeof(UserDto), nameof(FirstEmail), nameof(UserDto.EmailAddress))]
[MapProperty(typeof(UserDto), nameof(SecondEmail), nameof(UserDto.EmailAddress))]
public sealed class User
{
    public string FirstEmail { get; set; } = """";
    public string SecondEmail { get; set; } = """";
}

public sealed class UserDto
{
    public string EmailAddress { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.EmailAddress = source.SecondEmail;", result.GeneratedSource);

        var gm017 = Assert.Single(result.GeneratorDiagnostics.Where(d => d.Id == "GM017"));
        Assert.Equal(DiagnosticSeverity.Warning, gm017.Severity);
        var message = gm017.GetMessage();
        Assert.Contains("EmailAddress", message);
        Assert.Contains("[MapProperty]", message);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapUsing_DuplicateOnSameProperty_ReportsGM017_LastWins()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapUsing(typeof(UserDto), nameof(UserDto.FullName), nameof(First))]
[MapUsing(typeof(UserDto), nameof(UserDto.FullName), nameof(Second))]
public sealed class User
{
    public string Name { get; set; } = """";

    public static string First(User source) => ""first"";
    public static string Second(User source) => ""second"";
}

public sealed class UserDto
{
    public string FullName { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("global::Sample.User.Second(source)", result.GeneratedSource);
        Assert.DoesNotContain("global::Sample.User.First(source)", result.GeneratedSource);

        var gm017 = Assert.Single(result.GeneratorDiagnostics.Where(d => d.Id == "GM017"));
        Assert.Equal(DiagnosticSeverity.Warning, gm017.Severity);
        var message = gm017.GetMessage();
        Assert.Contains("FullName", message);
        Assert.Contains("[MapUsing]", message);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapCondition_OnTwoDifferentProperties_DoesNotReportGM017()
    {
        // Two [MapCondition] attributes in the same mapping, but naming two different
        // destination properties - not a duplicate, no ambiguity to flag.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapCondition(typeof(UserDto), nameof(UserDto.FirstName), nameof(ShouldMapFirst))]
[MapCondition(typeof(UserDto), nameof(UserDto.LastName), nameof(ShouldMapLast))]
public sealed class User
{
    public string FirstName { get; set; } = """";
    public string LastName { get; set; } = """";

    public static bool ShouldMapFirst(User source) => true;
    public static bool ShouldMapLast(User source) => true;
}

public sealed class UserDto
{
    public string FirstName { get; set; } = """";
    public string LastName { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "GM017");
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapDefault_IgnoredOnNestedProperty()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(AddressDto))]
public sealed class Address
{
    public string City { get; set; } = """";
}

[MapTo(typeof(UserDto))]
[MapDefault(typeof(UserDto), nameof(UserDto.Address), ""Unknown"")]
public sealed class User
{
    public Address Address { get; set; } = new();
}

public sealed class AddressDto
{
    public string City { get; set; } = """";
}

public sealed class UserDto
{
    public AddressDto Address { get; set; } = new();
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.Address = source.Address.ToAddressDto();", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapDefault_SupportsEnumConstant()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

public enum UserStatus { Unknown, Active }

[MapTo(typeof(UserDto))]
[MapDefault(typeof(UserDto), nameof(UserDto.Status), UserStatus.Active)]
public sealed class User
{
    public UserStatus? Status { get; set; }
}

public sealed class UserDto
{
    public UserStatus? Status { get; set; }
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.Status = source.Status ?? global::Sample.UserStatus.Active;", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapDefault_NotAutoReversedByGenerateReverse()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto), GenerateReverse = true)]
[MapDefault(typeof(UserDto), nameof(UserDto.Name), ""Unknown"")]
public sealed class User
{
    public string? Name { get; set; }
}

public sealed class UserDto
{
    public string? Name { get; set; }
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.Name = source.Name ?? \"Unknown\";", result.GeneratedSource);
        Assert.Contains("destination.Name = source.Name;", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapFrom_GeneratesExtensionMethodsSameAsMapTo()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}

[MapFrom(typeof(User))]
public sealed class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("public static global::Sample.UserDto ToUserDto(this global::Sample.User source)", result.GeneratedSource);
        Assert.Contains("destination.Id = source.Id;", result.GeneratedSource);
        Assert.Contains("destination.Name = source.Name;", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapFrom_MapPropertyRenamesDestinationProperty()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

public sealed class User
{
    public string Email { get; set; } = """";
}

[MapFrom(typeof(User))]
[MapProperty(typeof(User), nameof(User.Email), nameof(EmailAddress))]
public sealed class UserDto
{
    public string EmailAddress { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.EmailAddress = source.Email;", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapFrom_GenerateReverse_GeneratesBothDirections()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

public sealed class User
{
    public int Id { get; set; }
}

[MapFrom(typeof(User), GenerateReverse = true)]
public sealed class UserDto
{
    public int Id { get; set; }
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("ToUserDto(this global::Sample.User source)", result.GeneratedSource);
        Assert.Contains("ToUser(this global::Sample.UserDto source)", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapFrom_MapConditionMethodLivesOnDestination_NotSource()
    {
        // The whole point of [MapFrom]: User (the entity) has no idea UserDto exists, and no
        // static method referencing it - ShouldMapName lives on UserDto instead.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

public sealed class User
{
    public string Name { get; set; } = """";
}

[MapFrom(typeof(User))]
[MapCondition(typeof(User), nameof(Name), nameof(ShouldMapName))]
public sealed class UserDto
{
    public string Name { get; set; } = """";

    public static bool ShouldMapName(User source) => !string.IsNullOrEmpty(source.Name);
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("ShouldMapName(source)", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapFrom_MapUsingConverterLivesOnDestination_NotSource()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

public sealed class User
{
    public string FirstName { get; set; } = """";
    public string LastName { get; set; } = """";
}

[MapFrom(typeof(User))]
[MapUsing(typeof(User), nameof(FullName), nameof(ComputeFullName))]
public sealed class UserDto
{
    public string FullName { get; set; } = """";

    public static string ComputeFullName(User source) => $""{source.FirstName} {source.LastName}"";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.FullName = global::Sample.UserDto.ComputeFullName(source);", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapFrom_MapUsingMissingMethod_ReportsGM009AgainstDestinationType()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

public sealed class User
{
    public string FirstName { get; set; } = """";
}

[MapFrom(typeof(User))]
[MapUsing(typeof(User), nameof(FullName), ""DoesNotExist"")]
public sealed class UserDto
{
    public string FullName { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM009" && d.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain("destination.FullName", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapFrom_NestedMapping_WorksAlongsideMapTo()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(AddressDto))]
public sealed class Address
{
    public string City { get; set; } = """";
}

public sealed class AddressDto
{
    public string City { get; set; } = """";
}

public sealed class User
{
    public Address? HomeAddress { get; set; }
}

[MapFrom(typeof(User))]
public sealed class UserDto
{
    public AddressDto? HomeAddress { get; set; }
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.HomeAddress = source.HomeAddress?.ToAddressDto()!;", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapFrom_MapUsing_InlinedInProjection_UsesDestinationTypeQualifier()
    {
        // Regression coverage for the qualifier bug: MappingEmitter.Projection.cs used to
        // hardcode the mapping's *source* type as the converter call's qualifier, which would
        // reference a method that doesn't exist on the source for a [MapFrom]-declared mapping.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

public sealed class User
{
    public string FirstName { get; set; } = """";
    public string LastName { get; set; } = """";
}

[MapFrom(typeof(User))]
[MapUsing(typeof(User), nameof(FullName), nameof(ComputeFullName))]
public sealed class UserDto
{
    public string FullName { get; set; } = """";

    public static string ComputeFullName(User source) => $""{source.FirstName} {source.LastName}"";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("source => new global::Sample.UserDto { FullName = global::Sample.UserDto.ComputeFullName(source) };", result.GeneratedSource);
        Assert.Contains("ProjectToUserDto(this IQueryable<global::Sample.User> source)", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapFrom_MapConditionResolvesTwoArgOverload_MethodOnDestination()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

public sealed class Post
{
    public string Body { get; set; } = """";
}

[MapFrom(typeof(Post))]
[MapCondition(typeof(Post), nameof(Body), nameof(ShouldMapBody))]
public sealed class PostDto
{
    public string Body { get; set; } = """";

    public static bool ShouldMapBody(Post source, PostDto? destination) => string.IsNullOrEmpty(destination?.Body);
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("if (global::Sample.PostDto.ShouldMapBody(source, destination))", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapFrom_MapConditionMissingMethod_ReportsGM004AgainstDestinationType()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

public sealed class Post
{
    public string Body { get; set; } = """";
}

[MapFrom(typeof(Post))]
[MapCondition(typeof(Post), nameof(Body), ""DoesNotExist"")]
public sealed class PostDto
{
    public string Body { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM004" && d.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain("destination.Body", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapFrom_MapDefault_SubstitutesNull_ImperativeAndProjection()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

public sealed class User
{
    public string? Name { get; set; }
}

[MapFrom(typeof(User))]
[MapDefault(typeof(User), nameof(UserDto.Name), ""Unknown"")]
public sealed class UserDto
{
    public string Name { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.Name = source.Name ?? \"Unknown\";", result.GeneratedSource);
        Assert.Contains("Name = source.Name ?? \"Unknown\"", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapFrom_GenerateReverse_ConditionAndConverterNotCarriedToReverseDirection()
    {
        // Same rule as [MapTo]'s GenerateReverse: conditions/converters are tied to the
        // original declaration and aren't auto-reversed. Here that means User.FirstName is
        // left unmapped in the reverse direction (GM001) since UserDto.FullName has no
        // matching User property by name and [MapUsing] wasn't declared for that direction.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

public sealed class User
{
    public string FirstName { get; set; } = """";
}

[MapFrom(typeof(User), GenerateReverse = true)]
[MapUsing(typeof(User), nameof(FullName), nameof(ComputeFullName))]
public sealed class UserDto
{
    public string FullName { get; set; } = """";

    public static string ComputeFullName(User source) => source.FirstName;
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("ToUserDto(this global::Sample.User source)", result.GeneratedSource);
        Assert.Contains("ToUser(this global::Sample.UserDto source)", result.GeneratedSource);
        Assert.Contains("destination.FullName = global::Sample.UserDto.ComputeFullName(source);", result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM001");
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void DuplicateMapTo_SameDestinationDeclaredTwice_ReportsGM011()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
}

public sealed class UserDto
{
    public int Id { get; set; }
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM011" && d.Severity == DiagnosticSeverity.Warning);
        // Both declarations agree, so the mapping itself still comes out fine either way.
        Assert.Contains("public static global::Sample.UserDto ToUserDto(this global::Sample.User source)", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapToAndMapFrom_SamePairDeclaredOnBothSides_ReportsGM011()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
}

[MapFrom(typeof(User))]
public sealed class UserDto
{
    public int Id { get; set; }
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM011" && d.Severity == DiagnosticSeverity.Warning);
        Assert.Contains("public static global::Sample.UserDto ToUserDto(this global::Sample.User source)", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void GenerateReverse_ImpliedPairCollidingWithExplicitDeclaration_ReportsGM011()
    {
        // GenerateReverse on User -> UserDto implies UserDto -> User; an explicit [MapFrom]
        // on User declaring that same UserDto -> User pair collides with it.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto), GenerateReverse = true)]
[MapFrom(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
}

public sealed class UserDto
{
    public int Id { get; set; }
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM011" && d.Severity == DiagnosticSeverity.Warning);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void DistinctMappingPairs_MixingMapToAndMapFrom_DoesNotReportGM011()
    {
        // Different pairs declared via different attributes in the same compilation - no
        // collision, no GM011.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(OrderDto))]
public sealed class Order
{
    public int Id { get; set; }
}

public sealed class OrderDto
{
    public int Id { get; set; }
}

public sealed class User
{
    public int Id { get; set; }
}

[MapFrom(typeof(User))]
public sealed class UserDto
{
    public int Id { get; set; }
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "GM011");
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void CombinedMapFromAndMapTo_EachDirectionNeedsItsOwnCorrectlyOrientedMapProperty()
    {
        // [MapFrom(typeof(User))] and [MapTo(typeof(User))] on the same UserDto share the same
        // "other side" type (User), which is what MappingDiscovery uses to match a [MapProperty]
        // to a declaration. A single [MapProperty(typeof(User), "Email", "EmailAddress")] would
        // only correctly configure the User -> UserDto direction (source property "Email" found
        // on User); the UserDto -> User direction needs its own, oppositely-oriented
        // [MapProperty(typeof(User), "EmailAddress", "Email")]. Both coexist without collision
        // because they're keyed by different DestinationProperty names (EmailAddress vs Email),
        // and each direction's resolver only ever looks up the key matching its own destination
        // type's property name.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

public sealed class User
{
    public string Email { get; set; } = """";
}

[MapFrom(typeof(User))]
[MapProperty(typeof(User), nameof(User.Email), nameof(EmailAddress))]
[MapTo(typeof(User))]
[MapProperty(typeof(User), nameof(EmailAddress), nameof(User.Email))]
public sealed class UserDto
{
    public string EmailAddress { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("public static global::Sample.UserDto ToUserDto(this global::Sample.User source)", result.GeneratedSource);
        Assert.Contains("destination.EmailAddress = source.Email;", result.GeneratedSource);
        Assert.Contains("public static global::Sample.User ToUser(this global::Sample.UserDto source)", result.GeneratedSource);
        Assert.Contains("destination.Email = source.EmailAddress;", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void CombinedMapFromAndMapTo_SingleMapPropertyOnlyConfiguresItsOwnDirection_OtherDirectionReportsGM001()
    {
        // The failure mode if you forget the second, oppositely-oriented [MapProperty]: only
        // one direction gets the rename, the other silently falls back to exact-name matching,
        // finds nothing, and reports GM001 instead of using the rename you (probably) meant for
        // both directions.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

public sealed class User
{
    public string Email { get; set; } = """";
}

[MapFrom(typeof(User))]
[MapProperty(typeof(User), nameof(User.Email), nameof(EmailAddress))]
[MapTo(typeof(User))]
public sealed class UserDto
{
    public string EmailAddress { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.EmailAddress = source.Email;", result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM001" && d.GetMessage().Contains("Email"));
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void CombinedMapFromAndMapTo_DuplicatedInsteadOfReversedMapProperty_OtherDirectionStillReportsGM001()
    {
        // A plausible mistake: instead of writing a second, oppositely-oriented [MapProperty]
        // for the other direction, the same one gets pasted twice. GroupBy+last-wins collapses
        // the duplicate down to one dictionary entry keyed by "EmailAddress" - which only the
        // User -> UserDto direction ever looks up. UserDto -> User still has no entry keyed
        // "Email" to find, so it falls back to exact-name matching, finds nothing, and reports
        // GM001 - same failure mode as never adding a second attribute at all.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

public sealed class User
{
    public string Email { get; set; } = """";
}

[MapFrom(typeof(User))]
[MapProperty(typeof(User), nameof(User.Email), nameof(EmailAddress))]
[MapTo(typeof(User))]
[MapProperty(typeof(User), nameof(User.Email), nameof(EmailAddress))]
public sealed class UserDto
{
    public string EmailAddress { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.EmailAddress = source.Email;", result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM001" && d.GetMessage().Contains("Email") && d.GetMessage().Contains("User"));
        Assert.DoesNotContain("destination.Email = ", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void CombinedMapFromAndMapTo_DoesNotFalsePositivelyReportGM011()
    {
        // [MapFrom(typeof(User))] and [MapTo(typeof(User))] on the same UserDto declare two
        // *different* pairs - (User, UserDto) and (UserDto, User) - so this must not collide
        // with the duplicate-declaration diagnostic added for exactly this kind of ambiguity.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

public sealed class User
{
    public int Id { get; set; }
}

[MapFrom(typeof(User))]
[MapTo(typeof(User))]
public sealed class UserDto
{
    public int Id { get; set; }
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "GM011");
        Assert.Contains("public static global::Sample.UserDto ToUserDto(this global::Sample.User source)", result.GeneratedSource);
        Assert.Contains("public static global::Sample.User ToUser(this global::Sample.UserDto source)", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void CombinedMapFromAndMapTo_SharedPropertyNameCondition_WrongSignatureDirectionReportsGM004()
    {
        // Both User and UserDto happen to have a property named "Body". A single
        // [MapCondition(typeof(User), nameof(Body), nameof(ShouldMap))] gets picked up by both
        // directions (matched purely by the shared otherSide type, same as [MapProperty]) -
        // but only one static ShouldMap overload was actually written, accepting User. The
        // User -> UserDto direction resolves fine; the UserDto -> User direction has no
        // ShouldMap(UserDto) or ShouldMap(UserDto, User?) overload to find, and reports GM004
        // instead of silently reusing the wrong one.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

public sealed class User
{
    public string Body { get; set; } = """";
}

[MapFrom(typeof(User))]
[MapTo(typeof(User))]
[MapCondition(typeof(User), nameof(Body), nameof(ShouldMap))]
public sealed class UserDto
{
    public string Body { get; set; } = """";

    public static bool ShouldMap(User source) => !string.IsNullOrEmpty(source.Body);
}
");

        Assert.NotNull(result.GeneratedSource);
        // User -> UserDto: resolves against the User-accepting overload, gated correctly.
        Assert.Contains("if (global::Sample.UserDto.ShouldMap(source))", result.GeneratedSource);
        // UserDto -> User: no ShouldMap(UserDto) overload exists.
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM004" && d.Severity == DiagnosticSeverity.Error);
        // "destination.Body" appears exactly once - the guarded assignment inside ToUserDto
        // above. The failed condition means ToUser's Body property was skipped entirely
        // (not emitted unconditioned, and not emitted with the wrong-direction guard either).
        Assert.Single(Regex.Matches(result.GeneratedSource!, "destination\\.Body"));
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void CombinedMapFromAndMapTo_PerDirectionMapUsing_OneDirectionMissingMethod_ReportsGM009ForThatDirectionOnly()
    {
        // Each direction has its own [MapUsing], correctly scoped by destinationProperty (same
        // mechanism verified for [MapProperty] above). The User -> UserDto converter exists;
        // the UserDto -> User one is misspelled, so only that direction reports GM009 - the
        // other direction's converter is unaffected.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

public sealed class User
{
    public string FirstName { get; set; } = """";
    public string LastName { get; set; } = """";
}

[MapFrom(typeof(User))]
[MapUsing(typeof(User), nameof(FullName), nameof(ComputeFullName))]
[MapTo(typeof(User))]
[MapUsing(typeof(User), nameof(User.FirstName), ""DoesNotExist"")]
public sealed class UserDto
{
    public string FullName { get; set; } = """";

    public static string ComputeFullName(User source) => $""{source.FirstName} {source.LastName}"";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.FullName = global::Sample.UserDto.ComputeFullName(source);", result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM009" && d.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain("destination.FirstName", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapIgnore_ScopedToSourceType_ExcludesOnlyThatSourceLeavesOthersMapped()
    {
        // [MapIgnore(typeof(UserV1))] only excludes Extra from the UserV1 -> UserDto mapping;
        // the UserV2 -> UserDto mapping (declared via a second, repeatable [MapFrom]) still
        // maps it normally, in both the imperative methods and the SQL projection.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

public sealed class UserV1
{
    public int Id { get; set; }
    public string Extra { get; set; } = """";
}

public sealed class UserV2
{
    public int Id { get; set; }
    public string Extra { get; set; } = """";
}

[MapFrom(typeof(UserV1))]
[MapFrom(typeof(UserV2))]
public sealed class UserDto
{
    public int Id { get; set; }

    [MapIgnore(typeof(UserV1))]
    public string Extra { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);

        var source = result.GeneratedSource!;
        var v1Start = source.IndexOf("ToUserDto(this global::Sample.UserV1", System.StringComparison.Ordinal);
        var v2Start = source.IndexOf("ToUserDto(this global::Sample.UserV2", System.StringComparison.Ordinal);
        Assert.True(v1Start >= 0 && v2Start > v1Start);

        var v1Section = source.Substring(v1Start, v2Start - v1Start);
        var v2Section = source.Substring(v2Start);

        Assert.DoesNotContain("Extra", v1Section);
        Assert.Contains("destination.Extra = source.Extra;", v2Section);
        Assert.Contains("Extra = source.Extra }", v2Section);

        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "GM001" && d.GetMessage().Contains("Extra"));
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapIgnore_Unscoped_StillExcludesFromEverySourceAlongsideMultipleMapFrom()
    {
        // A plain, parameterless [MapIgnore] must keep working exactly as before even now that
        // the attribute is repeatable/Type-scoped - it still excludes Extra from every mapping
        // into UserDto regardless of source.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

public sealed class UserV1
{
    public int Id { get; set; }
    public string Extra { get; set; } = """";
}

public sealed class UserV2
{
    public int Id { get; set; }
    public string Extra { get; set; } = """";
}

[MapFrom(typeof(UserV1))]
[MapFrom(typeof(UserV2))]
public sealed class UserDto
{
    public int Id { get; set; }

    [MapIgnore]
    public string Extra { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.DoesNotContain("Extra", result.GeneratedSource);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "GM001" && d.GetMessage().Contains("Extra"));
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapIgnore_ScopedToUnrelatedSourceType_DoesNotExcludeProperty()
    {
        // The typeof(...) argument names a type that is never actually a source for this
        // destination - it must not match anything, so the property maps normally instead of
        // being silently (and incorrectly) treated as ignored.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

public sealed class Unrelated
{
}

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
    public string Extra { get; set; } = """";
}

public sealed class UserDto
{
    public int Id { get; set; }

    [MapIgnore(typeof(Unrelated))]
    public string Extra { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.Extra = source.Extra;", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapIgnore_MatchingSourceCombinedWithMapCondition_ReportsGM012AndStaysExcluded()
    {
        // A [MapCondition] on a property that a same-source-scoped [MapIgnore] also excludes
        // can never run - the [MapIgnore] check's continue happens first every time, so the
        // condition method is dead code. GM012 flags this instead of silently discarding it.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

public sealed class User
{
    public string Body { get; set; } = """";
}

[MapFrom(typeof(User))]
[MapCondition(typeof(User), nameof(Body), nameof(ShouldMap))]
public sealed class UserDto
{
    [MapIgnore(typeof(User))]
    public string Body { get; set; } = """";

    public static bool ShouldMap(User source) => true;
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.DoesNotContain("Body", result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM012" && d.Severity == DiagnosticSeverity.Warning && d.GetMessage().Contains("Body"));
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapIgnore_ScopedToDifferentSourceThanMapCondition_DoesNotReportGM012AndConditionStillApplies()
    {
        // The [MapIgnore] here is scoped to UserV2, while [MapCondition] is scoped to UserV1 -
        // two independent, non-overlapping mappings into the same destination property. GM012
        // must not fire, and the condition must still gate the UserV1 -> UserDto mapping
        // normally while UserV2 -> UserDto stays unconditionally excluded.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

public sealed class UserV1
{
    public string Body { get; set; } = """";
}

public sealed class UserV2
{
    public string Body { get; set; } = """";
}

[MapFrom(typeof(UserV1))]
[MapCondition(typeof(UserV1), nameof(Body), nameof(ShouldMap))]
[MapFrom(typeof(UserV2))]
public sealed class UserDto
{
    [MapIgnore(typeof(UserV2))]
    public string Body { get; set; } = """";

    public static bool ShouldMap(UserV1 source) => true;
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "GM012");

        var source = result.GeneratedSource!;
        var v1Start = source.IndexOf("ToUserDto(this global::Sample.UserV1", System.StringComparison.Ordinal);
        var v2Start = source.IndexOf("ToUserDto(this global::Sample.UserV2", System.StringComparison.Ordinal);
        Assert.True(v1Start >= 0 && v2Start > v1Start);

        var v1Section = source.Substring(v1Start, v2Start - v1Start);
        var v2Section = source.Substring(v2Start);

        Assert.Contains("if (global::Sample.UserDto.ShouldMap(source))", v1Section);
        Assert.DoesNotContain("Body", v2Section);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void RequiredProperty_MappedFromMatchingSource_CompilesAndIsSetInTheInitializer()
    {
        // A bare `new Dest()` fails to compile (CS9035) whenever Dest has an unset 'required'
        // member, even if a later statement in the same or a different method assigns it - the
        // one-arg overload must set it directly in the object-initializer it constructs with.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}

public sealed class UserDto
{
    public int Id { get; set; }

    public required string Name { get; set; }
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("new global::Sample.UserDto { Name = source.Name }", result.GeneratedSource);
        // The two-arg overload (populate-into-existing-instance) still assigns it too - the
        // property is a regular mutable setter, so reassigning it there is legal and harmless.
        Assert.Contains("destination.Name = source.Name;", result.GeneratedSource);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "GM013");
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void RequiredProperty_LeftUnmapped_ReportsGM013()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
}

public sealed class UserDto
{
    public int Id { get; set; }

    public required string Name { get; set; }
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM013" && d.Severity == DiagnosticSeverity.Error && d.GetMessage().Contains("Name"));
    }

    [Fact]
    public void RequiredProperty_CombinedWithMapCondition_ReportsGM014AndGM013AndExcludesTheProperty()
    {
        // A required member must be set unconditionally within the object-creation expression -
        // there's no way to honor "maybe don't set it" for a required property, so the
        // combination is rejected (GM014) and the property is treated as genuinely unmapped
        // (GM013), rather than emitting code that fails to compile with no explanation.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapCondition(typeof(UserDto), nameof(UserDto.Name), nameof(ShouldMap))]
public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = """";

    public static bool ShouldMap(User source) => true;
}

public sealed class UserDto
{
    public int Id { get; set; }

    public required string Name { get; set; }
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.DoesNotContain("Name", result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM014" && d.Severity == DiagnosticSeverity.Warning && d.GetMessage().Contains("Name"));
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM013" && d.Severity == DiagnosticSeverity.Error && d.GetMessage().Contains("Name"));
    }

    [Fact]
    public void MapIgnore_MatchingSourceCombinedWithMapUsingAndMapDefaultAndMapProperty_ReportsGM012ForAll()
    {
        // GM012 isn't limited to [MapCondition] - any of the four per-property overrides
        // configured against a property a matching [MapIgnore] excludes is equally dead, since
        // none of them are ever consulted once MapIgnore's continue fires first.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapUsing(typeof(UserDto), nameof(UserDto.Nickname), nameof(ComputeNickname))]
[MapDefault(typeof(UserDto), nameof(UserDto.Nickname), ""Unknown"")]
[MapProperty(typeof(UserDto), nameof(FullName), nameof(UserDto.Nickname))]
public sealed class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = """";

    public static string ComputeNickname(User source) => source.FullName;
}

public sealed class UserDto
{
    public int Id { get; set; }

    [MapIgnore(typeof(User))]
    public string Nickname { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.DoesNotContain("Nickname", result.GeneratedSource);

        var gm012 = Assert.Single(result.GeneratorDiagnostics.Where(d => d.Id == "GM012"));
        Assert.Equal(DiagnosticSeverity.Warning, gm012.Severity);
        var message = gm012.GetMessage();
        Assert.Contains("[MapUsing]", message);
        Assert.Contains("[MapDefault]", message);
        Assert.Contains("[MapProperty]", message);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapIgnore_SourceTypeNeverMapped_ReportsGM015AndDoesNotExcludeTheProperty()
    {
        // [MapIgnore(typeof(TypoUser))] names a type that never actually maps into UserDto -
        // almost certainly a typo, or left behind after a rename. It must not silently do
        // nothing: report GM015, and since it doesn't match the real source (User), Extra is
        // still mapped normally from User.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

public sealed class TypoUser
{
}

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
    public string Extra { get; set; } = """";
}

public sealed class UserDto
{
    public int Id { get; set; }

    [MapIgnore(typeof(TypoUser))]
    public string Extra { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.Extra = source.Extra;", result.GeneratedSource);

        var gm015 = Assert.Single(result.GeneratorDiagnostics.Where(d => d.Id == "GM015"));
        Assert.Equal(DiagnosticSeverity.Warning, gm015.Severity);
        var message = gm015.GetMessage();
        Assert.Contains("TypoUser", message);
        Assert.Contains("Extra", message);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapIgnore_SourceTypeMatchesADeclaredMapping_DoesNotReportGM015()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
    public string Extra { get; set; } = """";
}

public sealed class UserDto
{
    public int Id { get; set; }

    [MapIgnore(typeof(User))]
    public string Extra { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "GM015");
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapIgnore_UnscopedAlongsideScoped_ReportsGM016()
    {
        // The unscoped [MapIgnore] already excludes every source; the scoped one next to it
        // adds nothing and is flagged as redundant configuration, not a real bug.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
}

public sealed class UserDto
{
    public int Id { get; set; }

    [MapIgnore]
    [MapIgnore(typeof(User))]
    public int ComputedOnly { get; set; }
}
");

        Assert.NotNull(result.GeneratedSource);

        var gm016 = Assert.Single(result.GeneratorDiagnostics.Where(d => d.Id == "GM016"));
        Assert.Equal(DiagnosticSeverity.Info, gm016.Severity);
        var message = gm016.GetMessage();
        Assert.Contains("ComputedOnly", message);
        Assert.Contains("unscoped", message);
        Assert.Contains("Sample.User", message);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapIgnore_SameSourceTypeTwice_ReportsGM016()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
    public string Extra { get; set; } = """";
}

public sealed class UserDto
{
    public int Id { get; set; }

    [MapIgnore(typeof(User))]
    [MapIgnore(typeof(User))]
    public string Extra { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);

        var gm016 = Assert.Single(result.GeneratorDiagnostics.Where(d => d.Id == "GM016"));
        Assert.Equal(DiagnosticSeverity.Info, gm016.Severity);
        var message = gm016.GetMessage();
        Assert.Contains("Extra", message);
        Assert.Contains("more than one", message);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapIgnore_ScopedToTwoDifferentRealSources_DoesNotReportGM016()
    {
        // The legitimate repeatable-MapIgnore usage pattern: one scoped attribute per source
        // that should actually be excluded, each naming a real, distinct source. Not redundant.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

public sealed class UserV1
{
    public int Id { get; set; }
    public string Extra { get; set; } = """";
}

public sealed class UserV2
{
    public int Id { get; set; }
    public string Extra { get; set; } = """";
}

public sealed class UserV3
{
    public int Id { get; set; }
    public string Extra { get; set; } = """";
}

[MapFrom(typeof(UserV1))]
[MapFrom(typeof(UserV2))]
[MapFrom(typeof(UserV3))]
public sealed class UserDto
{
    public int Id { get; set; }

    [MapIgnore(typeof(UserV1))]
    [MapIgnore(typeof(UserV2))]
    public string Extra { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "GM016");
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "GM015");
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void Projection_FieldHasNoInlineInitializer_AssignedInSuppressedStaticConstructor()
    {
        // A field-initializer-only static constructor is implicit and can't carry an attribute -
        // the field must be declared bare and assigned in an explicit static constructor instead,
        // so [UnconditionalSuppressMessage] can be attached to it.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
}

public sealed class UserDto
{
    public int Id { get; set; }
}
");

        Assert.NotNull(result.GeneratedSource);
        var source = result.GeneratedSource!;

        Assert.Contains(
            "public static readonly Expression<Func<global::Sample.User, global::Sample.UserDto>> UserToUserDtoProjection;",
            source);
        Assert.Contains(
            "[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(\"Trimming\", \"IL2026\"",
            source);
        Assert.Contains("static GeneratedMappings()", source);
        Assert.Contains(
            "UserToUserDtoProjection = source => new global::Sample.UserDto { Id = source.Id };",
            source);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void Projection_MultipleMappings_AllAssignedInOneSharedStaticConstructor()
    {
        // Every mapping's projection field assignment must land in the same single explicit
        // static constructor, not one per mapping - a type can only have one static constructor.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
}

[MapTo(typeof(PostDto))]
public sealed class Post
{
    public int Id { get; set; }
}

public sealed class UserDto
{
    public int Id { get; set; }
}

public sealed class PostDto
{
    public int Id { get; set; }
}
");

        Assert.NotNull(result.GeneratedSource);
        var source = result.GeneratedSource!;

        Assert.Single(Regex.Matches(source, "static GeneratedMappings\\(\\)"));
        Assert.Contains("UserToUserDtoProjection = source => new global::Sample.UserDto { Id = source.Id };", source);
        Assert.Contains("PostToPostDtoProjection = source => new global::Sample.PostDto { Id = source.Id };", source);
        AssertNoCompileErrors(result);
    }

    private static void AssertNoCompileErrors(GeneratorTestResult result)
    {
        var errors = result.CompilationDiagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.True(errors.Count == 0, string.Join("\n", errors.Select(e => e.ToString())));
    }
}

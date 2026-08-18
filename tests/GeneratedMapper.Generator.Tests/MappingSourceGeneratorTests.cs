using System.Linq;
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
        Assert.Contains("Expression<Func<global::Sample.Basket, global::Sample.BasketDto>> ToBasketDtoProjection", result.GeneratedSource);
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
    public void InitOnlyDestination_WithoutParameterlessConstructor_ReportsGM006AndSkipsMapping()
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
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM006" && d.Severity == DiagnosticSeverity.Warning);
        Assert.DoesNotContain("ToUserDto", result.GeneratedSource);
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
        Assert.Contains("Expression<Func<global::Sample.User, global::Sample.UserDto>> ToUserDtoProjection", result.GeneratedSource);
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

    private static void AssertNoCompileErrors(GeneratorTestResult result)
    {
        var errors = result.CompilationDiagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.True(errors.Count == 0, string.Join("\n", errors.Select(e => e.ToString())));
    }
}

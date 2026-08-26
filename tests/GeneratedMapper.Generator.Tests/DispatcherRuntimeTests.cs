using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace GeneratedMapper.Generator.Tests;

/// <summary>
/// Loads the actually-emitted assembly and calls the generated dispatcher (Map&lt;T&gt;) and
/// GeneratedMapperService through reflection, instead of only asserting on generated source
/// text — this verifies the FrozenDictionary-based dispatch, the null/unmapped-type error
/// paths, and that IMapper still works, at runtime.
/// </summary>
public class DispatcherRuntimeTests
{
    private const string Source = @"
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
";

    [Fact]
    public void MapGeneric_RoutesToCorrectMapping()
    {
        var result = GeneratorTestHelper.Run(Source);
        Assert.NotNull(result.Assembly);

        var (userType, userDtoType) = GetSampleTypes(result.Assembly!);
        var user = CreateUser(userType, id: 42, name: "Ada");

        var mapMethod = GetMappingsType(result.Assembly!)
            .GetMethod("Map", genericParameterCount: 1, new[] { typeof(object) })!
            .MakeGenericMethod(userDtoType);

        var dto = mapMethod.Invoke(null, new[] { user });

        Assert.Equal(42, userDtoType.GetProperty("Id")!.GetValue(dto));
        Assert.Equal("Ada", userDtoType.GetProperty("Name")!.GetValue(dto));
    }

    [Fact]
    public void MapGeneric_NullSource_ThrowsArgumentNullException()
    {
        var result = GeneratorTestHelper.Run(Source);
        Assert.NotNull(result.Assembly);

        var (_, userDtoType) = GetSampleTypes(result.Assembly!);

        var mapMethod = GetMappingsType(result.Assembly!)
            .GetMethod("Map", genericParameterCount: 1, new[] { typeof(object) })!
            .MakeGenericMethod(userDtoType);

        var ex = Assert.Throws<TargetInvocationException>(() => mapMethod.Invoke(null, new object?[] { null }));
        Assert.IsType<ArgumentNullException>(ex.InnerException);
    }

    [Fact]
    public void MapGeneric_UnmappedSourceType_ThrowsInvalidOperationException()
    {
        var result = GeneratorTestHelper.Run(Source);
        Assert.NotNull(result.Assembly);

        var (_, userDtoType) = GetSampleTypes(result.Assembly!);

        var mapMethod = GetMappingsType(result.Assembly!)
            .GetMethod("Map", genericParameterCount: 1, new[] { typeof(object) })!
            .MakeGenericMethod(userDtoType);

        var ex = Assert.Throws<TargetInvocationException>(() => mapMethod.Invoke(null, new object[] { "not a mapped source type" }));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void MapInto_PopulatesAndReturnsTheProvidedDestinationInstance()
    {
        var result = GeneratorTestHelper.Run(Source);
        Assert.NotNull(result.Assembly);

        var (userType, userDtoType) = GetSampleTypes(result.Assembly!);
        var user = CreateUser(userType, id: 7, name: "Grace");
        var existingDto = Activator.CreateInstance(userDtoType)!;

        var mapIntoMethod = GetMappingsType(result.Assembly!)
            .GetMethod(
                "Map",
                genericParameterCount: 2,
                new[] { Type.MakeGenericMethodParameter(0), Type.MakeGenericMethodParameter(1) })!
            .MakeGenericMethod(userType, userDtoType);

        var returned = mapIntoMethod.Invoke(null, new[] { user, existingDto });

        Assert.Same(existingDto, returned);
        Assert.Equal(7, userDtoType.GetProperty("Id")!.GetValue(existingDto));
        Assert.Equal("Grace", userDtoType.GetProperty("Name")!.GetValue(existingDto));
    }

    [Fact]
    public void GeneratedMapperService_ImplementsRealIMapper_AndDispatchesCorrectly()
    {
        var result = GeneratorTestHelper.Run(Source);
        Assert.NotNull(result.Assembly);

        var (userType, userDtoType) = GetSampleTypes(result.Assembly!);
        var user = CreateUser(userType, id: 3, name: "Katherine");

        var serviceType = result.Assembly!.GetType("GeneratedMapper.GeneratedMapperService")!;
        var serviceInstance = Activator.CreateInstance(serviceType)!;

        // GeneratedMapperService implements the SAME GeneratedMapper.IMapper referenced by
        // this test project (both point at the same Abstractions assembly) — no reflection
        // needed to prove the interface relationship itself.
        var mapper = Assert.IsAssignableFrom<IMapper>(serviceInstance);

        var mapMethod = typeof(IMapper)
            .GetMethod("Map", genericParameterCount: 1, new[] { typeof(object) })!
            .MakeGenericMethod(userDtoType);

        var dto = mapMethod.Invoke(mapper, new[] { user });

        Assert.Equal(3, userDtoType.GetProperty("Id")!.GetValue(dto));
        Assert.Equal("Katherine", userDtoType.GetProperty("Name")!.GetValue(dto));
    }

    [Fact]
    public void MaxDepth_CyclicRuntimeGraph_TruncatesInsteadOfStackOverflowing()
    {
        const string source = @"
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
";

        var result = GeneratorTestHelper.Run(source);
        Assert.NotNull(result.Assembly);

        var categoryType = result.Assembly!.GetType("Sample.Category")!;
        var a = Activator.CreateInstance(categoryType)!;
        var b = Activator.CreateInstance(categoryType)!;
        categoryType.GetProperty("Parent")!.SetValue(a, b);
        categoryType.GetProperty("Parent")!.SetValue(b, a); // genuine reference cycle

        var toDto = GetMappingsType(result.Assembly!).GetMethod("ToCategoryDto", new[] { categoryType })!;
        var dto = toDto.Invoke(null, new[] { a });

        var dtoType = result.Assembly!.GetType("Sample.CategoryDto")!;
        object? current = dto;
        var hops = 0;

        // A genuinely cyclic runtime graph would stack-overflow without the MaxDepth guard.
        // With MaxDepth = 3, the Parent chain must terminate (hit a null Parent) instead of
        // reflecting the source cycle forever.
        while (current is not null && hops < 10)
        {
            current = dtoType.GetProperty("Parent")!.GetValue(current);
            hops++;
        }

        Assert.Equal(4, hops);
    }

    [Fact]
    public void InitOnlyRecordDestination_OneArgMapper_ProducesCorrectValues()
    {
        const string source = @"
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
";

        var result = GeneratorTestHelper.Run(source);
        Assert.NotNull(result.Assembly);

        var (userType, userDtoType) = GetSampleTypes(result.Assembly!);
        var user = CreateUser(userType, id: 11, name: "Ada");

        var toDto = GetMappingsType(result.Assembly!).GetMethod("ToUserDto", new[] { userType })!;
        var dto = toDto.Invoke(null, new[] { user });

        Assert.Equal(11, userDtoType.GetProperty("Id")!.GetValue(dto));
        Assert.Equal("Ada", userDtoType.GetProperty("Name")!.GetValue(dto));
    }

    [Fact]
    public void InitOnlyRecordDestination_MapGeneric_StillDispatchesCorrectly()
    {
        const string source = @"
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
";

        var result = GeneratorTestHelper.Run(source);
        Assert.NotNull(result.Assembly);

        var (userType, userDtoType) = GetSampleTypes(result.Assembly!);
        var user = CreateUser(userType, id: 5, name: "Grace");

        var mapMethod = GetMappingsType(result.Assembly!)
            .GetMethod("Map", genericParameterCount: 1, new[] { typeof(object) })!
            .MakeGenericMethod(userDtoType);

        var dto = mapMethod.Invoke(null, new[] { user });

        Assert.Equal(5, userDtoType.GetProperty("Id")!.GetValue(dto));
        Assert.Equal("Grace", userDtoType.GetProperty("Name")!.GetValue(dto));
    }

    [Fact]
    public void InitOnlyRecordDestination_MapInto_ThrowsBecauseTwoArgOverloadWasOmitted()
    {
        const string source = @"
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
";

        var result = GeneratorTestHelper.Run(source);
        Assert.NotNull(result.Assembly);

        var (userType, userDtoType) = GetSampleTypes(result.Assembly!);
        var user = CreateUser(userType, id: 1, name: "X");
        var existingDto = Activator.CreateInstance(userDtoType)!;

        var mapIntoMethod = GetMappingsType(result.Assembly!)
            .GetMethod(
                "Map",
                genericParameterCount: 2,
                new[] { Type.MakeGenericMethodParameter(0), Type.MakeGenericMethodParameter(1) })!
            .MakeGenericMethod(userType, userDtoType);

        // No _mapInto entry exists for this pair (GM008 — the destination has init-only
        // properties), so this must fail the same way an entirely unmapped pair would.
        var ex = Assert.Throws<TargetInvocationException>(() => mapIntoMethod.Invoke(null, new[] { user, existingDto }));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void HashSetDestination_RuntimeProducesPopulatedHashSet()
    {
        const string source = @"
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
";

        var result = GeneratorTestHelper.Run(source);
        Assert.NotNull(result.Assembly);

        var basketType = result.Assembly!.GetType("Sample.Basket")!;
        var itemType = result.Assembly!.GetType("Sample.Item")!;
        var basket = Activator.CreateInstance(basketType)!;

        var itemsList = (System.Collections.IList)basketType.GetProperty("Items")!.GetValue(basket)!;
        var item = Activator.CreateInstance(itemType)!;
        itemType.GetProperty("Id")!.SetValue(item, 7);
        itemsList.Add(item);

        var toDto = GetMappingsType(result.Assembly!).GetMethod("ToBasketDto", new[] { basketType })!;
        var dto = toDto.Invoke(null, new[] { basket });

        var basketDtoType = result.Assembly!.GetType("Sample.BasketDto")!;
        var itemsValue = basketDtoType.GetProperty("Items")!.GetValue(dto);

        var itemDtoType = result.Assembly!.GetType("Sample.ItemDto")!;
        var expectedHashSetType = typeof(HashSet<>).MakeGenericType(itemDtoType);
        Assert.IsType(expectedHashSetType, itemsValue);

        var count = (int)expectedHashSetType.GetProperty("Count")!.GetValue(itemsValue)!;
        Assert.Equal(1, count);
    }

    [Fact]
    public void ArrayDestination_RuntimeProducesActualArray()
    {
        const string source = @"
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
";

        var result = GeneratorTestHelper.Run(source);
        Assert.NotNull(result.Assembly);

        var basketType = result.Assembly!.GetType("Sample.Basket")!;
        var basket = Activator.CreateInstance(basketType)!;
        var tagsList = (System.Collections.IList)basketType.GetProperty("Tags")!.GetValue(basket)!;
        tagsList.Add(1);
        tagsList.Add(2);
        tagsList.Add(3);

        var toDto = GetMappingsType(result.Assembly!).GetMethod("ToBasketDto", new[] { basketType })!;
        var dto = toDto.Invoke(null, new[] { basket });

        var basketDtoType = result.Assembly!.GetType("Sample.BasketDto")!;
        var tagsValue = basketDtoType.GetProperty("Tags")!.GetValue(dto);

        var array = Assert.IsType<int[]>(tagsValue);
        Assert.Equal(new[] { 1, 2, 3 }, array);
    }

    [Fact]
    public void MaxDepth_CyclicEnumerableRuntimeGraph_TruncatesInsteadOfStackOverflowing()
    {
        const string source = @"
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
";

        var result = GeneratorTestHelper.Run(source);
        Assert.NotNull(result.Assembly);

        var categoryType = result.Assembly!.GetType("Sample.Category")!;
        var a = Activator.CreateInstance(categoryType)!;
        var b = Activator.CreateInstance(categoryType)!;

        var aChildren = (System.Collections.IList)categoryType.GetProperty("Children")!.GetValue(a)!;
        var bChildren = (System.Collections.IList)categoryType.GetProperty("Children")!.GetValue(b)!;
        aChildren.Add(b);
        bChildren.Add(a); // genuine reference cycle through the enumerable property

        var toDto = GetMappingsType(result.Assembly!).GetMethod("ToCategoryDto", new[] { categoryType })!;
        var dto = toDto.Invoke(null, new[] { a });

        var dtoType = result.Assembly!.GetType("Sample.CategoryDto")!;

        // Always follow the first (only) child. With MaxDepth = 2 this must terminate at an
        // empty Children list instead of reflecting the source cycle forever.
        object? current = dto;
        var hops = 0;

        while (current is not null && hops < 10)
        {
            var children = (System.Collections.IList)dtoType.GetProperty("Children")!.GetValue(current)!;
            current = children.Count > 0 ? children[0] : null;
            hops++;
        }

        Assert.Equal(3, hops);
    }

    [Fact]
    public void MapUsing_RuntimeComputesConvertedValue()
    {
        const string source = @"
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
";

        var result = GeneratorTestHelper.Run(source);
        Assert.NotNull(result.Assembly);

        var userType = result.Assembly!.GetType("Sample.User")!;
        var user = Activator.CreateInstance(userType)!;
        userType.GetProperty("FirstName")!.SetValue(user, "Ada");
        userType.GetProperty("LastName")!.SetValue(user, "Lovelace");

        var toDto = GetMappingsType(result.Assembly!).GetMethod("ToUserDto", new[] { userType })!;
        var dto = toDto.Invoke(null, new[] { user });

        var userDtoType = result.Assembly!.GetType("Sample.UserDto")!;
        Assert.Equal("Ada Lovelace", userDtoType.GetProperty("FullName")!.GetValue(dto));
    }

    [Fact]
    public void MapUsing_ProjectionExpressionComputesConvertedValueWhenCompiledAndInvoked()
    {
        const string source = @"
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
";

        var result = GeneratorTestHelper.Run(source);
        Assert.NotNull(result.Assembly);

        var projectionField = GetMappingsType(result.Assembly!).GetField("UserToUserDtoProjection")!;
        var projectionExpression = (System.Linq.Expressions.LambdaExpression)projectionField.GetValue(null)!;
        var compiled = projectionExpression.Compile();

        var userType = result.Assembly!.GetType("Sample.User")!;
        var user = Activator.CreateInstance(userType)!;
        userType.GetProperty("FirstName")!.SetValue(user, "Grace");
        userType.GetProperty("LastName")!.SetValue(user, "Hopper");

        var dto = compiled.DynamicInvoke(user);

        var userDtoType = result.Assembly!.GetType("Sample.UserDto")!;
        Assert.Equal("Grace Hopper", userDtoType.GetProperty("FullName")!.GetValue(dto));
    }

    [Fact]
    public void MapDefault_RuntimeSubstitutesNullValue()
    {
        const string source = @"
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
";

        var result = GeneratorTestHelper.Run(source);
        Assert.NotNull(result.Assembly);

        var userType = result.Assembly!.GetType("Sample.User")!;
        var user = Activator.CreateInstance(userType)!;
        userType.GetProperty("Name")!.SetValue(user, null);

        var toDto = GetMappingsType(result.Assembly!).GetMethod("ToUserDto", new[] { userType })!;
        var dto = toDto.Invoke(null, new[] { user });

        var userDtoType = result.Assembly!.GetType("Sample.UserDto")!;
        Assert.Equal("Unknown", userDtoType.GetProperty("Name")!.GetValue(dto));
    }

    [Fact]
    public void MapDefault_RuntimeDoesNotOverrideANonNullValue()
    {
        const string source = @"
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
";

        var result = GeneratorTestHelper.Run(source);
        Assert.NotNull(result.Assembly);

        var userType = result.Assembly!.GetType("Sample.User")!;
        var user = Activator.CreateInstance(userType)!;
        userType.GetProperty("Name")!.SetValue(user, "Ada");

        var toDto = GetMappingsType(result.Assembly!).GetMethod("ToUserDto", new[] { userType })!;
        var dto = toDto.Invoke(null, new[] { user });

        var userDtoType = result.Assembly!.GetType("Sample.UserDto")!;
        Assert.Equal("Ada", userDtoType.GetProperty("Name")!.GetValue(dto));
    }

    [Fact]
    public void MapDefault_ProjectionExpressionAppliesCoalesceWhenCompiledAndInvoked()
    {
        const string source = @"
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
";

        var result = GeneratorTestHelper.Run(source);
        Assert.NotNull(result.Assembly);

        var projectionField = GetMappingsType(result.Assembly!).GetField("UserToUserDtoProjection")!;
        var projectionExpression = (System.Linq.Expressions.LambdaExpression)projectionField.GetValue(null)!;
        var compiled = projectionExpression.Compile();

        var userType = result.Assembly!.GetType("Sample.User")!;
        var user = Activator.CreateInstance(userType)!;
        userType.GetProperty("Name")!.SetValue(user, null);

        var dto = compiled.DynamicInvoke(user);

        var userDtoType = result.Assembly!.GetType("Sample.UserDto")!;
        Assert.Equal("Unknown", userDtoType.GetProperty("Name")!.GetValue(dto));
    }

    [Fact]
    public void MapDefault_RuntimeAppliesToAConvertedProperty()
    {
        const string source = @"
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
";

        var result = GeneratorTestHelper.Run(source);
        Assert.NotNull(result.Assembly);

        var userType = result.Assembly!.GetType("Sample.User")!;
        var user = Activator.CreateInstance(userType)!;
        userType.GetProperty("FirstName")!.SetValue(user, null);

        var toDto = GetMappingsType(result.Assembly!).GetMethod("ToUserDto", new[] { userType })!;
        var dto = toDto.Invoke(null, new[] { user });

        var userDtoType = result.Assembly!.GetType("Sample.UserDto")!;
        Assert.Equal("N/A", userDtoType.GetProperty("FullName")!.GetValue(dto));
    }

    [Fact]
    public void MapDefault_RuntimeCombinedWithMapCondition_AppliesDefaultOnlyWhenConditionPasses()
    {
        const string source = @"
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
    public string Name { get; set; } = ""untouched"";
}
";

        var result = GeneratorTestHelper.Run(source);
        Assert.NotNull(result.Assembly);

        var userType = result.Assembly!.GetType("Sample.User")!;
        var userDtoType = result.Assembly!.GetType("Sample.UserDto")!;
        var toDto = GetMappingsType(result.Assembly!).GetMethod("ToUserDto", new[] { userType })!;

        var activeUser = Activator.CreateInstance(userType)!;
        userType.GetProperty("IsActive")!.SetValue(activeUser, true);
        userType.GetProperty("Name")!.SetValue(activeUser, null);
        var activeDto = toDto.Invoke(null, new[] { activeUser });
        Assert.Equal("Unknown", userDtoType.GetProperty("Name")!.GetValue(activeDto));

        var inactiveUser = Activator.CreateInstance(userType)!;
        userType.GetProperty("IsActive")!.SetValue(inactiveUser, false);
        userType.GetProperty("Name")!.SetValue(inactiveUser, null);
        var inactiveDto = toDto.Invoke(null, new[] { inactiveUser });
        Assert.Equal("untouched", userDtoType.GetProperty("Name")!.GetValue(inactiveDto));
    }

    [Fact]
    public void MapDefault_RuntimeSupportsNullableValueTypeWithNumericDefault()
    {
        const string source = @"
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
";

        var result = GeneratorTestHelper.Run(source);
        Assert.NotNull(result.Assembly);

        var userType = result.Assembly!.GetType("Sample.User")!;
        var userDtoType = result.Assembly!.GetType("Sample.UserDto")!;
        var toDto = GetMappingsType(result.Assembly!).GetMethod("ToUserDto", new[] { userType })!;

        var userWithNullAge = Activator.CreateInstance(userType)!;
        userType.GetProperty("Age")!.SetValue(userWithNullAge, null);
        var dtoWithDefault = toDto.Invoke(null, new[] { userWithNullAge });
        Assert.Equal(18, userDtoType.GetProperty("Age")!.GetValue(dtoWithDefault));

        var userWithAge = Activator.CreateInstance(userType)!;
        userType.GetProperty("Age")!.SetValue(userWithAge, 25);
        var dtoWithAge = toDto.Invoke(null, new[] { userWithAge });
        Assert.Equal(25, userDtoType.GetProperty("Age")!.GetValue(dtoWithAge));
    }

    [Fact]
    public void MapDefault_RuntimeHandlesStringDefaultContainingQuotes()
    {
        // The [MapDefault] argument in the target source needs to be a C# string literal whose
        // *value* itself contains a double-quote (`Say "hi"`) - built via Replace on a
        // placeholder rather than hand-escaped inside this file's own verbatim string, since
        // nesting C# escaping two levels deep is exactly the kind of thing that's easy to get
        // subtly wrong. Proves SymbolDisplay.FormatPrimitive re-escapes the value correctly on
        // the way back out into the generated mapping code, not just that a plain word like
        // "Unknown" (no embedded quotes) works.
        var quotedDefaultLiteral = "\"Say \\\"hi\\\"\""; // literal C# source text: "Say \"hi\""

        var source = @"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapDefault(typeof(UserDto), nameof(UserDto.Name), __DEFAULT__)]
public sealed class User
{
    public string? Name { get; set; }
}

public sealed class UserDto
{
    public string Name { get; set; } = """";
}
".Replace("__DEFAULT__", quotedDefaultLiteral);

        var result = GeneratorTestHelper.Run(source);
        Assert.NotNull(result.Assembly);

        var userType = result.Assembly!.GetType("Sample.User")!;
        var user = Activator.CreateInstance(userType)!;
        userType.GetProperty("Name")!.SetValue(user, null);

        var toDto = GetMappingsType(result.Assembly!).GetMethod("ToUserDto", new[] { userType })!;
        var dto = toDto.Invoke(null, new[] { user });

        var userDtoType = result.Assembly!.GetType("Sample.UserDto")!;
        Assert.Equal("Say \"hi\"", userDtoType.GetProperty("Name")!.GetValue(dto));
    }

    [Fact]
    public void Flattening_RuntimeProducesCorrectValueThroughNestedChain()
    {
        // F9: source-text assertions elsewhere prove the right chain is *emitted*; this compiles
        // and actually invokes it, proving `source.HomeAddress.City` reads the real runtime
        // value through a real nested object, not just a string that happens to look right.
        const string source = @"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public Address HomeAddress { get; set; } = new();
}

public sealed class Address
{
    public string City { get; set; } = """";
}

public sealed class UserDto
{
    public string HomeAddressCity { get; set; } = """";
}
";

        var result = GeneratorTestHelper.Run(source);
        Assert.NotNull(result.Assembly);

        var userType = result.Assembly!.GetType("Sample.User")!;
        var addressType = result.Assembly!.GetType("Sample.Address")!;
        var user = Activator.CreateInstance(userType)!;
        var address = Activator.CreateInstance(addressType)!;
        addressType.GetProperty("City")!.SetValue(address, "Paris");
        userType.GetProperty("HomeAddress")!.SetValue(user, address);

        var toDto = GetMappingsType(result.Assembly!).GetMethod("ToUserDto", new[] { userType })!;
        var dto = toDto.Invoke(null, new[] { user });

        var userDtoType = result.Assembly!.GetType("Sample.UserDto")!;
        Assert.Equal("Paris", userDtoType.GetProperty("HomeAddressCity")!.GetValue(dto));
    }

    [Fact]
    public void Flattening_ProjectionExpressionReadsRealValueWhenCompiledAndInvoked()
    {
        const string source = @"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public Address HomeAddress { get; set; } = new();
}

public sealed class Address
{
    public string City { get; set; } = """";
}

public sealed class UserDto
{
    public string HomeAddressCity { get; set; } = """";
}
";

        var result = GeneratorTestHelper.Run(source);
        Assert.NotNull(result.Assembly);

        var projectionField = GetMappingsType(result.Assembly!).GetField("UserToUserDtoProjection")!;
        var projectionExpression = (System.Linq.Expressions.LambdaExpression)projectionField.GetValue(null)!;
        var compiled = projectionExpression.Compile();

        var userType = result.Assembly!.GetType("Sample.User")!;
        var addressType = result.Assembly!.GetType("Sample.Address")!;
        var user = Activator.CreateInstance(userType)!;
        var address = Activator.CreateInstance(addressType)!;
        addressType.GetProperty("City")!.SetValue(address, "Berlin");
        userType.GetProperty("HomeAddress")!.SetValue(user, address);

        var dto = compiled.DynamicInvoke(user);

        var userDtoType = result.Assembly!.GetType("Sample.UserDto")!;
        Assert.Equal("Berlin", userDtoType.GetProperty("HomeAddressCity")!.GetValue(dto));
    }

    [Fact]
    public void Flattening_RuntimeCombinedWithMapDefault_SubstitutesWhenLeafIsNull()
    {
        const string source = @"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapDefault(typeof(UserDto), nameof(UserDto.HomeAddressCity), ""Unknown"")]
public sealed class User
{
    public Address HomeAddress { get; set; } = new();
}

public sealed class Address
{
    public string? City { get; set; }
}

public sealed class UserDto
{
    public string HomeAddressCity { get; set; } = """";
}
";

        var result = GeneratorTestHelper.Run(source);
        Assert.NotNull(result.Assembly);

        var userType = result.Assembly!.GetType("Sample.User")!;
        var addressType = result.Assembly!.GetType("Sample.Address")!;
        var user = Activator.CreateInstance(userType)!;
        var address = Activator.CreateInstance(addressType)!;
        addressType.GetProperty("City")!.SetValue(address, null);
        userType.GetProperty("HomeAddress")!.SetValue(user, address);

        var toDto = GetMappingsType(result.Assembly!).GetMethod("ToUserDto", new[] { userType })!;
        var dto = toDto.Invoke(null, new[] { user });

        var userDtoType = result.Assembly!.GetType("Sample.UserDto")!;
        Assert.Equal("Unknown", userDtoType.GetProperty("HomeAddressCity")!.GetValue(dto));
    }

    private static Type GetMappingsType(Assembly assembly)
        => assembly.GetType("GeneratedMapper.GeneratedMappings")!;

    private static (Type UserType, Type UserDtoType) GetSampleTypes(Assembly assembly)
        => (assembly.GetType("Sample.User")!, assembly.GetType("Sample.UserDto")!);

    private static object CreateUser(Type userType, int id, string name)
    {
        var user = Activator.CreateInstance(userType)!;
        userType.GetProperty("Id")!.SetValue(user, id);
        userType.GetProperty("Name")!.SetValue(user, name);
        return user;
    }
}

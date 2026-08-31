namespace AnvilMap.Generator.Tests;

// Scope boundaries and runtime correctness for presized-loop collection materialization, beyond
// MappingSourceGeneratorTests.cs's basic eligible-case coverage.
public class EnumerableLoopMaterializationTests
{
    [Fact]
    public void SameElementType_DifferentContainer_StillUsesDirectMaterializeNoLoop()
    {
        var result = GeneratorTestHelper.Run(@"
using System.Collections.Generic;
using AnvilMap;

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
        Assert.DoesNotContain("for (var i = 0; i <", result.GeneratedSource);
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void HashSetDestination_StillUsesSelectToHashSet()
    {
        var result = GeneratorTestHelper.Run(@"
using System.Collections.Generic;
using AnvilMap;

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
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void ImmutableArrayDestination_StillUsesOldPath()
    {
        var result = GeneratorTestHelper.Run(@"
using System.Collections.Generic;
using System.Collections.Immutable;
using AnvilMap;

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
    public ImmutableArray<ItemDto> Items { get; set; } = ImmutableArray<ItemDto>.Empty;
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("source.Items.Select(x => x.ToItemDto()).ToImmutableArray()", result.GeneratedSource);
        Assert.DoesNotContain("for (var i = 0; i <", result.GeneratedSource);
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void SelfRecursiveCollection_WithMaxDepth_StillUsesRecursiveSelectPath()
    {
        var result = GeneratorTestHelper.Run(@"
using System.Collections.Generic;
using AnvilMap;

namespace Sample;

[MapTo(typeof(CategoryDto), MaxDepth = 3)]
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
        Assert.Contains("source.Children.Select(x => x.ToCategoryDto(new global::Sample.CategoryDto(), depth + 1)).ToList()", result.GeneratedSource);
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void MapCondition_GuardsTheWholeLoopBlock()
    {
        var result = GeneratorTestHelper.Run(@"
using System.Collections.Generic;
using AnvilMap;

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
[MapCondition(typeof(BasketDto), nameof(BasketDto.Items), nameof(ShouldMapItems))]
public sealed class Basket
{
    public List<Item> Items { get; set; } = new();
    public bool IsLocked { get; set; }

    public static bool ShouldMapItems(Basket source) => !source.IsLocked;
}

public sealed class BasketDto
{
    public List<ItemDto> Items { get; set; } = new();
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("if (global::Sample.Basket.ShouldMapItems(source))", result.GeneratedSource);
        Assert.Contains("destination.Items = new List<global::Sample.ItemDto>(source.Items.Count);", result.GeneratedSource);
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void RuntimeMapping_ListAndArrayDestinations_ProduceCorrectValuesInOrder()
    {
        var result = GeneratorTestHelper.Run(@"
using System.Collections.Generic;
using AnvilMap;

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
    public List<Item> ListItems { get; set; } = new();
    public Item[] ArrayItems { get; set; } = System.Array.Empty<Item>();
}

public sealed class BasketDto
{
    public List<ItemDto> ListItems { get; set; } = new();
    public ItemDto[] ArrayItems { get; set; } = System.Array.Empty<ItemDto>();
}
");

        Assert.NotNull(result.Assembly);

        var itemType = result.Assembly!.GetType("Sample.Item")!;
        var basketType = result.Assembly!.GetType("Sample.Basket")!;
        var itemDtoType = result.Assembly!.GetType("Sample.ItemDto")!;

        var items = new[] { 10, 20, 30 }.Select(id =>
        {
            var item = System.Activator.CreateInstance(itemType)!;
            itemType.GetProperty("Id")!.SetValue(item, id);
            return item;
        }).ToArray();

        var itemList = (System.Collections.IList)System.Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType))!;
        foreach (var item in items)
        {
            itemList.Add(item);
        }

        var itemArray = System.Array.CreateInstance(itemType, items.Length);
        items.CopyTo(itemArray, 0);

        var basket = System.Activator.CreateInstance(basketType)!;
        basketType.GetProperty("ListItems")!.SetValue(basket, itemList);
        basketType.GetProperty("ArrayItems")!.SetValue(basket, itemArray);

        var toDto = result.Assembly!.GetType("AnvilMap.GeneratedMappings")!.GetMethod("ToBasketDto", new[] { basketType });
        var dto = toDto!.Invoke(null, new[] { basket })!;

        var basketDtoType = dto.GetType();
        var listItems = (System.Collections.IList)basketDtoType.GetProperty("ListItems")!.GetValue(dto)!;
        var arrayItems = (System.Collections.IList)basketDtoType.GetProperty("ArrayItems")!.GetValue(dto)!;

        Assert.Equal(3, listItems.Count);
        Assert.Equal(3, arrayItems.Count);

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal((int)itemType.GetProperty("Id")!.GetValue(items[i])!, (int)itemDtoType.GetProperty("Id")!.GetValue(listItems[i])!);
            Assert.Equal((int)itemType.GetProperty("Id")!.GetValue(items[i])!, (int)itemDtoType.GetProperty("Id")!.GetValue(arrayItems[i])!);
        }
    }
}

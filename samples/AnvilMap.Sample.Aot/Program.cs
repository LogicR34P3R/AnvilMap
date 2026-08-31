using AnvilMap;
using AnvilMap.Sample.Aot.Models;
using AnvilMap.Sample.Aot.ViewModels;

// Native AOT verification target - see README.md's "Native AOT" section. Asserts results
// rather than just printing them, to catch a silently-wrong trim rather than just a nonzero
// exit code.

var external = new Order
{
    Id = 1,
    Reference = "ORD-EXTERNAL",
    IsInternal = false,
    InternalNotes = "Should never appear on the DTO below.",
    Status = OrderStatus.Shipped,
    Customer = new Customer { Name = "Ada Lovelace", Email = "ada@example.com" },
    LineItems =
    {
        new LineItem { ProductName = "Widget", Quantity = 2, UnitPrice = 9.99m },
        new LineItem { ProductName = "Gadget", Quantity = 1, UnitPrice = 24.50m },
    },
    Tags = { "priority", "gift" },
    RecentNotes = { "Packed", "Shipped" },
};

var internalOrder = new Order
{
    Id = 2,
    Reference = "ORD-INTERNAL",
    IsInternal = true,
    InternalNotes = "Restock before Friday.",
    Status = OrderStatus.Pending,
    PromoCode = "SAVE10",
    Customer = new Customer { Name = "Grace Hopper", Email = "grace@example.com" },
    LineItems = { new LineItem { ProductName = "Cable", Quantity = 5, UnitPrice = 3.00m } },
    Tags = { "internal" },
    RecentNotes = { "Awaiting restock" },
};

Verify("extension method", external.ToOrderDto(), external);
Verify("extension method", internalOrder.ToOrderDto(), internalOrder);

IMapper mapper = new AnvilMapService();
Verify("IMapper", mapper.Map<Order, OrderDto>(external), external);
Verify("IMapper", mapper.Map<Order, OrderDto>(internalOrder), internalOrder);

// Compiling and invoking the projection field directly proves, under real Native AOT, that
// expression-tree compilation works and the property accessors Expression.Bind reflects over
// (the IL2026 trim warning) weren't trimmed away. Total's [MapUsing] uses InlineInProjection,
// so this also proves a spliced converter body (a LINQ .Sum() lambda) survives AOT trimming.
// AM005/AM022/AM023 exclude InternalNotes/Status/Tags/RecentNotes, checked separately below.
var compiled = GeneratedMappings.OrderToOrderDtoProjection.Compile();
var compiledExternal = compiled(external);
var compiledInternal = compiled(internalOrder);
Verify("compiled projection", compiledExternal, external, checkConditionedProperty: false, checkEnumToString: false, checkCollectionShapes: false);
Verify("compiled projection", compiledInternal, internalOrder, checkConditionedProperty: false, checkEnumToString: false, checkCollectionShapes: false);

Console.WriteLine();

var root = new Category
{
    Name = "Root",
    Children =
    {
        new Category
        {
            Name = "Level 1",
            Children = { new Category { Name = "Level 2", Children = { new Category { Name = "Level 3 (cut off)" } } } },
        },
    },
};

var rootDto = root.ToCategoryDto();
Assert(rootDto.Children[0].Children[0].Name == "Level 2", "MaxDepth: Level 2 still mapped");
Assert(rootDto.Children[0].Children[0].Children.Count == 0, "MaxDepth: Level 3 cut off");
Console.WriteLine($"[MaxDepth] {rootDto.Name} -> {rootDto.Children[0].Name} -> {rootDto.Children[0].Children[0].Name} " +
    $"(Children.Count={rootDto.Children[0].Children[0].Children.Count}, cut off by MaxDepth)");

Console.WriteLine();

var image = new ImageAttachment { FileName = "cover.png", Width = 1920, Height = 1080 };
var video = new VideoAttachment { FileName = "demo.mp4", DurationSeconds = 42 };
var plain = new Attachment { FileName = "notes.txt" };

Assert(image.ToAttachmentDto() is ImageAttachmentDto { Width: 1920, Height: 1080 }, "MapInclude: ImageAttachment dispatch");
Assert(video.ToAttachmentDto() is VideoAttachmentDto { DurationSeconds: 42 }, "MapInclude: VideoAttachment dispatch");
Assert(plain.ToAttachmentDto() is AttachmentDto and not ImageAttachmentDto and not VideoAttachmentDto, "MapInclude: base fallback");
Console.WriteLine("[MapInclude] ImageAttachment/VideoAttachment/base Attachment all dispatched correctly.");

Console.WriteLine();
Console.WriteLine("All Native AOT mapping checks passed, including a compiled Expression<Func<Order, OrderDto>>.");

static void Verify(
    string via, OrderDto dto, Order source,
    bool checkConditionedProperty = true, bool checkEnumToString = true, bool checkCollectionShapes = true)
{
    Console.WriteLine($"[{via}] {dto.Reference}: total={dto.Total}, notes='{dto.InternalNotes}', status='{dto.Status}'");

    Assert(dto.Id == source.Id, "Id");
    Assert(dto.Reference == source.Reference, "Reference");
    Assert(dto.Customer.Name == source.Customer.Name, "Customer.Name (nested mapping)");
    Assert(dto.Customer.Email == source.Customer.Email, "Customer.Email (nested mapping)");
    Assert(dto.CustomerEmail == source.Customer.Email, "CustomerEmail (explicit dotted-path [MapProperty])");
    Assert(dto.LineItems.Count == source.LineItems.Count, "LineItems.Count (enumerable mapping, element type declared via [MapFrom])");

    for (var i = 0; i < source.LineItems.Count; i++)
    {
        Assert(dto.LineItems[i].ProductName == source.LineItems[i].ProductName, $"LineItems[{i}].ProductName");
        Assert(dto.LineItems[i].Quantity == source.LineItems[i].Quantity, $"LineItems[{i}].Quantity");
    }

    var expectedTotal = source.LineItems.Sum(item => item.Quantity * item.UnitPrice);
    Assert(dto.Total == expectedTotal, "Total ([MapUsing] converter, InlineInProjection)");

    Assert(dto.StatusCode == (int)source.Status, "StatusCode (built-in enum -> underlying-type conversion)");
    Assert(dto.PromoCode == (source.PromoCode ?? "NONE"), "PromoCode ([MapDefault])");

    if (checkCollectionShapes)
    {
        Assert(dto.Tags.SequenceEqual(source.Tags), "Tags (ImmutableArray<T> destination shape)");
        Assert(dto.RecentNotes.SequenceEqual(source.RecentNotes), "RecentNotes (ObservableCollection<T> destination shape)");
    }
    else
    {
        Assert(dto.Tags.IsEmpty, "Tags (excluded from the expression-tree projection by AM023)");
        Assert(dto.RecentNotes.Count == 0, "RecentNotes (excluded from the expression-tree projection by AM023)");
    }

    if (checkEnumToString)
    {
        Assert(dto.Status == source.Status.ToString(), "Status (built-in enum -> string conversion)");
    }
    else
    {
        Assert(dto.Status == "", "Status (excluded from the expression-tree projection by AM022)");
    }

    if (checkConditionedProperty)
    {
        var expectedNotes = source.IsInternal ? source.InternalNotes : "";
        Assert(dto.InternalNotes == expectedNotes, "InternalNotes ([MapCondition])");
    }
    else
    {
        Assert(dto.InternalNotes == "", "InternalNotes (excluded from the expression-tree projection by AM005)");
    }
}

static void Assert(bool condition, string what)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Native AOT verification failed: {what}");
    }
}

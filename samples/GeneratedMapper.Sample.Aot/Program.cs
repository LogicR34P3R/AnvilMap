using GeneratedMapper;
using GeneratedMapper.Sample.Aot.Models;
using GeneratedMapper.Sample.Aot.ViewModels;

// Native AOT verification target - see docs/roadmapv3.md F15 and README.md's AOT section. This
// exercises direct/nested/enumerable mapping, [MapCondition], and [MapUsing] through both the
// extension-method and IMapper paths, then asserts the results rather than just printing them -
// the point is to catch a silently-wrong trim (something reflection-based quietly no-op'ing),
// not just confirm the process exits 0.

var external = new Order
{
    Id = 1,
    Reference = "ORD-EXTERNAL",
    IsInternal = false,
    InternalNotes = "Should never appear on the DTO below.",
    Customer = new Customer { Name = "Ada Lovelace", Email = "ada@example.com" },
    LineItems =
    {
        new LineItem { ProductName = "Widget", Quantity = 2, UnitPrice = 9.99m },
        new LineItem { ProductName = "Gadget", Quantity = 1, UnitPrice = 24.50m },
    },
};

var internalOrder = new Order
{
    Id = 2,
    Reference = "ORD-INTERNAL",
    IsInternal = true,
    InternalNotes = "Restock before Friday.",
    Customer = new Customer { Name = "Grace Hopper", Email = "grace@example.com" },
    LineItems = { new LineItem { ProductName = "Cable", Quantity = 5, UnitPrice = 3.00m } },
};

Verify("extension method", external.ToOrderDto(), external);
Verify("extension method", internalOrder.ToOrderDto(), internalOrder);

IMapper mapper = new GeneratedMapperService();
Verify("IMapper", mapper.Map<Order, OrderDto>(external), external);
Verify("IMapper", mapper.Map<Order, OrderDto>(internalOrder), internalOrder);

// GeneratedMappings.OrderToOrderDtoProjection - the Expression<Func<Order, OrderDto>> behind
// ProjectToOrderDto() - is what the IL2026 trim warning is actually about (the C# compiler uses
// the [RequiresUnreferencedCode]-annotated Expression.Bind to build its MemberInitExpression).
// EF Core never calls .Compile() on it - it walks the expression tree and translates it to SQL -
// but calling .Compile() here is exactly what IQueryable.Select() does under the hood for an
// in-memory (non-EF-Core) LINQ provider, so it's the most direct way to prove two things under
// Native AOT specifically: that compiling an expression tree at runtime works at all (a bigger,
// separate question from trimming - full AOT has no JIT, so this depends on the interpreter
// fallback), and that the reflected property accessors Expression.Bind needed weren't trimmed
// away despite the warning.
var compiled = GeneratedMappings.OrderToOrderDtoProjection.Compile();
// [MapCondition] can't be translated into an expression tree (no method calls allowed), so the
// projection leaves InternalNotes out entirely (GM005) rather than gating it - unlike the
// imperative/IMapper paths above, it's always "" here regardless of IsInternal. Expected, not a
// trimming artifact - checked separately below so this doesn't get confused with an actual bug.
Verify("compiled projection", compiled(external), external, checkConditionedProperty: false);
Verify("compiled projection", compiled(internalOrder), internalOrder, checkConditionedProperty: false);

Console.WriteLine();
Console.WriteLine("All Native AOT mapping checks passed, including a compiled Expression<Func<Order, OrderDto>>.");

static void Verify(string via, OrderDto dto, Order source, bool checkConditionedProperty = true)
{
    Console.WriteLine($"[{via}] {dto.Reference}: total={dto.Total}, notes='{dto.InternalNotes}'");

    Assert(dto.Id == source.Id, "Id");
    Assert(dto.Reference == source.Reference, "Reference");
    Assert(dto.Customer.Name == source.Customer.Name, "Customer.Name (nested mapping)");
    Assert(dto.Customer.Email == source.Customer.Email, "Customer.Email (nested mapping)");
    Assert(dto.LineItems.Count == source.LineItems.Count, "LineItems.Count (enumerable mapping)");

    for (var i = 0; i < source.LineItems.Count; i++)
    {
        Assert(dto.LineItems[i].ProductName == source.LineItems[i].ProductName, $"LineItems[{i}].ProductName");
        Assert(dto.LineItems[i].Quantity == source.LineItems[i].Quantity, $"LineItems[{i}].Quantity");
    }

    var expectedTotal = source.LineItems.Sum(item => item.Quantity * item.UnitPrice);
    Assert(dto.Total == expectedTotal, "Total ([MapUsing] converter)");

    if (checkConditionedProperty)
    {
        var expectedNotes = source.IsInternal ? source.InternalNotes : "";
        Assert(dto.InternalNotes == expectedNotes, "InternalNotes ([MapCondition])");
    }
    else
    {
        Assert(dto.InternalNotes == "", "InternalNotes (excluded from the SQL projection by GM005)");
    }
}

static void Assert(bool condition, string what)
{
    if (!condition)
        throw new InvalidOperationException($"Native AOT verification failed: {what}");
}

using AnvilMap;

[MapTo(typeof(ProductDto))]
[MapCondition(typeof(ProductDto), nameof(ProductDto.InternalSku), nameof(ShouldMapInternalSku))]
[MapUsing(typeof(ProductDto), nameof(ProductDto.DisplayPrice), nameof(FormatPrice), InlineInProjection = true)]
[MapDefault(typeof(ProductDto), nameof(ProductDto.Description), "No description")]
[MapProperty(typeof(ProductDto), nameof(Status), nameof(ProductDto.StatusCode))]
public sealed class Product
{
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public string? Description { get; set; }
    public bool IsInternal { get; set; }
    public string InternalSku { get; set; } = "";
    public ProductStatus Status { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<int> RelatedIds { get; set; } = new();
    public List<string> RecentChanges { get; set; } = new();

    public static bool ShouldMapInternalSku(Product source) => source.IsInternal;

    public static string FormatPrice(Product source) => "$" + source.Price.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
}

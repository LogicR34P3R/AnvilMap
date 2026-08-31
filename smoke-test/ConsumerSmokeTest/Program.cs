using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using AnvilMap;

var user = new User { Id = 1, Name = "Ada" };
var dto = user.ToUserDto();
Check(dto.Id == 1 && dto.Name == "Ada", "Basic mapping + GenerateReverse (forward)");

IMapper mapper = new AnvilMapService();
var viaMapper = mapper.Map<User, UserDto>(user);
Check(viaMapper.Id == 1 && viaMapper.Name == "Ada", "IMapper dispatch");

var roundTripped = dto.ToUser();
Check(roundTripped.Id == 1 && roundTripped.Name == "Ada", "GenerateReverse (reverse direction)");

var oneArgIntercepted = GeneratedMappings.Map<User, UserDto>(user);
Check(oneArgIntercepted.Id == 1 && oneArgIntercepted.Name == "Ada", "Generic dispatcher, one-arg");

var existingDto = new UserDto();
var twoArgIntercepted = GeneratedMappings.Map<User, UserDto>(user, existingDto);
Check(ReferenceEquals(twoArgIntercepted, existingDto) && twoArgIntercepted.Id == 1, "Generic dispatcher, two-arg");

var employee = new Employee { Name = "Grace", Address = new Address { City = "Arlington" } };
var employeeDto = employee.ToEmployeeDto();
Check(employeeDto.HomeCity == "Arlington", "Explicit dotted-path [MapProperty]");

var product = new Product
{
    Name = "Widget",
    Price = 19.999m,
    IsInternal = true,
    InternalSku = "SKU-123",
    Status = ProductStatus.Active,
    Tags = { "new", "sale", "new" },
    RelatedIds = { 10, 20 },
    RecentChanges = { "restocked" },
};
var productDto = product.ToProductDto();
Check(productDto.InternalSku == "SKU-123", "[MapCondition] (true branch)");
Check(productDto.DisplayPrice == "$20.00", "[MapUsing] converter");
Check(productDto.Description == "No description", "[MapDefault] (null source)");
Check(productDto.Status == "Active", "Built-in enum -> string conversion");
Check(productDto.StatusCode == (int)ProductStatus.Active, "Built-in enum -> underlying-type conversion");
Check(productDto.Tags.SetEquals(new[] { "new", "sale" }), "HashSet<T> destination shape (dedup)");
Check(productDto.RelatedIds.SequenceEqual(new[] { 10, 20 }), "ImmutableArray<T> destination shape");
Check(productDto.RecentChanges.SequenceEqual(new[] { "restocked" }), "ObservableCollection<T> destination shape");

var externalProduct = new Product { Name = "Gadget", Price = 5m, IsInternal = false, Status = ProductStatus.Draft };
var externalProductDto = externalProduct.ToProductDto();
Check(externalProductDto.InternalSku == "", "[MapCondition] (false branch)");

var coordinates = new Coordinates { Lat = 38.88, Lng = -77.10 };
var coordinatesDto = coordinates.ToCoordinatesDto();
Check(coordinatesDto.Lat == 38.88 && coordinatesDto.Lng == -77.10, "[MapFrom] declared on the destination side");

var root = new Category
{
    Name = "Root",
    Children = { new Category { Name = "L1", Children = { new Category { Name = "L2", Children = { new Category { Name = "L3 (cut off)" } } } } } },
};
var rootDto = root.ToCategoryDto();
Check(rootDto.Children[0].Children[0].Name == "L2", "MaxDepth (still maps up to the limit)");
Check(rootDto.Children[0].Children[0].Children.Count == 0, "MaxDepth (cuts off past the limit)");

var dog = new Dog { Name = "Rex", Breed = "Labrador" };
var animalDto = dog.ToAnimalDto();
Check(animalDto is DogDto { Breed: "Labrador" }, "[MapInclude] polymorphic dispatch");
Check(new Animal { Name = "Generic" }.ToAnimalDto() is AnimalDto and not DogDto, "[MapInclude] base fallback");

Check(GeneratedMappings.Map<AnimalDto>((object)dog) is DogDto { Breed: "Labrador" }, "[MapInclude] via generic dispatcher (runtime-type-keyed)");
Check(mapper.Map<Animal, AnimalDto>(dog) is DogDto { Breed: "Labrador" }, "[MapInclude] via IMapper");

var compiledProductProjection = GeneratedMappings.ProductToProductDtoProjection.Compile()(product);
Check(compiledProductProjection.DisplayPrice == "$20.00", "[MapUsing] InlineInProjection - compiled projection expression");

Console.WriteLine("Smoke test passed: AnvilMap.Generator + AnvilMap.Abstractions work correctly from packed NuGet packages.");

VerifyInterceptors();
VerifyInlineProjection();

static void Check(bool condition, string what)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Smoke test failed: {what}");
    }
}

static void VerifyInterceptors()
{
    var text = ReadGeneratedMappingsSource();

#if NET10_0_OR_GREATER
    Check(text.Contains("InterceptsLocation("), "Interceptor emitted on a C# 14 (net10.0) consumer");
    Check(text.Contains("class Interceptors"), "Interceptor container class emitted on a C# 14 (net10.0) consumer");
    Console.WriteLine("Interceptor smoke test passed: net10.0 consumer got real [InterceptsLocation] redirects.");
#else
    Check(!text.Contains("InterceptsLocation"), "No interceptor on a pre-C#14 (net8.0) consumer");
    Check(!text.Contains("class Interceptors"), "No interceptor container class on a pre-C#14 (net8.0) consumer");
    Console.WriteLine("Interceptor smoke test passed: net8.0 consumer (control) got no interception, dispatcher still worked above.");
#endif
}

static void VerifyInlineProjection()
{
    var text = ReadGeneratedMappingsSource();
    var projectionLine = text.Split('\n').FirstOrDefault(l => l.Contains("ProductToProductDtoProjection ="));

    Check(projectionLine is not null, "Projection field initializer for Product -> ProductDto exists");
    Check(!projectionLine!.Contains("FormatPrice("), "InlineInProjection spliced the converter body instead of calling it");
    Check(projectionLine.Contains("(source).Price.ToString(\"F2\""), "Inlined converter body is present in the projection");
    Console.WriteLine("Inline-projection smoke test passed: [MapUsing] InlineInProjection spliced into the real generated projection.");
}

static string ReadGeneratedMappingsSource()
{
    var path = Path.Combine(
        ProjectDirectory(),
        "Generated",
#if NET10_0_OR_GREATER
        "net10.0",
#else
        "net8.0",
#endif
        "AnvilMap.Generator",
        "AnvilMap.Generator.MappingSourceGenerator",
        "GeneratedMappings.g.cs");

    if (!File.Exists(path))
    {
        throw new InvalidOperationException($"Smoke test failed: expected the real generated file at {path}.");
    }

    return File.ReadAllText(path);
}

static string ProjectDirectory([CallerFilePath] string here = "") => Path.GetDirectoryName(here)!;

[MapTo(typeof(UserDto), GenerateReverse = true)]
public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public sealed class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

[MapTo(typeof(EmployeeDto))]
[MapProperty(typeof(EmployeeDto), "Address.City", nameof(EmployeeDto.HomeCity))]
public sealed class Employee
{
    public string Name { get; set; } = "";
    public Address Address { get; set; } = new();
}

public sealed class Address
{
    public string City { get; set; } = "";
}

public sealed class EmployeeDto
{
    public string Name { get; set; } = "";
    public string HomeCity { get; set; } = "";
}

public enum ProductStatus
{
    Draft,
    Active,
    Discontinued
}

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

public sealed class ProductDto
{
    public string Name { get; set; } = "";
    public string DisplayPrice { get; set; } = "";
    public string Description { get; set; } = "";
    public string InternalSku { get; set; } = "";
    public string Status { get; set; } = "";
    public int StatusCode { get; set; }
    public HashSet<string> Tags { get; set; } = new();
    public ImmutableArray<int> RelatedIds { get; set; } = ImmutableArray<int>.Empty;
    public ObservableCollection<string> RecentChanges { get; set; } = new();
}

public sealed class Coordinates
{
    public double Lat { get; set; }
    public double Lng { get; set; }
}

[MapFrom(typeof(Coordinates))]
public sealed class CoordinatesDto
{
    public double Lat { get; set; }
    public double Lng { get; set; }
}

[MapTo(typeof(CategoryDto), MaxDepth = 2)]
public sealed class Category
{
    public string Name { get; set; } = "";
    public List<Category> Children { get; set; } = new();
}

public sealed class CategoryDto
{
    public string Name { get; set; } = "";
    public List<CategoryDto> Children { get; set; } = new();
}

[MapTo(typeof(AnimalDto))]
[MapInclude(typeof(AnimalDto), typeof(Dog), typeof(DogDto))]
public class Animal
{
    public string Name { get; set; } = "";
}

[MapTo(typeof(DogDto))]
public sealed class Dog : Animal
{
    public string Breed { get; set; } = "";
}

public class AnimalDto
{
    public string Name { get; set; } = "";
}

public sealed class DogDto : AnimalDto
{
    public string Breed { get; set; } = "";
}

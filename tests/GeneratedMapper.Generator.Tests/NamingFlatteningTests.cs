using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace GeneratedMapper.Generator.Tests;

// F9: naming-convention flattening (docs/roadmapv2.md). Covers the resolver-side path matching
// only - MappingEmitter needs no changes at all, since a flattened destination just gets a
// dotted SourcePropertyName ("HomeAddress.City") that every existing `source.{...}` interpolation
// already treats as an opaque C# member-access expression.
public class NamingFlatteningTests
{
    [Fact]
    public void SingleLevelFlattening_MatchesNestedProperty()
    {
        var result = GeneratorTestHelper.Run(@"
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
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.HomeAddressCity = source.HomeAddress.City;", result.GeneratedSource);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "GM001");
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MultiLevelFlattening_MatchesDeeplyNestedProperty()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(OrderDto))]
public sealed class Order
{
    public Customer Buyer { get; set; } = new();
}

public sealed class Customer
{
    public HomeAddress Home { get; set; } = new();
}

public sealed class HomeAddress
{
    public string City { get; set; } = """";
}

public sealed class OrderDto
{
    public string BuyerHomeCity { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.BuyerHomeCity = source.Buyer.Home.City;", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void Flattening_HonoredInSqlProjection()
    {
        var result = GeneratorTestHelper.Run(@"
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
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("source => new global::Sample.UserDto { HomeAddressCity = source.HomeAddress.City };", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void ExplicitMapPropertyOverride_IsNotSubjectToFlattening()
    {
        // An explicit [MapProperty] override naming a source property that doesn't exist must
        // still report GM001 rather than silently falling back to flattening - even though
        // "HomeAddress.City" would otherwise be a valid flattened match for HomeAddressCity.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapProperty(typeof(UserDto), ""NotARealProperty"", nameof(UserDto.HomeAddressCity))]
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
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM001" && d.GetMessage().Contains("HomeAddressCity"));
        Assert.DoesNotContain("HomeAddressCity", result.GeneratedSource);
    }

    [Fact]
    public void NullableIntermediateSegment_IsNotFlattened()
    {
        // HomeAddress is nullable here, so the chain would need a null-guard to be safe -
        // excluded from candidates entirely rather than emitting an unguarded `.` chain.
        var result = GeneratorTestHelper.Run(@"
#nullable enable
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public Address? HomeAddress { get; set; }
}

public sealed class Address
{
    public string City { get; set; } = """";
}

public sealed class UserDto
{
    public string HomeAddressCity { get; set; } = """";
}
");

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM001" && d.GetMessage().Contains("HomeAddressCity"));
    }

    [Fact]
    public void AmbiguousFlattenedMatch_ReportsGM010AndLeavesPropertyUnmapped()
    {
        // "HomeAddressCity" splits two valid ways: Home.AddressCity and HomeAddress.City.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public Home Home { get; set; } = new();
    public Address HomeAddress { get; set; } = new();
}

public sealed class Home
{
    public string AddressCity { get; set; } = """";
}

public sealed class Address
{
    public string City { get; set; } = """";
}

public sealed class UserDto
{
    public string HomeAddressCity { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM010" && d.GetMessage().Contains("HomeAddressCity"));
        Assert.DoesNotContain("HomeAddressCity", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void NoPossibleSplit_StillReportsGM001()
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
    public string CompletelyUnrelatedName { get; set; } = """";
}
");

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM001" && d.GetMessage().Contains("CompletelyUnrelatedName"));
    }

    [Fact]
    public void Flattening_NotAutoReversedByGenerateReverse()
    {
        // GenerateReverse mirrors ExplicitProperties (renamed), but a flattened match isn't an
        // ExplicitPropertyMapping at all - it's resolved fresh per-direction. The reverse
        // direction (UserDto -> User) has no way to un-flatten "HomeAddressCity" back into
        // separate Address-object construction, so User.HomeAddress is left unmapped there.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto), GenerateReverse = true)]
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
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.HomeAddressCity = source.HomeAddress.City;", result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM001" && d.GetMessage().Contains("HomeAddress") && d.GetMessage().Contains("Sample.User"));
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void IncompatibleLeafType_ReportsGM003WithTheFlattenedPathInTheMessage()
    {
        // Address.City (string) can't convert to UserDto.HomeAddressCity (int) - the flattened
        // path is still found (that part succeeds), but Kind resolution then fails on the leaf's
        // type. The diagnostic must name the actual chain that was matched ("HomeAddress.City"),
        // not just the last segment ("City") - otherwise the message would be misleading about
        // what was actually resolved.
        var result = GeneratorTestHelper.Run(@"
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
    public int HomeAddressCity { get; set; }
}
");

        Assert.Contains(result.GeneratorDiagnostics, d =>
            d.Id == "GM003" &&
            d.GetMessage().Contains("HomeAddress.City") &&
            d.GetMessage().Contains("HomeAddressCity"));
    }

    [Fact]
    public void MapCondition_AppliesToAFlattenedProperty()
    {
        // [MapCondition] is keyed by destination property name and doesn't touch
        // SourcePropertyName at all, so it should gate a flattened value exactly like a direct
        // one - the guard and the flattened chain both need to show up together.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapCondition(typeof(UserDto), nameof(UserDto.HomeAddressCity), nameof(ShouldMapCity))]
public sealed class User
{
    public Address HomeAddress { get; set; } = new();
    public bool IsPublic { get; set; }

    public static bool ShouldMapCity(User source) => source.IsPublic;
}

public sealed class Address
{
    public string City { get; set; } = """";
}

public sealed class UserDto
{
    public string HomeAddressCity { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("if (global::Sample.User.ShouldMapCity(source))", result.GeneratedSource);
        Assert.Contains("destination.HomeAddressCity = source.HomeAddress.City;", result.GeneratedSource);
        // Conditioned, so excluded from the SQL projection (GM005) rather than mistranslated.
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM005" && d.GetMessage().Contains("HomeAddressCity"));
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void MapDefault_AppliesToAFlattenedProperty()
    {
        var result = GeneratorTestHelper.Run(@"
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
    public string City { get; set; } = """";
}

public sealed class UserDto
{
    public string HomeAddressCity { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.HomeAddressCity = source.HomeAddress.City ?? \"Unknown\";", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void FlattenedLeaf_ResolvesToNestedKind_NotJustDirect()
    {
        // The flattened leaf (HomeAddress.PrimaryContact) is itself a mapped type, not a plain
        // scalar - flattening must feed into the normal Kind resolution (Direct/Nested/
        // Enumerable), not assume Direct.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public Address HomeAddress { get; set; } = new();
}

public sealed class Address
{
    public Contact PrimaryContact { get; set; } = new();
}

[MapTo(typeof(ContactDto))]
public sealed class Contact
{
    public string Phone { get; set; } = """";
}

public sealed class ContactDto
{
    public string Phone { get; set; } = """";
}

public sealed class UserDto
{
    public ContactDto HomeAddressPrimaryContact { get; set; } = new();
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(
            "destination.HomeAddressPrimaryContact = source.HomeAddress.PrimaryContact.ToContactDto();",
            result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void FlattenedProperty_UsableAsPositionalRecordConstructorArgument()
    {
        // TryMatchConstructor keys resolved properties by DestinationPropertyName, so a
        // flattened property should be just as eligible as a direct one for becoming a
        // constructor argument on a positional-record destination.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
    public Address HomeAddress { get; set; } = new();
}

public sealed class Address
{
    public string City { get; set; } = """";
}

public sealed record UserDto(int Id, string HomeAddressCity);
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(
            "source => new global::Sample.UserDto(source.Id, source.HomeAddress.City);",
            result.GeneratedSource);
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

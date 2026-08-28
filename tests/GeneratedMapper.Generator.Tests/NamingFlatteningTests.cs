using Microsoft.CodeAnalysis;

namespace GeneratedMapper.Generator.Tests;

// Naming-convention flattening. Covers the resolver-side path matching only - MappingEmitter
// needs no changes at all, since a flattened destination just gets a dotted SourcePropertyName
// ("HomeAddress.City") that every existing `source.{...}` interpolation already treats as an
// opaque C# member-access expression.
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
    public void ExplicitMapPropertyOverride_PlainNameNotSubjectToPascalCaseSearch_ReportsGM021()
    {
        // A plain (non-dotted) explicit [MapProperty] source must still be an exact top-level
        // property name - it never falls back to TryResolveFlattenedPath's PascalCase-boundary
        // search (that's the default name-matching path's fallback only). Reported as GM021
        // (naming exactly what was looked up), not the generic GM001.
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
        Assert.Contains(result.GeneratorDiagnostics, d =>
            d.Id == "GM021" && d.GetMessage().Contains("HomeAddressCity") && d.GetMessage().Contains("NotARealProperty"));
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "GM001");
        Assert.DoesNotContain("HomeAddressCity", result.GeneratedSource);
    }

    [Fact]
    public void ExplicitMapPropertyOverride_DottedPath_ResolvesNestedProperty()
    {
        // "HomeAddressCity" splits two valid ways (see AmbiguousFlattenedMatch_...), so the
        // automatic PascalCase search refuses to guess (GM010) - but an explicit [MapProperty]
        // naming the specific dotted path directly resolves it unambiguously, since every
        // segment is given literally rather than searched for.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapProperty(typeof(UserDto), ""HomeAddress.City"", nameof(UserDto.HomeAddressCity))]
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
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "GM010");
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "GM021");
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "GM001");
        Assert.Contains("destination.HomeAddressCity = source.HomeAddress.City;", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void ExplicitMapPropertyOverride_DottedPath_UnknownIntermediateSegment_ReportsGM021WithSpecificSegment()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapProperty(typeof(UserDto), ""HomeAddress.Cty"", nameof(UserDto.HomeAddressCity))]
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
        Assert.Contains(result.GeneratorDiagnostics, d =>
            d.Id == "GM021" && d.GetMessage().Contains("Address") && d.GetMessage().Contains("Cty"));
        Assert.DoesNotContain("HomeAddressCity", result.GeneratedSource);
    }

    [Fact]
    public void ExplicitMapPropertyOverride_DottedPath_NullableIntermediateSegment_ReportsGM021()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapProperty(typeof(UserDto), ""HomeAddress.City"", nameof(UserDto.HomeAddressCity))]
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

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d =>
            d.Id == "GM021" && d.GetMessage().Contains("HomeAddress") && d.GetMessage().Contains("nullable"));
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
    public void AmbiguousFlattenedMatch_DisambiguatedWithMapUsing_ResolvesCorrectly()
    {
        // [MapProperty] naming the dotted path directly (see
        // ExplicitMapPropertyOverride_DottedPath_ResolvesNestedProperty) is the simpler fix for
        // this - [MapUsing] is an alternative, useful when the value needs actual computation
        // rather than just picking a different source path.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapUsing(typeof(UserDto), nameof(UserDto.HomeAddressCity), nameof(ResolveHomeAddressCity))]
public sealed class User
{
    public Home Home { get; set; } = new();
    public Address HomeAddress { get; set; } = new();

    public static string ResolveHomeAddressCity(User source) => source.HomeAddress.City;
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
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "GM010");
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "GM001");
        Assert.Contains("destination.HomeAddressCity = global::Sample.User.ResolveHomeAddressCity(source);", result.GeneratedSource);
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

    // Explicit dotted-path [MapProperty] (TryResolveExplicitPath) - good weather.

    [Fact]
    public void ExplicitMapPropertyOverride_DottedPath_MultiLevelResolvesNestedProperty()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(OrderDto))]
[MapProperty(typeof(OrderDto), ""Buyer.Home.City"", nameof(OrderDto.BuyerCity))]
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
    public string BuyerCity { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id is "GM021" or "GM001");
        Assert.Contains("destination.BuyerCity = source.Buyer.Home.City;", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void ExplicitMapPropertyOverride_DottedPath_CombinedWithMapCondition_GatesTheValue()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapProperty(typeof(UserDto), ""HomeAddress.City"", nameof(UserDto.HomeAddressCity))]
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
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM005" && d.GetMessage().Contains("HomeAddressCity"));
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void ExplicitMapPropertyOverride_DottedPath_CombinedWithMapDefault_SubstitutesNull()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapProperty(typeof(UserDto), ""HomeAddress.City"", nameof(UserDto.HomeAddressCity))]
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
");

        Assert.NotNull(result.GeneratedSource);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "GM019");
        Assert.Contains("destination.HomeAddressCity = source.HomeAddress.City ?? \"Unknown\";", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void ExplicitMapPropertyOverride_DottedPath_HonoredInSqlProjection()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapProperty(typeof(UserDto), ""HomeAddress.City"", nameof(UserDto.HomeAddressCity))]
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
    public void ExplicitMapPropertyOverride_DottedPath_LeafResolvesToNestedKind()
    {
        // The leaf (Buyer.PrimaryAddress) is itself a mapped type, not a plain scalar - the
        // explicit path must feed into the normal Kind resolution (Direct/Nested/Enumerable)
        // exactly like automatic flattening's leaf does (FlattenedLeaf_ResolvesToNestedKind...).
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(OrderDto))]
[MapProperty(typeof(OrderDto), ""Buyer.PrimaryAddress"", nameof(OrderDto.ShippingAddress))]
public sealed class Order
{
    public Customer Buyer { get; set; } = new();
}

public sealed class Customer
{
    public Address PrimaryAddress { get; set; } = new();
}

[MapTo(typeof(AddressDto))]
public sealed class Address
{
    public string City { get; set; } = """";
}

public sealed class AddressDto
{
    public string City { get; set; } = """";
}

public sealed class OrderDto
{
    public AddressDto ShippingAddress { get; set; } = new();
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.ShippingAddress = source.Buyer.PrimaryAddress.ToAddressDto();", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void ExplicitMapPropertyOverride_DottedPath_NullableLeafSegment_IsAllowed()
    {
        // Only INTERMEDIATE segments must be non-nullable - the leaf follows the same
        // nullability rules as a normal direct match (see NullableIntermediateSegment_...
        // for the intermediate case, which this must NOT trigger for a nullable leaf).
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapProperty(typeof(UserDto), ""HomeAddress.City"", nameof(UserDto.HomeAddressCity))]
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
    public string? HomeAddressCity { get; set; }
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "GM021");
        Assert.Contains("destination.HomeAddressCity = source.HomeAddress.City;", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void ExplicitMapPropertyOverride_DottedPath_NotAutoReversedByGenerateReverse()
    {
        // Mirrors Flattening_NotAutoReversedByGenerateReverse: ExplicitProperties DO reverse
        // (renamed), but the reversed entry ("HomeAddress.City" as the DESTINATION name) never
        // matches a real property on User in the reverse direction either - User.HomeAddress is
        // left unmapped there (GM001), same fail-open-to-GM001 outcome as automatic flattening.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto), GenerateReverse = true)]
[MapProperty(typeof(UserDto), ""HomeAddress.City"", nameof(UserDto.HomeAddressCity))]
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
    public void ExplicitMapPropertyOverride_DottedPath_UsableAsPositionalRecordConstructorArgument()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapProperty(typeof(UserDto), ""HomeAddress.City"", nameof(UserDto.HomeAddressCity))]
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

    // Explicit dotted-path [MapProperty] - bad weather.

    [Fact]
    public void ExplicitMapPropertyOverride_DottedPath_UnknownFirstSegment_ReportsGM021()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapProperty(typeof(UserDto), ""NotReal.City"", nameof(UserDto.HomeAddressCity))]
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
        Assert.Contains(result.GeneratorDiagnostics, d =>
            d.Id == "GM021" && d.GetMessage().Contains("Sample.User") && d.GetMessage().Contains("NotReal"));
        Assert.DoesNotContain("HomeAddressCity", result.GeneratedSource);
    }

    [Fact]
    public void ExplicitMapPropertyOverride_DottedPath_UnknownLeafSegment_ReportsGM021()
    {
        // Distinct from an unknown intermediate segment (...UnknownIntermediateSegment...): the
        // walk gets all the way to the last segment before failing, so the reported type must
        // be the LEAF's containing type (Address), not the root (User).
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapProperty(typeof(UserDto), ""HomeAddress.NotReal"", nameof(UserDto.HomeAddressCity))]
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
        Assert.Contains(result.GeneratorDiagnostics, d =>
            d.Id == "GM021" && d.GetMessage().Contains("Sample.Address") && d.GetMessage().Contains("NotReal"));
        Assert.DoesNotContain("HomeAddressCity", result.GeneratedSource);
    }

    [Fact]
    public void ExplicitMapPropertyOverride_DottedPath_TrailingDot_ReportsGM021WithoutCrashing()
    {
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapProperty(typeof(UserDto), ""HomeAddress."", nameof(UserDto.HomeAddressCity))]
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
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM021");
        Assert.DoesNotContain("HomeAddressCity", result.GeneratedSource);
    }

    [Fact]
    public void ExplicitMapPropertyOverride_DottedPath_TargetingIgnoredProperty_ReportsGM012NotGM021()
    {
        // [MapIgnore] wins before source resolution is ever attempted (see MappingResolver.cs's
        // deadOverrides check) - a dotted [MapProperty] on an ignored property must report GM012
        // (dead override), not GM021, since its source is never actually looked up.
        var result = GeneratorTestHelper.Run(@"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapProperty(typeof(UserDto), ""HomeAddress.City"", nameof(UserDto.HomeAddressCity))]
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
    [MapIgnore]
    public string HomeAddressCity { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "GM012" && d.GetMessage().Contains("[MapProperty]"));
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "GM021");
        Assert.DoesNotContain("destination.HomeAddressCity", result.GeneratedSource);
    }

    private static void AssertNoCompileErrors(GeneratorTestResult result)
    {
        var errors = result.CompilationDiagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.True(errors.Count == 0, string.Join("\n", errors.Select(e => e.ToString())));
    }
}

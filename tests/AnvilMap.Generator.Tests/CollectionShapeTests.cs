namespace AnvilMap.Generator.Tests;

public class CollectionShapeTests
{
    private const string Source = @"
using AnvilMap;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;

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

[MapTo(typeof(PersonDto))]
public sealed class Person
{
    public List<int> Tags { get; set; } = new();
    public List<int>? NullableTags { get; set; }
    public List<Address> Addresses { get; set; } = new();
    public List<Address>? NullableAddresses { get; set; }
}

public sealed class PersonDto
{
    public ImmutableArray<int> Tags { get; set; }
    public ImmutableArray<int> NullableTags { get; set; }
    public ObservableCollection<AddressDto> Addresses { get; set; } = new();
    public ObservableCollection<AddressDto>? NullableAddresses { get; set; }
}
";

    [Fact]
    public void ImmutableArray_SameElementType_EmitsToImmutableArrayAndCompiles()
    {
        var result = GeneratorTestHelper.Run(Source);

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.Tags = source.Tags.ToImmutableArray();", result.GeneratedSource);
        Assert.Contains("using System.Collections.Immutable;", result.GeneratedSource);
        GeneratorTestHelper.AssertNoUnexpectedErrors(result, "AM023");
    }

    [Fact]
    public void ImmutableArray_NullableSource_FallsBackToEmptyInsteadOfDefault()
    {
        var result = GeneratorTestHelper.Run(Source);

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(
            "destination.NullableTags = source.NullableTags is null ? global::System.Collections.Immutable.ImmutableArray<int>.Empty : source.NullableTags.ToImmutableArray();",
            result.GeneratedSource);
        GeneratorTestHelper.AssertNoUnexpectedErrors(result, "AM023");
    }

    [Fact]
    public void ImmutableArray_ExcludedFromProjectionWithAM023()
    {
        var result = GeneratorTestHelper.Run(Source);

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d =>
            d.Id == "AM023" && d.GetMessage().Contains("Tags") && d.GetMessage().Contains("ImmutableArray"));
        // All four Person properties are excluded (ImmutableArray/ObservableCollection), so the
        // projection initializer ends up empty.
        Assert.Contains("PersonToPersonDtoProjection = source => new global::Sample.PersonDto {  };", result.GeneratedSource);
        GeneratorTestHelper.AssertNoUnexpectedErrors(result, "AM023");
    }

    [Fact]
    public void ObservableCollection_ElementMapped_WrapsInConstructorAndCompiles()
    {
        var result = GeneratorTestHelper.Run(Source);

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(
            "destination.Addresses = new global::System.Collections.ObjectModel.ObservableCollection<global::Sample.AddressDto>(source.Addresses.Select(x => x.ToAddressDto()));",
            result.GeneratedSource);
        GeneratorTestHelper.AssertNoUnexpectedErrors(result, "AM023");
    }

    [Fact]
    public void ObservableCollection_NullableSource_UsesTernaryNullCheck()
    {
        var result = GeneratorTestHelper.Run(Source);

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(
            "destination.NullableAddresses = source.NullableAddresses is null ? null! : new global::System.Collections.ObjectModel.ObservableCollection<global::Sample.AddressDto>(source.NullableAddresses.Select(x => x.ToAddressDto()));",
            result.GeneratedSource);
        GeneratorTestHelper.AssertNoUnexpectedErrors(result, "AM023");
    }

    [Fact]
    public void ObservableCollection_ExcludedFromProjectionWithAM023()
    {
        var result = GeneratorTestHelper.Run(Source);

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d =>
            d.Id == "AM023" && d.GetMessage().Contains("Addresses") && d.GetMessage().Contains("ObservableCollection"));
        GeneratorTestHelper.AssertNoUnexpectedErrors(result, "AM023");
    }

    [Fact]
    public void ImmutableArrayUsing_OnlyAddedWhenActuallyUsed()
    {
        var result = GeneratorTestHelper.Run(@"
using AnvilMap;

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
        Assert.DoesNotContain("System.Collections.Immutable", result.GeneratedSource);
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void RuntimeMapping_MaterializesBothShapesWithCorrectValues()
    {
        var result = GeneratorTestHelper.Run(Source);
        Assert.NotNull(result.Assembly);

        var personType = result.Assembly!.GetType("Sample.Person")!;
        var addressType = result.Assembly!.GetType("Sample.Address")!;
        var personDtoType = result.Assembly!.GetType("Sample.PersonDto")!;

        var address = Activator.CreateInstance(addressType)!;
        addressType.GetProperty("City")!.SetValue(address, "Oslo");

        var addressList = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(addressType))!;
        addressList.Add(address);

        var person = Activator.CreateInstance(personType)!;
        personType.GetProperty("Tags")!.SetValue(person, new List<int> { 1, 2, 3 });
        personType.GetProperty("Addresses")!.SetValue(person, addressList);

        var toDto = result.Assembly!.GetType("AnvilMap.GeneratedMappings")!
            .GetMethod("ToPersonDto", new[] { personType });
        var dto = toDto!.Invoke(null, new[] { person })!;

        var tags = personDtoType.GetProperty("Tags")!.GetValue(dto)!;
        Assert.Equal(3, (int)tags.GetType().GetProperty("Length")!.GetValue(tags)!);

        var addresses = (System.Collections.ICollection)personDtoType.GetProperty("Addresses")!.GetValue(dto)!;
        Assert.Single(addresses);
    }
}

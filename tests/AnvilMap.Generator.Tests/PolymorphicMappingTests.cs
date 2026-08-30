namespace AnvilMap.Generator.Tests;

public class PolymorphicMappingTests
{
    private const string Source = @"
using AnvilMap;

namespace Sample;

[MapTo(typeof(AnimalDto))]
[MapInclude(typeof(AnimalDto), typeof(Dog), typeof(DogDto))]
[MapInclude(typeof(AnimalDto), typeof(Cat), typeof(CatDto))]
public class Animal
{
    public string Name { get; set; } = """";
}

[MapTo(typeof(DogDto))]
public class Dog : Animal
{
    public string Breed { get; set; } = """";
}

[MapTo(typeof(CatDto))]
public class Cat : Animal
{
    public bool IsIndoor { get; set; }
}

public class AnimalDto
{
    public string Name { get; set; } = """";
}

public class DogDto : AnimalDto
{
    public string Breed { get; set; } = """";
}

public class CatDto : AnimalDto
{
    public bool IsIndoor { get; set; }
}
";

    [Fact]
    public void EmitsTypeSwitchDispatchingToEachDerivedMapping()
    {
        var result = GeneratorTestHelper.Run(Source);

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("public static global::Sample.AnimalDto ToAnimalDto(this global::Sample.Animal source)", result.GeneratedSource);
        Assert.Contains("=> source switch", result.GeneratedSource);
        Assert.Contains("global::Sample.Dog d => d.ToDogDto(),", result.GeneratedSource);
        Assert.Contains("global::Sample.Cat d => d.ToCatDto(),", result.GeneratedSource);
        Assert.Contains("_ => source.ToAnimalDtoBase()", result.GeneratedSource);
        Assert.Contains("private static global::Sample.AnimalDto ToAnimalDtoBase(this global::Sample.Animal source)", result.GeneratedSource);
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void OmitsTwoArgOverloadAndReportsAM027()
    {
        var result = GeneratorTestHelper.Run(Source);

        Assert.DoesNotContain("ToAnimalDto(this global::Sample.Animal source, global::Sample.AnimalDto destination)", result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "AM027" && d.GetMessage().Contains("Animal"));
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void SkipsProjectionAndReportsAM028()
    {
        var result = GeneratorTestHelper.Run(Source);

        Assert.DoesNotContain("ProjectToAnimalDto", result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "AM028" && d.GetMessage().Contains("Animal"));
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void RuntimeMapping_DispatchesToTheCorrectDerivedType()
    {
        var result = GeneratorTestHelper.Run(Source);
        Assert.NotNull(result.Assembly);

        var dogType = result.Assembly!.GetType("Sample.Dog")!;
        var catType = result.Assembly!.GetType("Sample.Cat")!;
        var animalType = result.Assembly!.GetType("Sample.Animal")!;
        var dogDtoType = result.Assembly!.GetType("Sample.DogDto")!;
        var catDtoType = result.Assembly!.GetType("Sample.CatDto")!;

        var dog = Activator.CreateInstance(dogType)!;
        dogType.GetProperty("Name")!.SetValue(dog, "Rex");
        dogType.GetProperty("Breed")!.SetValue(dog, "Labrador");

        var cat = Activator.CreateInstance(catType)!;
        catType.GetProperty("Name")!.SetValue(cat, "Whiskers");
        catType.GetProperty("IsIndoor")!.SetValue(cat, true);

        var toAnimalDto = result.Assembly!.GetType("AnvilMap.GeneratedMappings")!
            .GetMethod("ToAnimalDto", new[] { animalType });

        var dogResult = toAnimalDto!.Invoke(null, new[] { dog });
        Assert.IsType(dogDtoType, dogResult);
        Assert.Equal("Rex", dogDtoType.GetProperty("Name")!.GetValue(dogResult));
        Assert.Equal("Labrador", dogDtoType.GetProperty("Breed")!.GetValue(dogResult));

        var catResult = toAnimalDto!.Invoke(null, new[] { cat });
        Assert.IsType(catDtoType, catResult);
        Assert.Equal("Whiskers", catDtoType.GetProperty("Name")!.GetValue(catResult));
        Assert.Equal(true, catDtoType.GetProperty("IsIndoor")!.GetValue(catResult));
    }

    [Fact]
    public void RuntimeMapping_BaseInstanceUsesBaseMapping()
    {
        var result = GeneratorTestHelper.Run(Source);
        Assert.NotNull(result.Assembly);

        var animalType = result.Assembly!.GetType("Sample.Animal")!;
        var animalDtoType = result.Assembly!.GetType("Sample.AnimalDto")!;

        var animal = Activator.CreateInstance(animalType)!;
        animalType.GetProperty("Name")!.SetValue(animal, "Generic");

        var toAnimalDto = result.Assembly!.GetType("AnvilMap.GeneratedMappings")!
            .GetMethod("ToAnimalDto", new[] { animalType });

        var animalResult = toAnimalDto!.Invoke(null, new[] { animal });
        Assert.IsType(animalDtoType, animalResult);
        Assert.Equal("Generic", animalDtoType.GetProperty("Name")!.GetValue(animalResult));
    }

    [Fact]
    public void GenerateReverseWithMapInclude_ReportsAM024AndSkipsReverse()
    {
        var result = GeneratorTestHelper.Run(@"
using AnvilMap;

namespace Sample;

[MapTo(typeof(AnimalDto), GenerateReverse = true)]
[MapInclude(typeof(AnimalDto), typeof(Dog), typeof(DogDto))]
public class Animal
{
    public string Name { get; set; } = """";
}

[MapTo(typeof(DogDto))]
public class Dog : Animal
{
    public string Breed { get; set; } = """";
}

public class AnimalDto
{
    public string Name { get; set; } = """";
}

public class DogDto : AnimalDto
{
    public string Breed { get; set; } = """";
}
");

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "AM024");
        Assert.DoesNotContain("ToAnimal(this global::Sample.AnimalDto", result.GeneratedSource);
        Assert.Contains("public static global::Sample.AnimalDto ToAnimalDto(this global::Sample.Animal source)", result.GeneratedSource);
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void DerivedTypeNotActuallyDerived_ReportsAM025AndSkipsThatInclude()
    {
        var result = GeneratorTestHelper.Run(@"
using AnvilMap;

namespace Sample;

[MapTo(typeof(AnimalDto))]
[MapInclude(typeof(AnimalDto), typeof(Unrelated), typeof(UnrelatedDto))]
public class Animal
{
    public string Name { get; set; } = """";
}

[MapTo(typeof(UnrelatedDto))]
public class Unrelated
{
    public string Name { get; set; } = """";
}

public class AnimalDto
{
    public string Name { get; set; } = """";
}

public class UnrelatedDto
{
    public string Name { get; set; } = """";
}
");

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "AM025");
        Assert.DoesNotContain("=> source switch", result.GeneratedSource);
        Assert.Contains("public static global::Sample.AnimalDto ToAnimalDto(this global::Sample.Animal source)", result.GeneratedSource);
        GeneratorTestHelper.AssertNoUnexpectedErrors(result, "AM025");
    }

    [Fact]
    public void DerivedPairMissingOwnMapping_ReportsAM026AndSkipsThatInclude()
    {
        var result = GeneratorTestHelper.Run(@"
using AnvilMap;

namespace Sample;

[MapTo(typeof(AnimalDto))]
[MapInclude(typeof(AnimalDto), typeof(Dog), typeof(DogDto))]
public class Animal
{
    public string Name { get; set; } = """";
}

public class Dog : Animal
{
    public string Breed { get; set; } = """";
}

public class AnimalDto
{
    public string Name { get; set; } = """";
}

public class DogDto : AnimalDto
{
    public string Breed { get; set; } = """";
}
");

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "AM026");
        Assert.DoesNotContain("=> source switch", result.GeneratedSource);
        GeneratorTestHelper.AssertNoUnexpectedErrors(result, "AM026");
    }

    [Fact]
    public void OneInvalidIncludeAmongValidOnes_OnlyTheInvalidOneIsSkipped()
    {
        var result = GeneratorTestHelper.Run(@"
using AnvilMap;

namespace Sample;

[MapTo(typeof(AnimalDto))]
[MapInclude(typeof(AnimalDto), typeof(Dog), typeof(DogDto))]
[MapInclude(typeof(AnimalDto), typeof(Cat), typeof(CatDto))]
public class Animal
{
    public string Name { get; set; } = """";
}

[MapTo(typeof(DogDto))]
public sealed class Dog : Animal
{
    public string Breed { get; set; } = """";
}

public sealed class Cat : Animal
{
    public bool IsIndoor { get; set; }
}

public class AnimalDto
{
    public string Name { get; set; } = """";
}

public sealed class DogDto : AnimalDto
{
    public string Breed { get; set; } = """";
}

public sealed class CatDto : AnimalDto
{
    public bool IsIndoor { get; set; }
}
");

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "AM026" && d.GetMessage().Contains("Cat"));
        Assert.Contains("global::Sample.Dog d => d.ToDogDto(),", result.GeneratedSource);
        Assert.DoesNotContain("ToCatDto(),", result.GeneratedSource);
        GeneratorTestHelper.AssertNoUnexpectedErrors(result, "AM026");
    }

    [Fact]
    public void NestedReferenceToPolymorphicMapping_ExcludedFromOuterProjectionToo()
    {
        var result = GeneratorTestHelper.Run(@"
using AnvilMap;

namespace Sample;

[MapTo(typeof(AnimalDto))]
[MapInclude(typeof(AnimalDto), typeof(Dog), typeof(DogDto))]
public class Animal
{
    public string Name { get; set; } = """";
}

[MapTo(typeof(DogDto))]
public class Dog : Animal
{
    public string Breed { get; set; } = """";
}

public class AnimalDto
{
    public string Name { get; set; } = """";
}

public class DogDto : AnimalDto
{
    public string Breed { get; set; } = """";
}

[MapTo(typeof(ShelterDto))]
public class Shelter
{
    public Animal Pet { get; set; } = null!;
}

public class ShelterDto
{
    public AnimalDto Pet { get; set; } = null!;
}
");

        Assert.DoesNotContain("ProjectToShelterDto", result.GeneratedSource);
        Assert.DoesNotContain("ProjectToAnimalDto", result.GeneratedSource);
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void DerivedSourceDerivesButDerivedDestinationDoesNot_ReportsAM025()
    {
        var result = GeneratorTestHelper.Run(@"
using AnvilMap;

namespace Sample;

[MapTo(typeof(AnimalDto))]
[MapInclude(typeof(AnimalDto), typeof(Dog), typeof(DogDto))]
public class Animal
{
    public string Name { get; set; } = """";
}

[MapTo(typeof(DogDto))]
public class Dog : Animal
{
    public string Breed { get; set; } = """";
}

public class AnimalDto
{
    public string Name { get; set; } = """";
}

public class DogDto
{
    public string Breed { get; set; } = """";
}
");

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "AM025");
        Assert.DoesNotContain("=> source switch", result.GeneratedSource);
        GeneratorTestHelper.AssertNoUnexpectedErrors(result, "AM025");
    }

    [Fact]
    public void TransitivelyDerivedType_ReportsAM025_SingleLevelOnly()
    {
        var result = GeneratorTestHelper.Run(@"
using AnvilMap;

namespace Sample;

[MapTo(typeof(AnimalDto))]
[MapInclude(typeof(AnimalDto), typeof(Puppy), typeof(PuppyDto))]
public class Animal
{
    public string Name { get; set; } = """";
}

public class Dog : Animal
{
    public string Breed { get; set; } = """";
}

[MapTo(typeof(PuppyDto))]
public sealed class Puppy : Dog
{
    public int AgeWeeks { get; set; }
}

public class AnimalDto
{
    public string Name { get; set; } = """";
}

public class DogDto : AnimalDto
{
    public string Breed { get; set; } = """";
}

public sealed class PuppyDto : DogDto
{
    public int AgeWeeks { get; set; }
}
");

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "AM025");
        Assert.DoesNotContain("=> source switch", result.GeneratedSource);
        GeneratorTestHelper.AssertNoUnexpectedErrors(result, "AM025");
    }

    [Fact]
    public void DuplicateIncludeForSameDerivedSource_ReportsAM029AndKeepsOnlyOneArm()
    {
        var result = GeneratorTestHelper.Run(@"
using AnvilMap;

namespace Sample;

[MapTo(typeof(AnimalDto))]
[MapInclude(typeof(AnimalDto), typeof(Dog), typeof(DogDto))]
[MapInclude(typeof(AnimalDto), typeof(Dog), typeof(DogDto2))]
public class Animal
{
    public string Name { get; set; } = """";
}

[MapTo(typeof(DogDto))]
[MapTo(typeof(DogDto2))]
public sealed class Dog : Animal
{
    public string Breed { get; set; } = """";
}

public class AnimalDto
{
    public string Name { get; set; } = """";
}

public sealed class DogDto : AnimalDto
{
    public string Breed { get; set; } = """";
}

public sealed class DogDto2 : AnimalDto
{
    public string Breed { get; set; } = """";
}
");

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "AM029" && d.GetMessage().Contains("Dog"));
        Assert.Contains("global::Sample.Dog d => d.ToDogDto2(),", result.GeneratedSource);
        Assert.DoesNotContain("d.ToDogDto(),", result.GeneratedSource);

        var occurrences = System.Text.RegularExpressions.Regex.Matches(result.GeneratedSource!, "global::Sample.Dog d =>").Count;
        Assert.Equal(1, occurrences);

        GeneratorTestHelper.AssertNoUnexpectedErrors(result, "AM029");
    }

    [Fact]
    public void DispatcherOneArg_TransparentlyDispatchesToTheCorrectDerivedType()
    {
        var result = GeneratorTestHelper.Run(Source);
        Assert.NotNull(result.Assembly);

        var dogType = result.Assembly!.GetType("Sample.Dog")!;
        var animalType = result.Assembly!.GetType("Sample.Animal")!;
        var dogDtoType = result.Assembly!.GetType("Sample.DogDto")!;

        var dog = Activator.CreateInstance(dogType)!;
        dogType.GetProperty("Name")!.SetValue(dog, "Rex");
        dogType.GetProperty("Breed")!.SetValue(dog, "Labrador");

        var mapMethod = result.Assembly!.GetType("AnvilMap.GeneratedMappings")!
            .GetMethod("Map", genericParameterCount: 1, new[] { typeof(object) })!
            .MakeGenericMethod(result.Assembly!.GetType("Sample.AnimalDto")!);

        var dogResult = mapMethod.Invoke(null, new[] { dog });
        Assert.IsType(dogDtoType, dogResult);
        Assert.Equal("Rex", dogDtoType.GetProperty("Name")!.GetValue(dogResult));
        Assert.Equal("Labrador", dogDtoType.GetProperty("Breed")!.GetValue(dogResult));
    }

    [Fact]
    public void DispatcherTwoArg_ThrowsBecauseTwoArgOverloadWasOmitted()
    {
        var result = GeneratorTestHelper.Run(Source);
        Assert.NotNull(result.Assembly);

        var animalType = result.Assembly!.GetType("Sample.Animal")!;
        var animalDtoType = result.Assembly!.GetType("Sample.AnimalDto")!;
        var animal = Activator.CreateInstance(animalType)!;
        var existingDto = Activator.CreateInstance(animalDtoType)!;

        var mapIntoMethod = result.Assembly!.GetType("AnvilMap.GeneratedMappings")!
            .GetMethod(
                "Map",
                genericParameterCount: 2,
                new[] { System.Type.MakeGenericMethodParameter(0), System.Type.MakeGenericMethodParameter(1) })!
            .MakeGenericMethod(animalType, animalDtoType);

        var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
            mapIntoMethod.Invoke(null, new[] { animal, existingDto }));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void DeclaredViaMapFromOnTheBaseDestination_StillDispatchesCorrectly()
    {
        var result = GeneratorTestHelper.Run(@"
using AnvilMap;

namespace Sample;

public class Animal
{
    public string Name { get; set; } = """";
}

[MapTo(typeof(DogDto))]
public sealed class Dog : Animal
{
    public string Breed { get; set; } = """";
}

[MapFrom(typeof(Animal))]
[MapInclude(typeof(Animal), typeof(Dog), typeof(DogDto))]
public class AnimalDto
{
    public string Name { get; set; } = """";
}

public sealed class DogDto : AnimalDto
{
    public string Breed { get; set; } = """";
}
");

        Assert.NotNull(result.Assembly);
        Assert.Contains("global::Sample.Dog d => d.ToDogDto(),", result.GeneratedSource);

        var dogType = result.Assembly!.GetType("Sample.Dog")!;
        var dogDtoType = result.Assembly!.GetType("Sample.DogDto")!;
        var dog = Activator.CreateInstance(dogType)!;
        dogType.GetProperty("Breed")!.SetValue(dog, "Poodle");

        var toAnimalDto = result.Assembly!.GetType("AnvilMap.GeneratedMappings")!
            .GetMethod("ToAnimalDto", new[] { result.Assembly!.GetType("Sample.Animal")! });
        var dogResult = toAnimalDto!.Invoke(null, new[] { dog });

        Assert.IsType(dogDtoType, dogResult);
        Assert.Equal("Poodle", dogDtoType.GetProperty("Breed")!.GetValue(dogResult));
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void MaxDepthCombinedWithMapInclude_ReportsAM020InsteadOfSilentlyIgnoringIt()
    {
        // Not re-run against a real cyclic graph here - that crashes the test host outright.
        var result = GeneratorTestHelper.Run(@"
using AnvilMap;
using System.Collections.Generic;

namespace Sample;

[MapTo(typeof(AnimalDto), MaxDepth = 2)]
[MapInclude(typeof(AnimalDto), typeof(Dog), typeof(DogDto))]
public class Animal
{
    public string Name { get; set; } = """";
    public List<Animal> Friends { get; set; } = new();
}

[MapTo(typeof(DogDto))]
public sealed class Dog : Animal
{
    public string Breed { get; set; } = """";
}

public class AnimalDto
{
    public string Name { get; set; } = """";
    public List<AnimalDto> Friends { get; set; } = new();
}

public sealed class DogDto : AnimalDto
{
    public string Breed { get; set; } = """";
}
");

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "AM020" && d.GetMessage().Contains("MapInclude"));
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }
}

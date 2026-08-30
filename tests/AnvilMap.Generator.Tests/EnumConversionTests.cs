namespace AnvilMap.Generator.Tests;

public class EnumConversionTests
{
    private const string Source = @"
using AnvilMap;

namespace Sample;

public enum Status : int
{
    Active = 1,
    Inactive = 2
}

[MapTo(typeof(StatusDto))]
public sealed class StatusEntity
{
    public Status State { get; set; }
    public Status Code { get; set; }
}

public sealed class StatusDto
{
    public string State { get; set; } = """";
    public int Code { get; set; }
}
";

    [Fact]
    public void EnumToUnderlyingType_EmitsCastAndCompiles()
    {
        var result = GeneratorTestHelper.Run(Source);

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.Code = (int)source.Code;", result.GeneratedSource);
        Assert.Contains("Code = (int)source.Code", result.GeneratedSource);
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void EnumToString_EmitsToStringCallAndCompiles()
    {
        var result = GeneratorTestHelper.Run(Source);

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.State = source.State.ToString();", result.GeneratedSource);
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void EnumToString_ExcludedFromProjectionWithAM022()
    {
        var result = GeneratorTestHelper.Run(Source);

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("StatusEntityToStatusDtoProjection = source => new global::Sample.StatusDto { Code = (int)source.Code };", result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "AM022" && d.GetMessage().Contains("State"));
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void EnumToUnderlyingType_IncludedInProjectionAsCast()
    {
        var result = GeneratorTestHelper.Run(Source);

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("Code = (int)source.Code", result.GeneratedSource);
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void RuntimeMapping_ConvertsEnumCorrectly()
    {
        var result = GeneratorTestHelper.Run(Source);
        Assert.NotNull(result.Assembly);

        var entityType = result.Assembly!.GetType("Sample.StatusEntity")!;
        var dtoType = result.Assembly!.GetType("Sample.StatusDto")!;
        var statusType = result.Assembly!.GetType("Sample.Status")!;

        var entity = Activator.CreateInstance(entityType)!;
        entityType.GetProperty("State")!.SetValue(entity, Enum.Parse(statusType, "Active"));
        entityType.GetProperty("Code")!.SetValue(entity, Enum.Parse(statusType, "Inactive"));

        var toDto = result.Assembly!.GetType("AnvilMap.GeneratedMappings")!
            .GetMethod("ToStatusDto", new[] { entityType });
        var dto = toDto!.Invoke(null, new[] { entity });

        Assert.Equal("Active", dtoType.GetProperty("State")!.GetValue(dto));
        Assert.Equal(2, dtoType.GetProperty("Code")!.GetValue(dto));
    }

    [Fact]
    public void MismatchedUnderlyingType_StillReportsAM003()
    {
        var result = GeneratorTestHelper.Run(@"
using AnvilMap;

namespace Sample;

public enum Status : int { Active }

[MapTo(typeof(Dto))]
public sealed class Entity
{
    public Status State { get; set; }
}

public sealed class Dto
{
    public long State { get; set; }
}
");

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "AM003" && d.GetMessage().Contains("State"));
        Assert.DoesNotContain("(long)source.State", result.GeneratedSource);
        GeneratorTestHelper.AssertNoUnexpectedErrors(result, "AM003");
    }

    [Fact]
    public void NonIntUnderlyingType_StillMatchesExactly()
    {
        var result = GeneratorTestHelper.Run(@"
using AnvilMap;

namespace Sample;

public enum Flags : byte { None = 0, A = 1 }

[MapTo(typeof(Dto))]
public sealed class Entity
{
    public Flags State { get; set; }
}

public sealed class Dto
{
    public byte State { get; set; }
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.State = (byte)source.State;", result.GeneratedSource);
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void MapCondition_StillGuardsAnEnumConversionProperty()
    {
        var result = GeneratorTestHelper.Run(@"
using AnvilMap;

namespace Sample;

public enum Status : int { Active, Inactive }

[MapTo(typeof(Dto))]
[MapCondition(typeof(Dto), nameof(Dto.State), nameof(ShouldMap))]
public sealed class Entity
{
    public Status State { get; set; }

    public static bool ShouldMap(Entity source) => source.State == Status.Active;
}

public sealed class Dto
{
    public string State { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("if (global::Sample.Entity.ShouldMap(source))", result.GeneratedSource);
        Assert.Contains("destination.State = source.State.ToString();", result.GeneratedSource);
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void ExplicitMapUsing_OverridesTheBuiltInConversion_AndIsIncludedInProjection()
    {
        var result = GeneratorTestHelper.Run(@"
using AnvilMap;

namespace Sample;

public enum Status : int { Active, Inactive }

[MapTo(typeof(Dto))]
[MapUsing(typeof(Dto), nameof(Dto.State), nameof(ToLabel))]
public sealed class Entity
{
    public Status State { get; set; }

    public static string ToLabel(Entity source) => source.State == Status.Active ? ""on"" : ""off"";
}

public sealed class Dto
{
    public string State { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "AM022");
        Assert.DoesNotContain("source.State.ToString()", result.GeneratedSource);
        Assert.Contains("destination.State = global::Sample.Entity.ToLabel(source);", result.GeneratedSource);
        Assert.Contains("EntityToDtoProjection = source => new global::Sample.Dto { State = global::Sample.Entity.ToLabel(source) };", result.GeneratedSource);
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void NullableEnumSource_StillReportsAM003()
    {
        var result = GeneratorTestHelper.Run(@"
using AnvilMap;

namespace Sample;

public enum Status : int { Active }

[MapTo(typeof(Dto))]
public sealed class Entity
{
    public Status? State { get; set; }
}

public sealed class Dto
{
    public string State { get; set; } = """";
}
");

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "AM003" && d.GetMessage().Contains("State"));
        Assert.DoesNotContain(".ToString()", result.GeneratedSource);
        GeneratorTestHelper.AssertNoUnexpectedErrors(result, "AM003");
    }
}

namespace AnvilMap.Generator.Tests;

// [MapUsing]'s InlineInProjection opt-in - splices an eligible converter's body into the
// projection instead of calling it.
public class MapUsingInlineProjectionTests
{
    [Fact]
    public void ExpressionBodiedConverter_SplicesBodyInsteadOfCallingIt()
    {
        var result = GeneratorTestHelper.Run(@"
using AnvilMap;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapUsing(typeof(UserDto), nameof(UserDto.FullName), nameof(ComputeFullName), InlineInProjection = true)]
public sealed class User
{
    public string FirstName { get; set; } = """";
    public string LastName { get; set; } = """";

    public static string ComputeFullName(User entity) => entity.FirstName + "" "" + entity.LastName;
}

public sealed class UserDto
{
    public string FullName { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(
            "UserToUserDtoProjection = source => new global::Sample.UserDto { FullName = (source).FirstName + \" \" + (source).LastName };",
            result.GeneratedSource);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "AM030");
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void ImperativeMapperStillCallsTheConverterMethod()
    {
        var result = GeneratorTestHelper.Run(@"
using AnvilMap;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapUsing(typeof(UserDto), nameof(UserDto.FullName), nameof(ComputeFullName), InlineInProjection = true)]
public sealed class User
{
    public string FirstName { get; set; } = """";
    public string LastName { get; set; } = """";

    public static string ComputeFullName(User entity) => entity.FirstName + "" "" + entity.LastName;
}

public sealed class UserDto
{
    public string FullName { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("destination.FullName = global::Sample.User.ComputeFullName(source);", result.GeneratedSource);
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void BlockBodyWithExactlyOneReturn_AlsoEligible()
    {
        var result = GeneratorTestHelper.Run(@"
using AnvilMap;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapUsing(typeof(UserDto), nameof(UserDto.FullName), nameof(ComputeFullName), InlineInProjection = true)]
public sealed class User
{
    public string FirstName { get; set; } = """";
    public string LastName { get; set; } = """";

    public static string ComputeFullName(User entity)
    {
        return entity.FirstName + "" "" + entity.LastName;
    }
}

public sealed class UserDto
{
    public string FullName { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(
            "UserToUserDtoProjection = source => new global::Sample.UserDto { FullName = (source).FirstName + \" \" + (source).LastName };",
            result.GeneratedSource);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "AM030");
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void MultiStatementBody_ReportsAM030AndFallsBackToMethodCall()
    {
        var result = GeneratorTestHelper.Run(@"
using AnvilMap;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapUsing(typeof(UserDto), nameof(UserDto.FullName), nameof(ComputeFullName), InlineInProjection = true)]
public sealed class User
{
    public string FirstName { get; set; } = """";
    public string LastName { get; set; } = """";

    public static string ComputeFullName(User entity)
    {
        var full = entity.FirstName + "" "" + entity.LastName;
        return full;
    }
}

public sealed class UserDto
{
    public string FullName { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(
            "UserToUserDtoProjection = source => new global::Sample.UserDto { FullName = global::Sample.User.ComputeFullName(source) };",
            result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "AM030" && d.GetMessage().Contains("FullName") &&
            d.GetMessage().Contains("single expression"));
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void NameofInBody_ReportsAM030AndFallsBackToMethodCall()
    {
        var result = GeneratorTestHelper.Run(@"
using AnvilMap;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapUsing(typeof(UserDto), nameof(UserDto.FullName), nameof(Describe), InlineInProjection = true)]
public sealed class User
{
    public string FirstName { get; set; } = """";

    public static string Describe(User entity) => nameof(entity.FirstName);
}

public sealed class UserDto
{
    public string FullName { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(
            "UserToUserDtoProjection = source => new global::Sample.UserDto { FullName = global::Sample.User.Describe(source) };",
            result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "AM030" && d.GetMessage().Contains("nameof"));
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void GenericTypeReferenceInBody_ReportsAM030AndFallsBackToMethodCall()
    {
        var result = GeneratorTestHelper.Run(@"
using AnvilMap;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapUsing(typeof(UserDto), nameof(UserDto.NameLength), nameof(CountNames), InlineInProjection = true)]
public sealed class User
{
    public string FirstName { get; set; } = """";

    public static int CountNames(User entity) => new System.Collections.Generic.List<string> { entity.FirstName }.Count;
}

public sealed class UserDto
{
    public int NameLength { get; set; }
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(
            "UserToUserDtoProjection = source => new global::Sample.UserDto { NameLength = global::Sample.User.CountNames(source) };",
            result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "AM030" && d.GetMessage().Contains("generic"));
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void ExternConverterWithNoBody_ReportsAM030NoAvailableSource()
    {
        var result = GeneratorTestHelper.Run(@"
using System.Runtime.InteropServices;
using AnvilMap;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapUsing(typeof(UserDto), nameof(UserDto.FullName), nameof(ComputeFullName), InlineInProjection = true)]
public sealed class User
{
    public string FirstName { get; set; } = """";

    [DllImport(""nonexistent.dll"")]
    public static extern string ComputeFullName(User entity);
}

public sealed class UserDto
{
    public string FullName { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(
            "UserToUserDtoProjection = source => new global::Sample.UserDto { FullName = global::Sample.User.ComputeFullName(source) };",
            result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "AM030" && d.GetMessage().Contains("no available source"));
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void ReferencesSiblingPublicStaticHelper_FullyQualifiesItSoItStillCompilesOnceSpliced()
    {
        var result = GeneratorTestHelper.Run(@"
using AnvilMap;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapUsing(typeof(UserDto), nameof(UserDto.FullName), nameof(ComputeFullName), InlineInProjection = true)]
public sealed class User
{
    public string FirstName { get; set; } = """";

    public static string ComputeFullName(User entity) => Normalize(entity.FirstName);

    public static string Normalize(string value) => value.Trim();
}

public sealed class UserDto
{
    public string FullName { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(
            "UserToUserDtoProjection = source => new global::Sample.UserDto { FullName = global::Sample.User.Normalize((source).FirstName) };",
            result.GeneratedSource);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "AM030");
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void ReferencesSiblingPrivateStaticHelper_ReportsAM030AndFallsBackToMethodCall()
    {
        var result = GeneratorTestHelper.Run(@"
using AnvilMap;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapUsing(typeof(UserDto), nameof(UserDto.FullName), nameof(ComputeFullName), InlineInProjection = true)]
public sealed class User
{
    public string FirstName { get; set; } = """";

    public static string ComputeFullName(User entity) => Normalize(entity.FirstName);

    private static string Normalize(string value) => value.Trim();
}

public sealed class UserDto
{
    public string FullName { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(
            "UserToUserDtoProjection = source => new global::Sample.UserDto { FullName = global::Sample.User.ComputeFullName(source) };",
            result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "AM030" && d.GetMessage().Contains("private"));
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void ConverterIgnoringItsParameter_StillInlinesWithNoSubstitution()
    {
        var result = GeneratorTestHelper.Run(@"
using AnvilMap;

namespace Sample;

[MapTo(typeof(UserDto))]
[MapUsing(typeof(UserDto), nameof(UserDto.Label), nameof(ComputeLabel), InlineInProjection = true)]
public sealed class User
{
    public string FirstName { get; set; } = """";

    public static string ComputeLabel(User entity) => ""N/A"";
}

public sealed class UserDto
{
    public string Label { get; set; } = """";
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(
            "UserToUserDtoProjection = source => new global::Sample.UserDto { Label = \"N/A\" };",
            result.GeneratedSource);
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }

    [Fact]
    public void DeclaredViaMapFromOnTheDestination_StillInlinesCorrectly()
    {
        var result = GeneratorTestHelper.Run(@"
using AnvilMap;

namespace Sample;

public sealed class User
{
    public string FirstName { get; set; } = """";
    public string LastName { get; set; } = """";
}

[MapFrom(typeof(User))]
[MapUsing(typeof(User), nameof(UserDto.FullName), nameof(ComputeFullName), InlineInProjection = true)]
public sealed class UserDto
{
    public string FullName { get; set; } = """";

    public static string ComputeFullName(User entity) => entity.FirstName + "" "" + entity.LastName;
}
");

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(
            "UserToUserDtoProjection = source => new global::Sample.UserDto { FullName = (source).FirstName + \" \" + (source).LastName };",
            result.GeneratedSource);
        GeneratorTestHelper.AssertNoUnexpectedErrors(result);
    }
}

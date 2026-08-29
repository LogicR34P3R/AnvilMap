using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AnvilMap.Generator.Tests;

public class XmlDocCommentTests
{
    private const string Source = @"
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
";

    [Fact]
    public void ToDestMethods_HaveSummaryDocComments()
    {
        var result = GeneratorTestHelper.Run(Source);

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(
            "/// <summary>Maps a <c>Sample.User</c> to a new <c>Sample.UserDto</c>.</summary>",
            result.GeneratedSource);
        Assert.Contains(
            "/// <summary>Maps a <c>Sample.User</c> onto an existing <c>Sample.UserDto</c> instance.</summary>",
            result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void ProjectToDestMethod_HasSummaryDocComment()
    {
        var result = GeneratorTestHelper.Run(Source);

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(
            "/// <summary>Projects a queryable of <c>Sample.User</c> to <c>Sample.UserDto</c>, " +
            "translatable by the query provider (e.g. EF Core).</summary>",
            result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void GenericDispatcherMapMethods_HaveSummaryDocComments()
    {
        var result = GeneratorTestHelper.Run(Source);

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("/// <summary>Maps <paramref name=\"source\"/> to a new <typeparamref name=\"TDestination\"/> instance, resolved by its runtime type.</summary>", result.GeneratedSource);
        Assert.Contains("/// <summary>Maps <paramref name=\"source\"/> to a new <typeparamref name=\"TDestination\"/> instance.</summary>", result.GeneratedSource);
        Assert.Contains("/// <summary>Maps <paramref name=\"source\"/> into the existing <paramref name=\"destination\"/> instance, overwriting its mapped properties in place.</summary>", result.GeneratedSource);
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void AnvilMapService_MapMethods_UseInheritdoc()
    {
        var result = GeneratorTestHelper.Run(Source);

        Assert.NotNull(result.GeneratedSource);
        Assert.Equal(3, CountOccurrences(result.GeneratedSource!, "/// <inheritdoc/>"));
        AssertNoCompileErrors(result);
    }

    [Fact]
    public void InterceptorMethods_HaveNoDocComments()
    {
        const string source = @"
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

public static class Caller
{
    public static UserDto CallDirect(User user) => GeneratedMappings.Map<User, UserDto>(user);
}
";
        var references = GeneratorTestHelper.PlatformReferences
            .Append(MetadataReference.CreateFromFile(typeof(MapToAttribute).Assembly.Location))
            .ToArray();
        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp14)
            .WithFeatures(new[] { new KeyValuePair<string, string>("InterceptorsNamespaces", "AnvilMap") });

        var result = GeneratorTestHelper.Run(source, references, parseOptions);

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("file static class Interceptors", result.GeneratedSource);

        var interceptorsBody = result.GeneratedSource![result.GeneratedSource.IndexOf("file static class Interceptors", StringComparison.Ordinal)..];
        Assert.DoesNotContain("/// <summary>", interceptorsBody);
        AssertNoCompileErrors(result);
    }

    private static int CountOccurrences(string text, string token)
    {
        var count = 0;
        var index = 0;

        while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

    private static void AssertNoCompileErrors(GeneratorTestResult result)
    {
        var errors = result.CompilationDiagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.True(errors.Count == 0, string.Join("\n", errors.Select(e => e.ToString())));
    }
}

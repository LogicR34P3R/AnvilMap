using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace GeneratedMapper.Generator.Tests;

/// <summary>
/// Interceptor-based direct dispatch. A direct <c>GeneratedMappings.Map&lt;TSource,
/// TDestination&gt;(...)</c> call, statically visible to the generator, should get an
/// <c>[InterceptsLocation]</c>-attributed redirect straight to <c>To{Dest}()</c> when the
/// consumer is on C# 14 with interceptors enabled - never for calls through <see cref="IMapper"/>,
/// since interceptors redirect by source location regardless of the runtime type behind an
/// interface receiver, which would silently defeat mocking/DI substitution for IMapper.
/// </summary>
public class InterceptorDispatchTests
{
    private const string DirectAndInterfaceCallSource = @"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}

public sealed class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}

public static class Caller
{
    public static UserDto CallDirect(User user) => GeneratedMappings.Map<User, UserDto>(user);

    public static UserDto CallViaIMapper(IMapper mapper, User user) => mapper.Map<User, UserDto>(user);
}
";

    private const string TwoArgCallSource = @"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}

public sealed class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}

public static class Caller
{
    public static UserDto CallDirect(User user, UserDto destination) => GeneratedMappings.Map<User, UserDto>(user, destination);
}
";

    private const string TwoArgInitOnlyDestinationSource = @"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}

public sealed record UserDto
{
    public int Id { get; init; }
    public string Name { get; init; } = """";
}

public static class Caller
{
    public static UserDto CallDirectTwoArg(User user, UserDto destination) => GeneratedMappings.Map<User, UserDto>(user, destination);
}
";

    private const string TwoCallSitesSamePairSource = @"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}

public sealed class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}

public static class Caller
{
    public static UserDto CallDirect1(User user) => GeneratedMappings.Map<User, UserDto>(user);

    public static UserDto CallDirect2(User user) => GeneratedMappings.Map<User, UserDto>(user);
}
";

    private const string TwoDifferentPairsSource = @"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}

public sealed class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}

[MapTo(typeof(OrderDto))]
public sealed class Order
{
    public int Total { get; set; }
}

public sealed class OrderDto
{
    public int Total { get; set; }
}

public static class Caller
{
    public static UserDto CallUser(User user) => GeneratedMappings.Map<User, UserDto>(user);

    public static OrderDto CallOrder(Order order) => GeneratedMappings.Map<Order, OrderDto>(order);
}
";

    private const string UnmappedDestinationSource = @"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}

public sealed class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}

public sealed class UnrelatedType
{
    public int Value { get; set; }
}

public static class Caller
{
    public static UserDto CallMapped(User user) => GeneratedMappings.Map<User, UserDto>(user);

    public static UnrelatedType CallUnmapped(User user) => GeneratedMappings.Map<User, UnrelatedType>(user);
}
";

    private const string OpenGenericWrapperCallSource = @"
using GeneratedMapper;

namespace Sample;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}

public sealed class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = """";
}

public static class Caller
{
    // TEntity/TDto here are THIS method's own open type parameters, not closed types - even
    // though every actual call site of MapSomething below happens to use the same (User, UserDto)
    // pair, the Map<TEntity, TDto>(entity) call inside this method body can never be intercepted:
    // there's no single closed-generic interceptor that could correctly stand in for every
    // possible instantiation of MapSomething.
    public static TDto MapSomething<TEntity, TDto>(TEntity entity) => GeneratedMappings.Map<TEntity, TDto>(entity);

    public static UserDto CallThroughWrapper(User user) => MapSomething<User, UserDto>(user);
}
";

    private static CSharpParseOptions InterceptorsEnabled(LanguageVersion languageVersion)
        => new CSharpParseOptions(languageVersion)
            .WithFeatures(new[] { new KeyValuePair<string, string>("InterceptorsNamespaces", "GeneratedMapper") });

    private static MetadataReference[] References()
        => GeneratorTestHelper.PlatformReferences
            .Append(MetadataReference.CreateFromFile(typeof(MapToAttribute).Assembly.Location))
            .ToArray();

    [Fact]
    public void DirectStaticCall_CSharp14WithInterceptorsEnabled_EmitsInterceptorAndCompiles()
    {
        var result = GeneratorTestHelper.Run(DirectAndInterfaceCallSource, References(), InterceptorsEnabled(LanguageVersion.CSharp14));

        Assert.Contains("file static class Interceptors", result.GeneratedSource);
        Assert.Contains("System.Runtime.CompilerServices.InterceptsLocation(", result.GeneratedSource);
        Assert.Contains("Intercepted_UserDto_", result.GeneratedSource);
        Assert.Empty(result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        Assert.NotNull(result.Assembly);
        var callerType = result.Assembly!.GetType("Sample.Caller")!;
        var userType = result.Assembly!.GetType("Sample.User")!;
        var user = Activator.CreateInstance(userType)!;
        userType.GetProperty("Id")!.SetValue(user, 9);
        userType.GetProperty("Name")!.SetValue(user, "Ada");

        var dto = callerType.GetMethod("CallDirect")!.Invoke(null, new[] { user });

        var userDtoType = result.Assembly!.GetType("Sample.UserDto")!;
        Assert.Equal(9, userDtoType.GetProperty("Id")!.GetValue(dto));
        Assert.Equal("Ada", userDtoType.GetProperty("Name")!.GetValue(dto));
    }

    [Fact]
    public void DirectStaticCall_PreCSharp14_NoInterceptorEmitted_DictionaryPathUnchanged()
    {
        // Pinned below CSharp14 - interceptor emission is gated on capabilities.UseCSharp14, so
        // nothing here should change relative to today's dictionary-dispatch-only output, even
        // though the exact same direct-call source is present.
        var result = GeneratorTestHelper.Run(DirectAndInterfaceCallSource, References(), new CSharpParseOptions(LanguageVersion.CSharp12));

        Assert.DoesNotContain("InterceptsLocation", result.GeneratedSource);
        Assert.DoesNotContain("class Interceptors", result.GeneratedSource);
        Assert.Empty(result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.NotNull(result.Assembly);
    }

    [Fact]
    public void IMapperTypedCall_NeverIntercepted_OnlyTheDirectStaticCallIs()
    {
        // Same source has both a direct GeneratedMappings.Map<,> call and an IMapper.Map<,> call
        // through an interface-typed parameter. Exactly one [InterceptsLocation] should appear -
        // the direct call's - never one for the IMapper-typed call, regardless of C# 14 being on.
        var result = GeneratorTestHelper.Run(DirectAndInterfaceCallSource, References(), InterceptorsEnabled(LanguageVersion.CSharp14));

        Assert.Empty(result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var interceptsCount = CountOccurrences(result.GeneratedSource!, "InterceptsLocation(");
        Assert.Equal(1, interceptsCount);
    }

    [Fact]
    public void TwoArgOverload_CSharp14WithInterceptorsEnabled_EmitsInterceptorAndCompiles()
    {
        var result = GeneratorTestHelper.Run(TwoArgCallSource, References(), InterceptorsEnabled(LanguageVersion.CSharp14));

        Assert.Contains("file static class Interceptors", result.GeneratedSource);
        Assert.Contains("System.Runtime.CompilerServices.InterceptsLocation(", result.GeneratedSource);
        Assert.Empty(result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        Assert.NotNull(result.Assembly);
        var callerType = result.Assembly!.GetType("Sample.Caller")!;
        var userType = result.Assembly!.GetType("Sample.User")!;
        var userDtoType = result.Assembly!.GetType("Sample.UserDto")!;
        var user = Activator.CreateInstance(userType)!;
        userType.GetProperty("Id")!.SetValue(user, 3);
        userType.GetProperty("Name")!.SetValue(user, "Grace");
        var destination = Activator.CreateInstance(userDtoType)!;

        var dto = callerType.GetMethod("CallDirect")!.Invoke(null, new[] { user, destination });

        Assert.Same(destination, dto);
        Assert.Equal(3, userDtoType.GetProperty("Id")!.GetValue(dto));
        Assert.Equal("Grace", userDtoType.GetProperty("Name")!.GetValue(dto));
    }

    [Fact]
    public void OpenGenericTypeParameterCall_NeverIntercepted_FallsBackToDictionaryDispatch()
    {
        // A Map<TEntity, TDto>(entity) call written inside a generic method, using that method's
        // own type parameters, is never a candidate for interception - TryGetInterceptedMapCall
        // explicitly bails out when either resolved type argument is TypeKind.TypeParameter. This
        // proves that guard actually holds: no interceptor for this call site, and the closed
        // instantiation used at the actual call site (User, UserDto) still works correctly through
        // the untouched dictionary dispatch.
        var result = GeneratorTestHelper.Run(OpenGenericWrapperCallSource, References(), InterceptorsEnabled(LanguageVersion.CSharp14));

        Assert.DoesNotContain("InterceptsLocation", result.GeneratedSource);
        Assert.DoesNotContain("class Interceptors", result.GeneratedSource);
        Assert.Empty(result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        Assert.NotNull(result.Assembly);
        var callerType = result.Assembly!.GetType("Sample.Caller")!;
        var userType = result.Assembly!.GetType("Sample.User")!;
        var user = Activator.CreateInstance(userType)!;
        userType.GetProperty("Id")!.SetValue(user, 21);
        userType.GetProperty("Name")!.SetValue(user, "Katherine");

        var dto = callerType.GetMethod("CallThroughWrapper")!.Invoke(null, new[] { user });

        var userDtoType = result.Assembly!.GetType("Sample.UserDto")!;
        Assert.Equal(21, userDtoType.GetProperty("Id")!.GetValue(dto));
        Assert.Equal("Katherine", userDtoType.GetProperty("Name")!.GetValue(dto));
    }

    [Fact]
    public void TwoArgOverload_InitOnlyDestination_NeverIntercepted_DictionaryPathThrowsUnchanged()
    {
        // GM008: no two-arg To{Dest}(source, destination) overload exists for an init-only
        // destination, so there's nothing for an interceptor to redirect to - mirrors the
        // existing _mapInto skip in MappingEmitter.Dispatcher.cs (HasInitOnlyProperty). If this
        // guard were wrong, the generator would emit an interceptor calling a method that
        // doesn't exist - a real compile break for any consumer with an init-only destination.
        var result = GeneratorTestHelper.Run(TwoArgInitOnlyDestinationSource, References(), InterceptorsEnabled(LanguageVersion.CSharp14));

        Assert.DoesNotContain("InterceptsLocation", result.GeneratedSource);
        Assert.DoesNotContain("class Interceptors", result.GeneratedSource);
        Assert.Empty(result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        Assert.NotNull(result.Assembly);
        var callerType = result.Assembly!.GetType("Sample.Caller")!;
        var userType = result.Assembly!.GetType("Sample.User")!;
        var userDtoType = result.Assembly!.GetType("Sample.UserDto")!;
        var user = Activator.CreateInstance(userType)!;
        var destination = Activator.CreateInstance(userDtoType)!;

        // No _mapInto entry exists for this pair (GM008) - matches DispatcherRuntimeTests'
        // InitOnlyRecordDestination_MapInto_ThrowsBecauseTwoArgOverloadWasOmitted, proving
        // interceptors being enabled doesn't change this pre-existing, unrelated behavior.
        var ex = Assert.Throws<TargetInvocationException>(() =>
            callerType.GetMethod("CallDirectTwoArg")!.Invoke(null, new[] { user, destination }));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void MultipleCallSitesOfSamePair_StackOntoOneInterceptorMethod()
    {
        var result = GeneratorTestHelper.Run(TwoCallSitesSamePairSource, References(), InterceptorsEnabled(LanguageVersion.CSharp14));

        Assert.Empty(result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // Two call sites, same (TSource,TDestination) pair and shape -> two [InterceptsLocation]
        // attributes stacked on ONE generated method, not two separate methods (confirmed
        // possible in Phase 1's spike; this is the first regression test for it in the real
        // suite).
        Assert.Equal(2, CountOccurrences(result.GeneratedSource!, "InterceptsLocation("));
        Assert.Equal(1, CountOccurrences(result.GeneratedSource!, "Intercepted_UserDto_0("));
        Assert.DoesNotContain("Intercepted_UserDto_1", result.GeneratedSource);

        Assert.NotNull(result.Assembly);
        var callerType = result.Assembly!.GetType("Sample.Caller")!;
        var userType = result.Assembly!.GetType("Sample.User")!;
        var userDtoType = result.Assembly!.GetType("Sample.UserDto")!;

        var user1 = Activator.CreateInstance(userType)!;
        userType.GetProperty("Id")!.SetValue(user1, 1);
        userType.GetProperty("Name")!.SetValue(user1, "First");
        var dto1 = callerType.GetMethod("CallDirect1")!.Invoke(null, new[] { user1 });
        Assert.Equal(1, userDtoType.GetProperty("Id")!.GetValue(dto1));
        Assert.Equal("First", userDtoType.GetProperty("Name")!.GetValue(dto1));

        var user2 = Activator.CreateInstance(userType)!;
        userType.GetProperty("Id")!.SetValue(user2, 2);
        userType.GetProperty("Name")!.SetValue(user2, "Second");
        var dto2 = callerType.GetMethod("CallDirect2")!.Invoke(null, new[] { user2 });
        Assert.Equal(2, userDtoType.GetProperty("Id")!.GetValue(dto2));
        Assert.Equal("Second", userDtoType.GetProperty("Name")!.GetValue(dto2));
    }

    [Fact]
    public void DifferentMappedPairsInSameFile_EachGetsItsOwnInterceptor_NoCrossTalk()
    {
        var result = GeneratorTestHelper.Run(TwoDifferentPairsSource, References(), InterceptorsEnabled(LanguageVersion.CSharp14));

        Assert.Empty(result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        Assert.Equal(2, CountOccurrences(result.GeneratedSource!, "InterceptsLocation("));
        Assert.Contains("Intercepted_UserDto_", result.GeneratedSource);
        Assert.Contains("Intercepted_OrderDto_", result.GeneratedSource);

        Assert.NotNull(result.Assembly);
        var callerType = result.Assembly!.GetType("Sample.Caller")!;
        var userType = result.Assembly!.GetType("Sample.User")!;
        var userDtoType = result.Assembly!.GetType("Sample.UserDto")!;
        var orderType = result.Assembly!.GetType("Sample.Order")!;
        var orderDtoType = result.Assembly!.GetType("Sample.OrderDto")!;

        var user = Activator.CreateInstance(userType)!;
        userType.GetProperty("Id")!.SetValue(user, 5);
        userType.GetProperty("Name")!.SetValue(user, "Ada");
        var userDto = callerType.GetMethod("CallUser")!.Invoke(null, new[] { user });
        Assert.Equal(5, userDtoType.GetProperty("Id")!.GetValue(userDto));
        Assert.Equal("Ada", userDtoType.GetProperty("Name")!.GetValue(userDto));

        var order = Activator.CreateInstance(orderType)!;
        orderType.GetProperty("Total")!.SetValue(order, 42);
        var orderDto = callerType.GetMethod("CallOrder")!.Invoke(null, new[] { order });
        Assert.Equal(42, orderDtoType.GetProperty("Total")!.GetValue(orderDto));
    }

    [Fact]
    public void CallSiteWithNoResolvedMapping_SilentlySkipped_MappedSiblingStillIntercepted()
    {
        // GeneratedMappings.Map<User, UnrelatedType>(...) is syntactically a candidate (matches
        // the discovery predicate) but no MappingModel exists for (User, UnrelatedType) - no
        // [MapTo] declares that pair. Must fall back to the dictionary (which itself throws
        // InvalidOperationException at runtime, unchanged), not crash the generator or emit
        // broken code - and must not affect the OTHER, genuinely mapped call site in the same
        // file.
        var result = GeneratorTestHelper.Run(UnmappedDestinationSource, References(), InterceptorsEnabled(LanguageVersion.CSharp14));

        Assert.Empty(result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        Assert.Equal(1, CountOccurrences(result.GeneratedSource!, "InterceptsLocation("));
        Assert.Contains("Intercepted_UserDto_", result.GeneratedSource);
        Assert.DoesNotContain("Intercepted_UnrelatedType", result.GeneratedSource);

        Assert.NotNull(result.Assembly);
        var callerType = result.Assembly!.GetType("Sample.Caller")!;
        var userType = result.Assembly!.GetType("Sample.User")!;
        var userDtoType = result.Assembly!.GetType("Sample.UserDto")!;

        var user = Activator.CreateInstance(userType)!;
        userType.GetProperty("Id")!.SetValue(user, 7);
        userType.GetProperty("Name")!.SetValue(user, "Grace");
        var dto = callerType.GetMethod("CallMapped")!.Invoke(null, new[] { user });
        Assert.Equal(7, userDtoType.GetProperty("Id")!.GetValue(dto));
        Assert.Equal("Grace", userDtoType.GetProperty("Name")!.GetValue(dto));

        var user2 = Activator.CreateInstance(userType)!;
        var ex = Assert.Throws<TargetInvocationException>(() =>
            callerType.GetMethod("CallUnmapped")!.Invoke(null, new[] { user2 }));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
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
}

# GeneratedMapper

Source-generator-based mapper: compile-time replacement for runtime reflection-based mappers, for mapping between database entities and view models, with EF Core-translatable SQL projection.

New here? See [USAGE.md](USAGE.md) for an installation-to-first-mapping walkthrough. The rest of this file is the attribute-by-attribute reference.

## Mapping declaration

```csharp
[MapTo(typeof(UserDto), GenerateReverse = true)]
[MapProperty(
    typeof(UserDto),
    nameof(User.Email),
    nameof(UserDto.EmailAddress))]
public sealed class User
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
}
```

The mapping configuration lives on the mapping declaration — either the source type (`[MapTo]`) or the destination type (`[MapFrom]`, see below) — not on either DTO/entity property. This keeps reverse mappings and projection mappings unambiguous about direction. Use `[MapIgnore]` on a destination property to opt it out of auto-wiring.

For each declared mapping the generator emits, into a `GeneratedMapper.GeneratedMappings` static class:

- `To{Destination}(this Source source)` / `To{Destination}(this Source source, Destination destination)` — imperative, in-memory mapping.
- `{Source}To{Destination}Projection` — a static `Expression<Func<Source, Destination>>` built entirely from inlined object initializers (no method calls), so it's translatable by EF Core.
- `ProjectTo{Destination}(this IQueryable<Source> source)` — applies the projection to a query, e.g. `dbContext.Blogs.ProjectToBlogDto()`.
- A generic `Map<TDestination>`/`Map<TSource,TDestination>` dispatcher (backed by static `FrozenDictionary` lookup tables, not a chain of type checks), plus a `GeneratedMapperService : IMapper` for DI registration (`services.AddSingleton<IMapper, GeneratedMapperService>()`).

Nested and enumerable (`List<T>`/array/`IEnumerable<T>`) properties are mapped automatically when a mapping exists between the element types.

### Declaring from the destination side

```csharp
// User (the entity) has no idea UserDto exists - no reference to it, no attribute on it.
public sealed class User
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
}

[MapFrom(typeof(User), GenerateReverse = true)]
[MapProperty(typeof(User), nameof(User.Email), nameof(EmailAddress))]
public sealed class UserDto
{
    public int Id { get; set; }
    public string EmailAddress { get; set; } = "";
}
```

`[MapFrom]` is `[MapTo]` placed on the other side of the mapping — functionally identical (same generated methods, same `GenerateReverse`/`MaxDepth`), just declared on the destination type instead of the source. Use it when the source shouldn't reference the destination — typically a domain entity in a core/domain layer that shouldn't know about the DTOs/view models an outer layer builds from it, which is usually already allowed to reference the entity.

`[MapProperty]`, `[MapCondition]`, `[MapUsing]`, and `[MapDefault]` can all be placed alongside `[MapFrom]` on the same destination type instead of on the source. The `Type` argument that normally names the destination now names the source instead (matching `[MapFrom]`'s own argument) — everything else about them, including the property-name arguments, keeps its usual meaning. The one real difference: a `[MapCondition]`/`[MapUsing]` method is looked up on whichever type physically carries the attribute, so for a `[MapFrom]`-declared mapping it's expected on the destination (the DTO), not the source — which is the point, since the source still isn't allowed to know about the destination:

```csharp
[MapFrom(typeof(User))]
[MapUsing(typeof(User), nameof(FullName), nameof(ComputeFullName))]
public sealed class UserDto
{
    public string FullName { get; set; } = "";

    // Lives here, not on User - it's fine for the DTO to reference User.FirstName/LastName.
    public static string ComputeFullName(User source) => $"{source.FirstName} {source.LastName}";
}
```

A given mapping only needs one of `[MapTo]`/`[MapFrom]` — pick whichever side is allowed to reference the other in your architecture. Nothing else in this document depends on which one was used; every example below works identically declared either way. Declaring the same source/destination pair twice — via both `[MapTo]` and `[MapFrom]`, two `[MapTo]`s to the same destination, or a `GenerateReverse`-implied pair colliding with an explicit declaration — is reported as `GM011`; only the last declaration encountered is actually used.

### Independent reverse mapping

`GenerateReverse = true` is the quick way to get both directions, but `[MapCondition]`/`[MapUsing]`/`[MapDefault]` are never auto-reversed by it — the reverse direction just leaves that property unconditioned/unconverted/without a fallback unless you declare it separately. To get both directions *each with their own* condition/converter/default, put both `[MapFrom]` and `[MapTo]` — naming the same other type — on one class instead of using `GenerateReverse`:

```csharp
public sealed class User
{
    public string Email { get; set; } = "";
}

[MapFrom(typeof(User))]                                              // User -> UserDto
[MapProperty(typeof(User), nameof(User.Email), nameof(EmailAddress))]
[MapCondition(typeof(User), nameof(EmailAddress), nameof(ShouldMapEmailForward))]

[MapTo(typeof(User))]                                                // UserDto -> User
[MapProperty(typeof(User), nameof(EmailAddress), nameof(User.Email))]
[MapCondition(typeof(User), nameof(User.Email), nameof(ShouldMapEmailBackward))]
public sealed class UserDto
{
    public string EmailAddress { get; set; } = "";

    public static bool ShouldMapEmailForward(User source) => !string.IsNullOrEmpty(source.Email);
    public static bool ShouldMapEmailBackward(UserDto source) => !string.IsNullOrEmpty(source.EmailAddress);
}
```

This produces both `User.ToUserDto()` and `UserDto.ToUser()`, same as `GenerateReverse`, but as two fully independent declarations — it does **not** trigger `GM011`, because `[MapFrom(typeof(User))]` and `[MapTo(typeof(User))]` name two different pairs (`User → UserDto` and `UserDto → User`), not the same pair twice. `GM011` only fires when the *same* pair and direction gets declared more than once (e.g. `[MapTo(typeof(UserDto))]` on `User` *and* `[MapFrom(typeof(User))]` on `UserDto` — that's the identical `User → UserDto` direction declared from both sides).

The two directions don't collide even though every companion attribute above names the same `Type` argument (`typeof(User)`): what actually separates them is the destination-property name in each attribute — `EmailAddress` only exists on `UserDto` (the `[MapFrom]` direction's destination), `Email` only exists on `User` (the `[MapTo]` direction's destination). One real footgun: each direction needs its *own*, oppositely-oriented companion attribute — reusing the same `[MapProperty(typeof(User), nameof(User.Email), nameof(EmailAddress))]` for both (instead of writing a second one with the arguments swapped) only configures the `[MapFrom]` direction; the other one silently falls back to exact-name matching, finds nothing, and reports `GM001`.

### Conditional mapping

```csharp
[MapTo(typeof(PostDto))]
[MapCondition(typeof(PostDto), nameof(PostDto.Body), nameof(ShouldMapBody))]
public sealed class Post
{
    public string Body { get; set; } = "";
    public bool IsDraft { get; set; }

    public static bool ShouldMapBody(Post source) => !source.IsDraft;
}
```

`[MapCondition]` gates a single destination property behind a `static bool` method declared on the source type, with signature `(TSource)` or `(TSource, TDestination?)` (the two-arg form can also inspect the destination, e.g. "only overwrite if not already set"). The condition is honored by the imperative mapper and — since it's just called through `To{Destination}(...)` — by the `IMapper`/`Map<T>` dispatcher too. It is **not** honored by `.ProjectTo{Destination}()`: an arbitrary method call can't be translated to SQL, so the property is left out of the projection entirely (`GM005`). Conditions are not auto-reversed by `GenerateReverse` — declare a separate `[MapCondition]` on the DTO if the reverse direction needs one too.

### Custom conversion

```csharp
[MapTo(typeof(UserDto))]
[MapUsing(typeof(UserDto), nameof(UserDto.FullName), nameof(ComputeFullName))]
public sealed class User
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";

    public static string ComputeFullName(User source) => $"{source.FirstName} {source.LastName}";
}
```

`[MapUsing]` maps a single destination property through a static conversion function instead of a direct/nested/enumerable match — useful when a destination property doesn't correspond to any single source property (a computed value, a combined field, a translated enum). `conditionMethod`/`converterMethod` must be a `static` method on the source type with signature `TDestinationProperty Method(TSource)` (an implicitly convertible return type is also accepted). It's honored by both the imperative mapper and SQL projections — for projections the call is inlined as-is, so it's your responsibility to keep the method translatable by EF Core's query provider. It can be combined with `[MapCondition]` on the same property (the condition gates whether the converted value is assigned). Like `[MapCondition]`, it is **not** auto-reversed by `GenerateReverse` — declare a separate `[MapUsing]` on the destination type if the reverse direction needs one too.

### Default values

```csharp
[MapTo(typeof(UserDto))]
[MapDefault(typeof(UserDto), nameof(UserDto.DisplayName), "Unknown")]
public sealed class User
{
    public string? DisplayName { get; set; }
}
```

`[MapDefault]` substitutes a constant when the matched value would otherwise be `null`, emitting `source.Prop ?? defaultValue` instead of a plain property access — a lighter alternative to `[MapUsing]` when all you need is a fallback value. It applies to a directly-matched property, or one computed via `[MapUsing]` on the same property; it has no effect on a nested/enumerable property or a non-nullable value type, since neither can meaningfully `?? ` against a constant. `defaultValue` is an attribute constructor argument, so it's limited to what Roslyn allows there — numeric, string, bool, char, or enum constants, not arbitrary expressions. Honored by both the imperative mapper and SQL projections (translated as `COALESCE`). Like `[MapCondition]`/`[MapUsing]`, it is **not** auto-reversed by `GenerateReverse`.

### Naming-convention flattening

```csharp
[MapTo(typeof(UserDto))]
public sealed class User
{
    public Address HomeAddress { get; set; } = new();
}

public sealed class Address
{
    public string City { get; set; } = "";
}

public sealed class UserDto
{
    public string HomeAddressCity { get; set; } = ""; // <- source.HomeAddress.City, no attribute needed
}
```

When a destination property has no exact-name match and no explicit `[MapProperty]` override, the generator tries splitting its name at PascalCase boundaries against a chain of nested source properties — `HomeAddressCity` against `source.HomeAddress.City`, checking each segment by exact case-sensitive name. This is a fallback for the default name-matching path only; an explicit `[MapProperty]` source name must still be an exact top-level property name. Every *intermediate* segment in a matched chain must be non-nullable (a `?`-annotated or `Nullable<T>` intermediate is excluded from candidates entirely, rather than emitting an unguarded chain that could throw at runtime) — the leaf property's own nullability is unaffected by this and follows the same rules as a normal direct match. If a destination name splits more than one valid way (e.g. both `Home.AddressCity` and `HomeAddress.City` resolve), the match is ambiguous and the property is left unmapped (`GM010`) rather than guessing — add an explicit `[MapProperty]` to disambiguate. A flattened match resolves independently in each direction, so it is **not** auto-reversed by `GenerateReverse`: the reverse direction has no way to un-flatten a scalar back into constructing a nested object, and leaves the nested property unmapped (`GM001`) unless you supply one yourself. Since the matched chain is just a longer property-access expression, this works identically — with no extra codegen — in both the imperative mapper and `.ProjectTo{Destination}()`'s SQL projection (see the sample app's `Post.Author`/`PostDto.AuthorDisplayName`, an EF Core owned type flattened straight into a SQL column).

### Diagnostics

`GM001` (destination property left unmapped), `GM002` (projection skipped — the mapping graph is cyclic; the imperative method is still generated), `GM003` (error — incompatible property types with no implicit conversion), `GM004` (error — `[MapCondition]` references a method that doesn't exist or has the wrong signature), `GM005` (a conditionally-mapped property was left out of a SQL projection), `GM006` (a mapping was skipped entirely because the destination has an init-only property, no accessible parameterless constructor, and no constructor whose parameters could all be matched to already-mapped, unconditioned properties), `GM007` (`[MapCondition]` on an init-only destination property isn't supported — the property was left out), `GM008` (the two-arg `To{Dest}(source, destination)` overload was omitted because the destination has init-only properties), `GM009` (error — `[MapUsing]` references a method that doesn't exist or has the wrong signature/return type), `GM010` (a destination property's name matched more than one valid naming-convention-flattening path and was left unmapped), `GM011` (the same source/destination pair was declared more than once — via `[MapTo]`, `[MapFrom]`, or a `GenerateReverse`-implied pair colliding with an explicit one; only the last one encountered is used).

Diagnostic IDs are never reused, only retired, tracked via `src/GeneratedMapper.Generator/AnalyzerReleases.Shipped.md`/`.Unshipped.md` (mechanically enforced at build time — see `CONTRIBUTING.md`).

### Recursion guard for self-referential types

```csharp
[MapTo(typeof(CategoryDto), MaxDepth = 3)]
public sealed class Category
{
    public string Name { get; set; } = "";
    public Category? Parent { get; set; }
}
```

`MaxDepth` guards a mapping that directly maps into itself (a property whose type is the same source/destination pair, e.g. `Category.Parent`/`Category.Children` both mapping to `Category`) against unbounded recursion on a cyclic runtime object graph — without it, a genuinely self-referential graph can stack-overflow at runtime, since the generator only detects *projection* cycles, not imperative ones. Once the depth limit is hit, the recursive property is left unset instead of continuing to recurse. Defaults to `0` (unlimited, unchanged behavior). Only guards direct self-reference within one `[MapTo]` declaration — it does not detect indirect cycles across multiple different mapping pairs.

### Init-only and record destinations

A destination with `init`-only properties (including non-positional records) is mapped by building the object via initializer syntax in `To{Dest}(source)`; the two-arg `To{Dest}(source, destination)` overload (and `IMapper.Map(source, destination)`) is omitted for it, since an `init` property can't be assigned after construction (`GM008`).

Positional records (e.g. `record UserDto(int Id, string Name);`) are supported too: when the destination has no parameterless constructor, the generator looks for one whose parameters all match already-mapped, unconditioned properties by name and type — the record's own synthesized constructor, in the common case — and builds `new UserDto(source.Id, source.Name)` instead, both imperatively and in `.ProjectTo{Destination}()`. Any settable property not covered by that constructor is still assigned afterward (via object-initializer syntax if it's `init`-only, sequential assignment otherwise). If no confident constructor match exists — a required parameter has no matching source property, or one is gated by `[MapCondition]` (conditions can't become constructor arguments) — the mapping is skipped entirely with `GM006` rather than guessing.

A `required` destination property (C# 11) is set inline wherever the destination gets constructed (the object-initializer in `To{Dest}(source)`, or the constructor call for a positional record), since C# enforces `required` on that expression itself — assigning it in a later statement doesn't count. A `required` property with no resolved mapping is reported as `GM013` rather than surfacing only as a raw `CS9035` in the generated file; combining `required` with `[MapCondition]` is rejected outright (`GM014`), since a required member can't be conditionally left unset.

## Native AOT

Everything the generator emits — the imperative `To{Dest}()` methods, the dispatcher, `IMapper`
— is plain, direct C# with no reflection at runtime, so it publishes and runs correctly under
Native AOT (`dotnet publish -p:PublishAot=true`). `GeneratedMapper.Abstractions`'s net8.0 target
opts into `<IsAotCompatible>true</IsAotCompatible>`, turning on the trimmer/AOT analyzer's own
build-time warnings.

The one caveat: the C# compiler itself — not GeneratedMapper — compiles an object-initializer
written inside an `Expression<Func<...>>` lambda (which is exactly what the
`{Source}To{Destination}Projection` field above is) by calling
`Expression.Bind(MethodInfo, Expression)`, a BCL method marked `[RequiresUnreferencedCode]`. That
would normally surface as an `IL2026` trim warning on every consumer's own AOT publish, for every
mapping that needs one, whether or not they ever call `ProjectTo{Destination}()`. "Needs one" is
deliberately precise: a destination whose constructor covers every mapped property (e.g. a
positional record) compiles to a pure `Expression.New(ctor, args)` with no trailing
`{ Prop = value }` block at all - no `Expression.Bind` call, nothing to warn about, nothing to fix.
For the common case that isn't true for (a plain mutable class), the generator fixes it at the
source, for consumers whose compilation actually has the capability (net6+ - see below): every
projection field is assigned in one explicit static constructor carrying
`[DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Dest))]` for every
destination type that actually needs one (an explicit instruction to the trimmer to keep those
properties, not a hope that something else already references them) plus
`[UnconditionalSuppressMessage("Trimming", "IL2026", ...)]` to silence the now-genuinely-guaranteed-safe
warning - a file where every mapping happens to be constructor-covered emits neither attribute at
all. This isn't just asserted: `samples/GeneratedMapper.Sample.Aot` is a small, EF-Core-free
console app published with `PublishAot=true` and actually run, including calling `.Compile()` on a
generated projection field directly (exactly what `IQueryable.Select()` does under the hood for an
in-memory, non-EF-Core provider) and verifying the result — confirming both that expression-tree
compilation works under full AOT and that the reflected property accessors survive trimming.

`UnconditionalSuppressMessageAttribute`/`DynamicDependencyAttribute`/`DynamicallyAccessedMemberTypes`
don't exist below net6 — emitting them unconditionally would break compilation for any
`netstandard2.0` consumer with even one SQL-projection mapping (verified directly, and covered by
a dedicated regression test compiled against the real netstandard2.0 reference assembly). So, like
`FrozenDictionary` and `#nullable`/`!` above, this is gated by asking the consumer's own
`Compilation` whether the type resolves (`ConsumerCapabilities.CanSuppressTrimWarnings`) rather
than assumed from a TFM name — a consumer without it just gets the same explicit-static-constructor
structure with no attributes, since it can't run the trim/AOT analyzer to produce the warning in
the first place.

## Project layout

- `src/GeneratedMapper.Abstractions` — attributes (`MapTo`, `MapFrom`, `MapProperty`, `MapIgnore`, `MapCondition`, `MapUsing`, `MapDefault`) and the `IMapper` interface.
- `src/GeneratedMapper.Generator` — the incremental source generator.
- `src/GeneratedMapper.CodeFixes` — IDE code fixes for the diagnostics that have an unambiguous mechanical fix (`GM001` — add `[MapIgnore]`; `GM004`/`GM009` — generate a stub method).
- `samples/GeneratedMapper.Sample` — a runnable console app mapping EF Core entities (SQLite in-memory) to view models, printing the SQL generated by `ProjectToBlogDto()`.
- `samples/GeneratedMapper.Sample.Aot` — a small, EF-Core-free console app that publishes and runs under Native AOT (`dotnet publish -r <rid>`, `PublishAot` is set in the project) — see the Native AOT section above.
- `tests/GeneratedMapper.Generator.Tests` — generator unit tests driven via `CSharpGeneratorDriver`.
- `tests/GeneratedMapper.CodeFixes.Tests` — code-fix tests driven via an `AdhocWorkspace` running the real generator.
- `benchmarks/GeneratedMapper.Benchmarks` — BenchmarkDotNet throughput/startup/SQL-projection comparison against AutoMapper.
- `benchmarks/GeneratedMapper.Benchmarks.ParityTests` — correctness (and SQL-translation) checks that must pass before the benchmark numbers above mean anything.

## Try it

```
dotnet build GeneratedMapper.sln
dotnet test tests/GeneratedMapper.Generator.Tests
dotnet test tests/GeneratedMapper.CodeFixes.Tests
dotnet run --project samples/GeneratedMapper.Sample
dotnet publish samples/GeneratedMapper.Sample.Aot -r <rid>   # e.g. win-x64, linux-x64, osx-arm64
```

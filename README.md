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

The mapping configuration lives on the mapping declaration (the source type), not on either DTO/entity property. This keeps reverse mappings and projection mappings unambiguous about direction. Use `[MapIgnore]` on a destination property to opt it out of auto-wiring.

For each declared mapping the generator emits, into a `GeneratedMapper.GeneratedMappings` static class:

- `To{Destination}(this Source source)` / `To{Destination}(this Source source, Destination destination)` — imperative, in-memory mapping.
- `To{Destination}Projection` — a static `Expression<Func<Source, Destination>>` built entirely from inlined object initializers (no method calls), so it's translatable by EF Core.
- `ProjectTo{Destination}(this IQueryable<Source> source)` — applies the projection to a query, e.g. `dbContext.Blogs.ProjectToBlogDto()`.
- A generic `Map<TDestination>`/`Map<TSource,TDestination>` dispatcher (backed by static `FrozenDictionary` lookup tables, not a chain of type checks), plus a `GeneratedMapperService : IMapper` for DI registration (`services.AddSingleton<IMapper, GeneratedMapperService>()`).

Nested and enumerable (`List<T>`/array/`IEnumerable<T>`) properties are mapped automatically when a mapping exists between the element types.

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

`GM001` (destination property left unmapped), `GM002` (projection skipped — the mapping graph is cyclic; the imperative method is still generated), `GM003` (error — incompatible property types with no implicit conversion), `GM004` (error — `[MapCondition]` references a method that doesn't exist or has the wrong signature), `GM005` (a conditionally-mapped property was left out of a SQL projection), `GM006` (a mapping was skipped entirely because the destination has an init-only property, no accessible parameterless constructor, and no constructor whose parameters could all be matched to already-mapped, unconditioned properties), `GM007` (`[MapCondition]` on an init-only destination property isn't supported — the property was left out), `GM008` (the two-arg `To{Dest}(source, destination)` overload was omitted because the destination has init-only properties), `GM009` (error — `[MapUsing]` references a method that doesn't exist or has the wrong signature/return type), `GM010` (a destination property's name matched more than one valid naming-convention-flattening path and was left unmapped).

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

## Project layout

- `src/GeneratedMapper.Abstractions` — attributes (`MapTo`, `MapProperty`, `MapIgnore`, `MapCondition`, `MapUsing`, `MapDefault`) and the `IMapper` interface.
- `src/GeneratedMapper.Generator` — the incremental source generator.
- `src/GeneratedMapper.CodeFixes` — IDE code fixes for the diagnostics that have an unambiguous mechanical fix (`GM001` — add `[MapIgnore]`; `GM004`/`GM009` — generate a stub method).
- `samples/GeneratedMapper.Sample` — a runnable console app mapping EF Core entities (SQLite in-memory) to view models, printing the SQL generated by `ProjectToBlogDto()`.
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
```

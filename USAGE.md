# Using GeneratedMapper

A practical guide for consumers: how to install it, what it generates, and how to call the
generated code. For the full list of attributes and diagnostics in one place, see the
"Mapping declaration" section of [README.md](README.md) — this guide walks through the same
ground task-by-task instead.

## Installing

GeneratedMapper isn't published to nuget.org yet. Until it is, you have two ways to consume it:

**From a locally built package** (recommended if you just want to try it):

```
dotnet pack GeneratedMapper.sln -c Release -o ./nupkg-out
```

then add a `nuget.config` pointing at that folder in your own project and

```
dotnet add package GeneratedMapper.Generator --version 0.1.0
```

This one package pulls in `GeneratedMapper.Abstractions` automatically — you don't need to
reference it separately. See `smoke-test/ConsumerSmokeTest` in this repo for a working example
of exactly this setup.

**As a project reference**, if you're working inside a solution that already contains
GeneratedMapper's source (e.g. you cloned this repo alongside your own project):

```xml
<ItemGroup>
  <ProjectReference Include="..\GeneratedMapper\src\GeneratedMapper.Abstractions\GeneratedMapper.Abstractions.csproj" />
  <ProjectReference Include="..\GeneratedMapper\src\GeneratedMapper.Generator\GeneratedMapper.Generator.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

Once nuget.org publishing happens, this section will just say `dotnet add package
GeneratedMapper.Generator`.

## Quick start

Declare a mapping by putting `[MapTo]` on your source type — the entity, not the DTO:

```csharp
using GeneratedMapper;

[MapTo(typeof(UserDto))]
public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public sealed class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
```

That's it — no separate mapping profile, no fluent configuration. Build the project, and you can
call:

```csharp
var user = new User { Id = 1, Name = "Ada" };
UserDto dto = user.ToUserDto();
```

Properties are matched by exact name by default. Anything you need to customize (renames,
conditions, computed values, nested objects) is declared with more attributes right there on
`User` — see "Customizing a mapping" below.

## What gets generated

For every `[MapTo(typeof(Dest))]` declaration, the generator emits into a single
`GeneratedMapper.GeneratedMappings` static class (one file for the whole project, regenerated on
every build):

| What | Signature | Use it for |
|---|---|---|
| Imperative mapper | `Dest To{Dest}(this Source source)` | The common case — map one object to a new instance. |
| Imperative mapper (populate) | `Dest To{Dest}(this Source source, Dest destination)` | Map into an object you already have (e.g. an EF Core entity you're updating). Omitted if `Dest` has `init`-only properties — see "Init-only and record destinations" below. |
| Projection expression | `Expression<Func<Source, Dest>> To{Dest}Projection` | A real expression tree, built only from object initializers — no method calls — so it's translatable by any LINQ provider, including EF Core. |
| Projection extension | `IQueryable<Dest> ProjectTo{Dest}(this IQueryable<Source> source)` | `dbContext.Users.ProjectToUserDto()` — applies the expression above to a query. Only the columns you actually mapped are selected; nothing else comes back from the database. |
| Generic dispatcher | `TDest Map<TDest>(object source)` / `Map<TSource,TDest>(TSource source)` / `Map<TSource,TDest>(TSource source, TDest destination)` | Type-erased mapping by runtime type, backed by a `FrozenDictionary` lookup — not a chain of `if`/`is` checks. Useful for generic infrastructure code that doesn't know the concrete types at compile time. |
| DI service | `GeneratedMapperService : IMapper` | Wraps the dispatcher above behind an injectable interface. |

Every one of these is produced by a Roslyn incremental source generator at build time — nothing
runs at your app's startup to build a configuration, and there's no reflection at runtime. The
generated file is ordinary C#; you can open it (see "Inspecting the generated code" below), set
a breakpoint in it, and step through it like any other method.

## Using it in your code

**Imperative, in-memory:**

```csharp
UserDto dto = user.ToUserDto();
```

**Via dependency injection**, when the calling code shouldn't know about the generated types
directly:

```csharp
// Startup/composition root
services.AddSingleton<IMapper, GeneratedMapperService>();

// Wherever you need it
public sealed class UserService(IMapper mapper)
{
    public UserDto GetUserDto(User user) => mapper.Map<User, UserDto>(user);
}
```

`IMapper.Map<TDestination>(object source)` is also available when you only know the source's
*runtime* type (e.g. in a generic repository) — it resolves the right mapping by looking up
`source.GetType()`, and throws `InvalidOperationException` if no `[MapTo]` declaration produced
a mapping for that pair.

**SQL projection with EF Core:**

```csharp
List<UserDto> dtos = await dbContext.Users
    .Where(u => u.IsActive)
    .ProjectToUserDto()
    .ToListAsync();
```

Compose it with the rest of your LINQ query exactly like `.Select()` — because that's literally
what it does under the hood (`source.Select(ToUserDtoProjection)`). Only the properties present
on `UserDto` are ever selected from the database.

## Customizing a mapping

All of these go on the **source** type, next to `[MapTo]` — never on the destination.

**Rename a property** (`[MapProperty]`):

```csharp
[MapTo(typeof(UserDto))]
[MapProperty(typeof(UserDto), nameof(Email), nameof(UserDto.EmailAddress))]
public sealed class User
{
    public string Email { get; set; } = "";
}
```

**Exclude a destination property** from being reported as unmapped (`[MapIgnore]`, put on the
*destination* property itself — the one exception to "config lives on the source"):

```csharp
public sealed class UserDto
{
    [MapIgnore]
    public int ComputedOnly { get; set; }
}
```

**Gate a property on a runtime condition** (`[MapCondition]`) — a `static bool` method on the
source type decides whether the property gets assigned:

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

The condition method can optionally also accept the destination (`static bool
Method(TSource, TDestination?)`), e.g. to only overwrite a value that isn't already set. Honored
by the imperative mapper and `IMapper`; **not** honored by `.ProjectTo{Dest}()` — a method call
can't be translated to SQL, so a conditioned property is left out of the projection entirely.

**Compute a value from a custom method** (`[MapUsing]`) — for a destination property that
doesn't correspond to any single source property:

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

Unlike `[MapCondition]`, this *is* honored in SQL projections — the call is inlined as-is, so
it's your responsibility to keep the method translatable by your LINQ provider (simple
expressions like string interpolation over other mapped properties are fine; anything the
provider can't translate will fail at query-execution time, the same as writing `.Select()` by
hand).

**Substitute a default when the value would be null** (`[MapDefault]`):

```csharp
[MapTo(typeof(UserDto))]
[MapDefault(typeof(UserDto), nameof(UserDto.DisplayName), "Unknown")]
public sealed class User
{
    public string? DisplayName { get; set; }
}
```

Emits `source.DisplayName ?? "Unknown"` (translated as SQL `COALESCE` in projections). Only
takes effect on a directly-matched or `[MapUsing]`-converted property whose type can actually be
`null` (a reference type or `Nullable<T>`) — silently has no effect otherwise.

**Nothing needed for a nested/renamed property that matches a naming convention** — if
`HomeAddressCity` on the destination has no direct match, the generator automatically tries
`source.HomeAddress.City`:

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
    public string HomeAddressCity { get; set; } = ""; // <- source.HomeAddress.City automatically
}
```

This only fires when there's no explicit `[MapProperty]` override and the match is unambiguous —
see the Diagnostics table below for what happens when it isn't.

## Nested objects and collections

If both `User` and `Address` have their own `[MapTo]` declarations, a `User.HomeAddress` property
mapping to `UserDto.HomeAddress` is wired up automatically — no attribute needed, as long as both
types are mapped *somewhere in the same project*. The same applies to collections
(`List<T>`/arrays/`IEnumerable<T>`): if the element types have a mapping between them, the
collection property maps automatically, materialized into whatever collection shape the
destination declares (`List<T>`, `T[]`, `HashSet<T>`).

## Reverse mappings

```csharp
[MapTo(typeof(UserDto), GenerateReverse = true)]
public sealed class User { /* ... */ }
```

generates both `user.ToUserDto()` and `dto.ToUser()` from one declaration. `[MapCondition]`,
`[MapUsing]`, `[MapDefault]`, and naming-convention flattening are **not** carried over to the
reverse direction automatically, since they're tied to the original source type (or, for
flattening, can't be un-flattened back into constructing a nested object) — declare a separate
attribute on the destination type if the reverse direction needs its own.

## Init-only and record destinations

A destination with `init`-only properties (including non-positional `record` types) is built via
object-initializer syntax; the two-argument `To{Dest}(source, destination)` overload (and
`IMapper.Map(source, destination)`) simply doesn't exist for it, since you can't assign an
`init` property after construction. Use the one-argument form instead.

Positional records (`record UserDto(int Id, string Name);`) are supported too — the generator
matches the record's own constructor against already-mapped properties and calls it directly,
both imperatively and in the SQL projection.

## Guarding against cyclic object graphs

If a type maps into itself somewhere in its own graph (e.g. `Category.Parent`/`Children` both
mapping to `Category`), a genuinely cyclic *runtime* object could recurse forever with the
imperative mapper. Guard it with `MaxDepth`:

```csharp
[MapTo(typeof(CategoryDto), MaxDepth = 3)]
public sealed class Category
{
    public string Name { get; set; } = "";
    public Category? Parent { get; set; }
}
```

Once the depth limit is hit, the recursive property is left unset instead of continuing. Defaults
to `0` (unlimited — safe for a non-cyclic tree, but only guards *direct* self-reference within
one `[MapTo]` declaration, not a cycle spanning two different declared mappings).

## Inspecting the generated code

Add this to your project to write the generated source to disk so you can read it:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>Generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

then look under `Generated/GeneratedMapper.Generator/.../GeneratedMappings.g.cs` after building.
`samples/GeneratedMapper.Sample` in this repo already does this — it's a good place to see real
generated output for a project with nested objects, collections, conditions, and a SQL
projection all together.

## Diagnostics

The generator reports build-time diagnostics instead of failing silently or throwing at runtime:

| ID | Severity | Meaning |
|---|---|---|
| GM001 | Info | Destination property has no matching source and was left unmapped. Add `[MapProperty]` or `[MapIgnore]`. |
| GM002 | Info | The SQL projection was skipped because the mapping graph is cyclic; the imperative method is still generated. |
| GM003 | Error | Incompatible property types — no implicit conversion and no nested mapping declared. |
| GM004 | Error | `[MapCondition]` references a method that doesn't exist or has the wrong signature. |
| GM005 | Info | A `[MapCondition]`-gated property was left out of the SQL projection (still honored by the imperative mapper). |
| GM006 | Warning | The destination has no usable constructor (no parameterless constructor, and no constructor whose parameters all resolve to already-mapped, unconditioned properties) — the mapping was skipped entirely. |
| GM007 | Warning | `[MapCondition]` on an `init`-only destination property isn't supported — the property was left out. |
| GM008 | Info | The two-argument `To{Dest}(source, destination)` overload was omitted because the destination has `init`-only properties. |
| GM009 | Error | `[MapUsing]` references a method that doesn't exist or has the wrong signature/return type. |
| GM010 | Warning | A destination property's name matched more than one valid naming-convention-flattening path — left unmapped rather than guessed. Add `[MapProperty]` to disambiguate. |

GM001, GM004, and GM009 have one-click IDE code fixes available if you also reference
`GeneratedMapper.CodeFixes`.

## What it doesn't do

Straightforward entity↔DTO shapes are the target use case. Not supported: dictionary mapping,
polymorphic/inheritance mapping, async mapping, before/after-map hooks, or DI-injected custom
converters (`[MapUsing]` is static-only, by design — no runtime service resolution, matching the
"no runtime cost, no runtime surprises" goal). `MaxDepth` only guards direct self-reference, not
a cycle spanning two different `[MapTo]` declarations.

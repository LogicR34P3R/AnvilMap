# Using AnvilMap

The complete reference: every attribute, every diagnostic, every edge case, task-by-task from
installation through the deep behavioral nuances. [README.md](README.md) is the shorter landing
page — install instructions, a quick-start example, and a feature overview that links back here
for the details.

## Installing

AnvilMap is published on nuget.org: [AnvilMap.Generator](https://www.nuget.org/packages/AnvilMap.Generator).

```
dotnet add package AnvilMap.Generator
```

This one package pulls in `AnvilMap.Abstractions` automatically — you don't need to
reference it separately.

**As a project reference**, if you're working inside a solution that already contains
AnvilMap's source instead (e.g. you cloned this repo alongside your own project, or want to
track an unreleased change):

```xml
<ItemGroup>
  <ProjectReference Include="..\AnvilMap\src\AnvilMap.Abstractions\AnvilMap.Abstractions.csproj" />
  <ProjectReference Include="..\AnvilMap\src\AnvilMap.Generator\AnvilMap.Generator.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

## Quick start

Declare a mapping by putting `[MapTo]` on your source type — the entity, not the DTO:

```csharp
using AnvilMap;

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

If your entities live in a core/domain layer that shouldn't reference the DTOs/view models built
from them, put the declaration on the DTO instead, with `[MapFrom]` in place of `[MapTo]` — it's
the same mapping, just declared from the other side:

```csharp
using AnvilMap;

// User has no reference to UserDto anywhere.
public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

[MapFrom(typeof(User))]
public sealed class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
```

Generates the exact same `user.ToUserDto()`. See "Customizing a mapping" below for how the
attributes behave when placed on the DTO this way instead of on the source.

## What gets generated

For every `[MapTo(typeof(Dest))]` declaration, the generator emits into a single
`AnvilMap.GeneratedMappings` static class (one file for the whole project, regenerated on
every build):

| What | Signature | Use it for |
|---|---|---|
| Imperative mapper | `Dest To{Dest}(this Source source)` | The common case — map one object to a new instance. |
| Imperative mapper (populate) | `Dest To{Dest}(this Source source, Dest destination)` | Map into an object you already have (e.g. an EF Core entity you're updating). Omitted if `Dest` has `init`-only properties — see "Init-only and record destinations" below. |
| Projection expression | `Expression<Func<Source, Dest>> {Source}To{Dest}Projection` | A real expression tree, built only from object initializers — no method calls — so it's translatable by any LINQ provider, including EF Core. Qualified by both the source and destination name, since a destination can have more than one source (multiple `[MapFrom]`). |
| Projection extension | `IQueryable<Dest> ProjectTo{Dest}(this IQueryable<Source> source)` | `dbContext.Users.ProjectToUserDto()` — applies the expression above to a query. Only the columns you actually mapped are selected; nothing else comes back from the database. |
| Generic dispatcher | `TDest Map<TDest>(object source)` / `Map<TSource,TDest>(TSource source)` / `Map<TSource,TDest>(TSource source, TDest destination)` | Type-erased mapping by runtime type, backed by a `FrozenDictionary` lookup — not a chain of `if`/`is` checks. Useful for generic infrastructure code that doesn't know the concrete types at compile time. On C# 14, a call to the two-type-argument overloads written directly (not through `IMapper`) with both types statically known gets an automatic, faster path — see "Interceptor-based dispatch" below. |
| DI service | `AnvilMapService : IMapper` | Wraps the dispatcher above behind an injectable interface. |

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
services.AddSingleton<IMapper, AnvilMapService>();

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
what it does under the hood (`source.Select(UserToUserDtoProjection)`). Only the properties present
on `UserDto` are ever selected from the database.

## Customizing a mapping

All of these normally go on the **source** type, next to `[MapTo]`. If your mapping is declared
the other way round, with `[MapFrom]` on the destination (see Quick start above), every one of
`[MapProperty]`, `[MapCondition]`, `[MapUsing]`, `[MapDefault]`, and `[MapInclude]` can be placed
there instead — the `Type` argument that normally names the destination now names the source
instead (matching `[MapFrom]`'s own argument), and everything else, including the property-name
arguments, keeps its usual meaning:

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

The one real difference: a `[MapCondition]`/`[MapUsing]` method is looked up on whichever type
physically carries the attribute, so for a `[MapFrom]`-declared mapping it's expected on the
destination (the DTO), not the source — which is the point, since the source still isn't allowed
to know about the destination.

**Rename a property** (`[MapProperty]`):

```csharp
[MapTo(typeof(UserDto))]
[MapProperty(typeof(UserDto), nameof(Email), nameof(UserDto.EmailAddress))]
public sealed class User
{
    public string Email { get; set; } = "";
}
```

The source name can also be a dotted path into a nested property (e.g. `"HomeAddress.City"`),
letting you pick a specific nested source explicitly instead of relying on
[naming-convention flattening](#naming-convention-flattening) to guess it.

**Exclude a destination property** from being reported as unmapped (`[MapIgnore]`, put on the
*destination* property itself — the one exception to "config lives on the source"):

```csharp
public sealed class UserDto
{
    [MapIgnore]
    public int ComputedOnly { get; set; }
}
```

With no argument, this excludes the property from every mapping into `UserDto`, regardless of
source. Pass a source type to scope the exclusion to just that mapping instead — useful when the
same destination has more than one source (multiple `[MapFrom]`) and the property should still
be mapped normally from the others:

```csharp
[MapFrom(typeof(LegacyUser))]
[MapFrom(typeof(User))]
public sealed class UserDto
{
    // Left unmapped only when built from LegacyUser; still mapped from User.
    [MapIgnore(typeof(LegacyUser))]
    public string Email { get; set; } = "";
}
```

`[MapIgnore]` is repeatable, so a property can be ignored for several specific sources by
decorating it with one attribute per source type. It always wins over anything else configured
for that property in the excluded mapping — a `[MapCondition]`, `[MapUsing]`, `[MapDefault]`, or
`[MapProperty]` on an ignored property is dead code and reported as AM012.

Two more checks catch likely mistakes: a `[MapIgnore(typeof(X))]` where `X` never actually maps
into that destination — a typo, or stale after a rename — is reported as AM015 rather than
silently doing nothing; and an unscoped `[MapIgnore]` alongside a scoped one, or the same source
type named twice, is reported as AM016 (a tidiness nag, not a bug).

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

**Enum conversions are automatic** — no `[MapUsing]` needed for an `enum` source property
mapped to its own underlying integral type (`Status` → `int`, via a cast) or to `string` (via
`.ToString()`). Any other mismatch (a different numeric type, `string` back to an `enum`, a
narrowing conversion) still requires an explicit `[MapUsing]` or is reported as `AM003` — this
generator never guesses at a lossy conversion. The `.ToString()` case is imperative-only
(`AM022`) since most query providers can't translate it into SQL; the underlying-type cast is
projection-safe and included in `.ProjectTo{Dest}()` too. Ordinary implicit C# conversions (`int`
widening to `long`, `DateTime` to `DateTimeOffset`, and so on) already work without any of this —
they're plain C# assignments, not something this generator needs to handle specially. An explicit
`[MapUsing]` on the same property always overrides the automatic conversion — e.g. to map an enum
to a specific string label instead of its member name via `.ToString()`, or to keep an
enum-to-string conversion inside `.ProjectTo{Dest}()` instead of it being left out (`AM022`),
since a `[MapUsing]` converter is inlined into the projection as-is (translatability is still your
responsibility, same as any other `[MapUsing]`).

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

Unlike `[MapCondition]`, this *is* honored in SQL projections — by default as a method call
(`Entity.ComputeFullName(source)`), so it's your responsibility to keep the method translatable
by your LINQ provider (simple expressions like string interpolation over other mapped properties
are fine; anything the provider can't translate will fail at query-execution time, the same as
writing `.Select()` by hand).

**`InlineInProjection` splices the converter's own body into the projection instead of calling
it**, so the query provider's translator can see into the logic directly instead of hitting an
opaque call it may or may not recognize:

```csharp
[MapUsing(typeof(UserDto), nameof(UserDto.FullName), nameof(ComputeFullName), InlineInProjection = true)]
```

Only eligible when `ComputeFullName`'s body is a single expression — an expression-bodied member
(`=> ...;`), or a block with exactly one `return` statement. Anything else (locals, branching,
loops, multiple statements) — along with a handful of narrower cases this generator isn't
confident it can rewrite correctly (a `nameof(...)` inside the body, a reference to a generic
type/method, or a reference to a `private`/`protected` member that wouldn't be reachable from the
generated file) — falls back to the ordinary method-call emission, with an `AM030` diagnostic
explaining why. False by default: silently changing an existing converter's projection behavior
could be surprising (e.g. one intentionally relying on the opaque-call shape for an EF Core
`[DbFunction]`-mapped method). Note that inlining only removes *this* opacity layer — a spliced
expression that itself needs `.ToImmutableArray()`/`new ObservableCollection<T>(...)` internally
is no more translatable inlined than it was as a call, so this doesn't rescue `AM022`/`AM023`'s
own excluded cases.

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
destination declares (`List<T>`, `T[]`, `HashSet<T>`/`ISet<T>`, `ImmutableArray<T>`,
`ObservableCollection<T>`). `ImmutableArray<T>`/`ObservableCollection<T>` are imperative-only —
excluded from `.ProjectTo{Dest}()` with `AM023`, since they aren't confirmed translatable by SQL
query providers the way `List<T>`/`T[]`/`HashSet<T>` are. A destination collection shape this
generator doesn't recognize (and that `List<T>` doesn't already implicitly convert to) reports
`AM003` instead of emitting code that wouldn't compile. Note that a `[MapUsing]` override doesn't
rescue this the way it does for `AM022` (below) — not even with `InlineInProjection` (above):
wrapping `.ToImmutableArray()`/`new ObservableCollection<T>(...)` in your own converter doesn't
make it any more translatable than the automatic version, whether it's called as a method or
spliced inline.

## Naming-convention flattening

When a destination property has no exact-name match and no explicit `[MapProperty]` override,
the generator tries splitting its name at PascalCase boundaries against a chain of nested source
properties:

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
    public string HomeAddressCity { get; set; } = "";
}
```

`UserDto.HomeAddressCity` has no exact match on `User`, so the generator tries `Home.AddressCity`
(no `Home` property exists) and `HomeAddress.City` (matches) — producing
`destination.HomeAddressCity = source.HomeAddress.City;`. This is a fallback for the default
name-matching path only — it never runs when an explicit `[MapProperty]` override is present for
that destination property (see below for how to name a nested path explicitly instead). Every
*intermediate* segment in a matched chain must be non-nullable (a `?`-annotated or `Nullable<T>`
intermediate is excluded from candidates entirely, rather than emitting an unguarded chain that
could throw at runtime) — the leaf property's own nullability is unaffected by this and follows
the same rules as a normal direct match.

If a destination name splits more than one valid way, the match is ambiguous and left unmapped
(`AM010`) rather than guessed:

```csharp
[MapTo(typeof(UserDto))]
public sealed class User
{
    public Home Home { get; set; } = new();
    public Address HomeAddress { get; set; } = new();
}

public sealed class Home
{
    public string AddressCity { get; set; } = "";
}

public sealed class Address
{
    public string City { get; set; } = "";
}

public sealed class UserDto
{
    public string HomeAddressCity { get; set; } = "";
}
```

Here `HomeAddressCity` splits two valid ways — `Home.AddressCity` and `HomeAddress.City` both
resolve — so it's left unmapped. Disambiguate by naming the specific dotted path in an explicit
`[MapProperty]` — unlike the PascalCase search above, an explicit source name is walked exactly
as written (no guessing, so no ambiguity), and it isn't restricted to a top-level property name:

```csharp
[MapTo(typeof(UserDto))]
[MapProperty(typeof(UserDto), "HomeAddress.City", nameof(UserDto.HomeAddressCity))]
public sealed class User
{
    public Home Home { get; set; } = new();
    public Address HomeAddress { get; set; } = new();
}
```

An invalid explicit path — a segment that doesn't exist, or an intermediate segment that's
nullable (the same rule as above) — is reported as `AM021` rather than left unmapped with no
explanation. `[MapUsing]` is an alternative when the destination actually needs a *computed*
value rather than just a different source property:

```csharp
[MapTo(typeof(UserDto))]
[MapUsing(typeof(UserDto), nameof(UserDto.HomeAddressCity), nameof(ResolveHomeAddressCity))]
public sealed class User
{
    public Home Home { get; set; } = new();
    public Address HomeAddress { get; set; } = new();

    public static string ResolveHomeAddressCity(User source) => source.HomeAddress.City;
}
```

A flattened or explicitly-pathed match resolves independently in each direction, so it's **not**
auto-reversed by `GenerateReverse` — see [Reverse mappings](#reverse-mappings) below. Since the
matched chain is just a longer property-access expression, this works identically — with no extra
codegen — in both the imperative mapper and `.ProjectTo{Destination}()`'s SQL projection.

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

`GenerateReverse` works the same way on `[MapFrom]`:

```csharp
[MapFrom(typeof(User), GenerateReverse = true)]
public sealed class UserDto { /* ... */ }
```

also generates both `user.ToUserDto()` and `dto.ToUser()`, still without touching `User`.

Neither `[MapTo]` nor `[MapFrom]` actually cares which of your two types is "the entity" — each
one just means "the decorated type is the source, the type argument is the destination" (for
`[MapTo]`) or "the type argument is the source, the decorated type is the destination" (for
`[MapFrom]`). So `[MapTo(typeof(User))]` placed directly on `UserDto` is equally valid, and
generates `dto.ToUser()` — a DTO-to-entity direction, useful for a create/update command that
populates an entity from an incoming DTO:

```csharp
[MapTo(typeof(User))]
public sealed class UserDto { /* ... */ }
```

If you want both directions declared entirely on the DTO, without `GenerateReverse` tying them
to one shared set of customization attributes, combine `[MapFrom]` and `[MapTo]` on the same
type instead — each direction gets its own `[MapProperty]`/`[MapCondition]`/etc. if they need to
differ:

```csharp
[MapFrom(typeof(User))]  // user.ToUserDto()
[MapTo(typeof(User))]    // dto.ToUser()
public sealed class UserDto { /* ... */ }
```

With this combined form, `[MapProperty]` (and the other customization attributes) needs a little care: both
`[MapFrom(typeof(User))]` and `[MapTo(typeof(User))]` share the same `typeof(User)` argument, which is all
`[MapProperty]` uses to figure out which declaration it belongs to — so **each direction needs its own
`[MapProperty]`, written with the property names in that direction's order**:

```csharp
[MapFrom(typeof(User))]
[MapProperty(typeof(User), nameof(User.Email), nameof(EmailAddress))]   // User -> UserDto
[MapTo(typeof(User))]
[MapProperty(typeof(User), nameof(EmailAddress), nameof(User.Email))]   // UserDto -> User
public sealed class UserDto
{
    public string EmailAddress { get; set; } = "";
}
```

The two don't collide — internally each is keyed by its `destinationProperty` argument (`"EmailAddress"` vs
`"Email"`), and every direction's resolver only ever looks up the one matching the property it's actually
trying to fill in on *its own* destination type; the other entry just goes unused for that direction. But if
you forget the second, oppositely-oriented `[MapProperty]`, the direction that's missing it doesn't error —
it silently falls back to exact-name matching, finds nothing, and reports `AM001` instead of applying the
rename you probably wanted on both sides.

If the rename is symmetric — which it usually is, `Email` ↔ `EmailAddress` either way — this is more setup
than you need: a single declaration with `GenerateReverse = true` (as in the previous example) auto-reverses
`[MapProperty]`'s source/destination for the synthesized reverse direction, so one attribute covers both
directions. Reach for the combined `[MapFrom]`+`[MapTo]` form only when the two directions genuinely need
independent configuration.

## Polymorphic mapping

If your source type has an inheritance hierarchy and you want a runtime-derived instance to
produce a correspondingly richer DTO — a `Dog` should map to a `DogDto` carrying `Breed`, not a
base `AnimalDto` that's missing it — declare one `[MapInclude]` per derived pair on the base
mapping:

```csharp
[MapTo(typeof(AnimalDto))]
[MapInclude(typeof(AnimalDto), typeof(Dog), typeof(DogDto))]
[MapInclude(typeof(AnimalDto), typeof(Cat), typeof(CatDto))]
public class Animal
{
    public string Name { get; set; } = "";
}

[MapTo(typeof(DogDto))]
public class Dog : Animal
{
    public string Breed { get; set; } = "";
}

[MapTo(typeof(CatDto))]
public class Cat : Animal
{
    public bool IsIndoor { get; set; }
}

public class AnimalDto
{
    public string Name { get; set; } = "";
}

public class DogDto : AnimalDto
{
    public string Breed { get; set; } = "";
}

public class CatDto : AnimalDto
{
    public bool IsIndoor { get; set; }
}
```

`animal.ToAnimalDto()` now dispatches on the source's runtime type: a `Dog` instance produces a
`DogDto`, a `Cat` instance produces a `CatDto`, and anything else falls back to the plain
`AnimalDto` mapping. `[MapInclude]`'s three arguments are the base mapping's own destination type
(needed since `[MapTo]` allows more than one destination per source), the derived source type,
and the derived destination type.

Each derived pair (`Dog`/`DogDto`, `Cat`/`CatDto`) needs its own ordinary `[MapTo]`/`[MapFrom]`
declaration — `[MapInclude]` only wires an already-declared mapping into the base type's dispatch,
it doesn't declare one itself; a pair with no mapping of its own is `AM026`. Both the derived
source and derived destination must derive directly (one level) from the base mapping's own
source/destination — checked independently, so a derived source that's correctly related but
whose derived destination *isn't* still reports `AM025`, same as the reverse. A deeper hierarchy
(`Puppy : Dog : Animal`) isn't supported yet — naming `Puppy` directly on `Animal` also reports
`AM025`. Naming the same derived source in more than one `[MapInclude]` is `AM029`; only the last
one is used, since two identical switch arms would otherwise fail to compile.

Like `[MapProperty]`/`[MapCondition]`/`[MapUsing]`/`[MapDefault]`, `[MapInclude]` can also be
declared alongside `[MapFrom]` on the destination side instead of alongside `[MapTo]` on the
source — in that case its first argument names the *source* type (matching `[MapFrom]`'s own
argument), not the destination:

```csharp
[MapFrom(typeof(Animal))]
[MapInclude(typeof(Animal), typeof(Dog), typeof(DogDto))]
public class AnimalDto { /* ... */ }
```

A `[MapInclude]`-carrying mapping doesn't generate a two-argument `To{Dest}(source, destination)`
overload (`AM027`) — there's no way to populate a caller-supplied `AnimalDto` instance as if it
were a `DogDto` instead — and doesn't generate a `ProjectTo{Dest}()` SQL projection (`AM028`) —
a runtime type-switch can't be expressed as a query-provider-translatable expression tree. The
imperative `To{Dest}()` method is otherwise a normal, complete mapping method; use it after
materializing query results if you need the polymorphic dispatch on data that came from a
projection.

The generic dispatcher (`GeneratedMappings.Map<TDestination>(source)`, `IMapper.Map(...)`)
supports polymorphic dispatch too: a `Dog` handed in as a plain `object`/`Animal` still produces a
`DogDto`, resolved by the source's actual runtime type, not whatever static type the caller
happened to use.

`GenerateReverse` isn't supported together with `[MapInclude]` (`AM024`) — reversing a type-switch
has no runtime-type signal to switch back on without a discriminator property. Declare a separate
reverse `[MapTo]`/`[MapFrom]` by hand if you need one.

`MaxDepth` also isn't supported together with `[MapInclude]` — reported the same way as combining
it with a positional record or `init`-only destination (`AM020`, "MaxDepth has no effect"). Take
that warning seriously here specifically: unlike most `AM020` cases, which are genuinely inert,
ignoring it on a polymorphic mapping with an actually-cyclic runtime graph crashes with an
uncatchable `StackOverflowException` — the depth guard is silently never applied to the
base-case fallback, so nothing stops the recursion.

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

then look under `Generated/AnvilMap.Generator/.../GeneratedMappings.g.cs` after building.
`samples/AnvilMap.Sample` in this repo already does this — it's a good place to see real
generated output for a project with nested objects, collections, conditions, and a SQL
projection all together.

## Diagnostics

The generator reports build-time diagnostics instead of failing silently or throwing at runtime:

| ID | Severity | Meaning |
|---|---|---|
| AM001 | Info | Destination property has no matching source and was left unmapped. Add `[MapProperty]` or `[MapIgnore]`. |
| AM002 | Info | The SQL projection was skipped because the mapping graph is cyclic; the imperative method is still generated. |
| AM003 | Error | Incompatible property types — no implicit conversion and no nested mapping declared. |
| AM004 | Error | `[MapCondition]` references a method that doesn't exist or has the wrong signature. |
| AM005 | Info | A `[MapCondition]`-gated property was left out of the SQL projection (still honored by the imperative mapper). |
| AM006 | Warning | The destination has no usable constructor (no parameterless constructor, and no constructor whose parameters all resolve to already-mapped, unconditioned properties) — the mapping was skipped entirely. |
| AM007 | Warning | `[MapCondition]` on an `init`-only destination property isn't supported — the property was left out. |
| AM008 | Info | The two-argument `To{Dest}(source, destination)` overload was omitted because the destination has `init`-only properties. |
| AM009 | Error | `[MapUsing]` references a method that doesn't exist or has the wrong signature/return type. |
| AM010 | Warning | A destination property's name matched more than one valid naming-convention-flattening path — left unmapped rather than guessed. Add a `[MapProperty]` naming the specific dotted path (e.g. `"HomeAddress.City"`) to state which one. |
| AM011 | Warning | The same source/destination pair was declared more than once (`[MapTo]` and/or `[MapFrom]`, including a `GenerateReverse`-implied pair colliding with an explicit declaration). Only the last one encountered is used. |
| AM012 | Warning | A `[MapCondition]`, `[MapUsing]`, `[MapDefault]`, or `[MapProperty]` targets a property that a `[MapIgnore]` already excludes from this same mapping, so the configuration is never consulted. Remove it, or scope the `[MapIgnore]` to a different source type. |
| AM013 | Error | A `required` destination property has no resolved mapping and was left unset — the generated method will fail to compile (`CS9035`). Add a matching source property, a `[MapProperty]` override, a `[MapDefault]`, or remove `required`. |
| AM014 | Warning | A `[MapCondition]` targets a `required` destination property, which isn't supported — a required member can't be conditionally left unset. Remove the `[MapCondition]`, or remove `required` (reported alongside `AM013`, since the property ends up unmapped either way). |
| AM015 | Warning | A `[MapIgnore(typeof(X))]` names a type that's never actually a source for this destination — likely a typo, or left behind after a rename. It has no effect. |
| AM016 | Info | A property has redundant `[MapIgnore]` attributes — an unscoped one alongside a scoped one, or the same source type named more than once. |
| AM017 | Warning | The same destination property is targeted by more than one `[MapProperty]`, `[MapCondition]`, `[MapUsing]`, or `[MapDefault]` in this mapping — only the last one encountered is used. Remove all but one, or make sure they agree. |
| AM018 | Error | A `Nested`/`Enumerable` property's own mapping was itself skipped (e.g. by `AM006`), so there's no generated method to call — the generated code will fail to compile. Fix whatever skipped that mapping (see its own diagnostic), or add a `[MapIgnore]` here. |
| AM019 | Warning | A `[MapDefault]` has no effect: it targets a nested/enumerable property, its value isn't a literal Roslyn can express as an attribute constant, or the property's type can't hold `null`. Remove it, or see `MapDefaultAttribute`'s documentation for what it supports. |
| AM020 | Warning | `MaxDepth` has no effect: either the destination isn't built via plain mutable-property assignment (a positional record, one with `init`-only properties, or a polymorphic `[MapInclude]` mapping, none of which support the depth-guard mechanism), or no property on the mapping is actually self-recursive. |
| AM021 | Warning | An explicit `[MapProperty]` source name doesn't resolve — a plain name that isn't a top-level source property, a dotted path with a segment that doesn't exist, or one with a nullable intermediate segment. Check for a typo, or update the `[MapProperty]` if the source changed. |
| AM022 | Info | An automatic `enum` → `string` conversion (via `.ToString()`) was left out of the SQL projection — most query providers can't translate it. The imperative mapper still applies it. |
| AM023 | Info | An `ImmutableArray<T>`/`ObservableCollection<T>` destination property was left out of the SQL projection — not confirmed translatable by SQL query providers. The imperative mapper still materializes it. |
| AM024 | Warning | `GenerateReverse` was combined with `[MapInclude]`, which isn't supported — reversing a type-switch has no runtime-type signal to switch back on without a discriminator property. The forward mapping is still generated. |
| AM025 | Error | A `[MapInclude]`'s derived source/destination type doesn't derive directly (one level) from the base mapping's own source/destination — that include was skipped. |
| AM026 | Error | A `[MapInclude]`'s derived pair has no `[MapTo]`/`[MapFrom]` declaration of its own — that include was skipped. |
| AM027 | Info | The two-argument `To{Dest}(source, destination)` overload was omitted because it's a polymorphic `[MapInclude]` mapping. |
| AM028 | Info | The SQL projection was skipped because it's a polymorphic `[MapInclude]` mapping — a runtime type-switch can't be expressed as a translatable expression. |
| AM029 | Warning | The same derived source type is named by more than one `[MapInclude]` on this mapping — only the last one is used. |
| AM030 | Warning | A `[MapUsing]`'s `InlineInProjection = true` couldn't inline the converter's body (not a single expression, or it references something that can't be safely inlined) — the projection falls back to a method call instead. |

AM001, AM004, and AM009 have one-click IDE code fixes, included automatically alongside the
generator itself — no separate package reference needed.

### Tuning diagnostic severity

Any `AMxxx` diagnostic above can be reconfigured per project via a plain `.editorconfig` entry —
turn an `Info` up to something you'll actually notice, or silence one you've decided not to act on:

```editorconfig
[*.cs]
dotnet_diagnostic.AM001.severity = suggestion
dotnet_diagnostic.AM010.severity = error
dotnet_diagnostic.AM019.severity = none
```

`severity` accepts `error`, `warning`, `suggestion`, `silent`, or `none` (fully disabled). This is
standard `.editorconfig`/Roslyn behavior, not anything AnvilMap-specific — the same syntax works
for any analyzer's diagnostics, `AMxxx` or otherwise.

## Native AOT

Everything the generator emits — the imperative `To{Dest}()` methods, the dispatcher, `IMapper` —
is plain, direct C# with no reflection at runtime, so it publishes and runs correctly under Native
AOT (`dotnet publish -p:PublishAot=true`). `AnvilMap.Abstractions`'s net8.0 target opts into
`<IsAotCompatible>true</IsAotCompatible>`, turning on the trimmer/AOT analyzer's build-time
warnings. `samples/AnvilMap.Sample.Aot` is a small, EF-Core-free console app that's actually
published with `PublishAot=true` and run — including calling `.Compile()` on a generated
projection field directly, exactly what `IQueryable.Select()` does under the hood for an in-memory
provider — to confirm this rather than just assert it.

**One caveat:** building `{Source}To{Destination}Projection` means compiling an object-initializer
inside an `Expression<Func<...>>`, and the C# compiler does that by calling
`Expression.Bind(MethodInfo, Expression)` under the hood. That method is marked
`[RequiresUnreferencedCode]`, so it trips an `IL2026` trim warning — not a AnvilMap bug,
just what happens whenever you build a member-init expression tree. It only comes up for
destinations that actually need an object-initializer, though: if the destination's constructor
already covers every mapped property (a positional record, say), the projection compiles to a
plain `Expression.New(ctor, args)` and there's nothing to warn about.

For the destinations where it does come up (plain mutable classes, the usual case), the generator
handles it for you: each projection field gets assigned inside an explicit static constructor
tagged with `[DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Dest))]` —
telling the trimmer directly to keep those properties instead of hoping nothing else strips
them — plus `[UnconditionalSuppressMessage("Trimming", "IL2026", ...)]` to clear the now-safe
warning. If nothing in your mapping actually needs this, neither attribute shows up.

Both attributes are absent below net6 (netstandard2.0 included) — emitting them unconditionally
would break compilation for exactly those consumers, so like `FrozenDictionary` and `#nullable`/`!`
elsewhere, this is gated by asking the consumer's own `Compilation` whether the type resolves
(`ConsumerCapabilities.CanSuppressTrimWarnings`), not assumed from a TFM name.

## Interceptor-based dispatch (C# 14)

On a consumer targeting C# 14 (.NET 10+), a call to the generic dispatcher written with both type
arguments given explicitly —

```csharp
UserDto dto = GeneratedMappings.Map<User, UserDto>(user);
```

— that the generator can see at compile time gets redirected via a C# interceptor straight to the
concrete `source.ToDest(...)` method, skipping the `FrozenDictionary` lookup entirely for that
call site. Measured on a flat mapping shape: the one-arg case lands statistically tied with
calling the extension method directly (down from ~1.7x its overhead), and the two-arg case
(already allocation-free, since it populates an existing instance) does better still.

This never applies to `IMapper.Map<TSource,TDestination>(...)` calls, even on C# 14 — interceptors
redirect by source *location*, not by the runtime type behind the receiver, so intercepting an
interface-typed call would silently bypass a mocked/faked `IMapper` in tests. `IMapper` stays
exactly as fast (and exactly as mockable) as it's always been; only a direct call naming
`GeneratedMappings` gets the fast path. It also never applies to the type-erased
`Map<TDestination>(object source)` overload (only the destination is statically known there) or to
a call written with open generic type parameters from an enclosing generic method — both fall back
to the untouched dictionary dispatch, always correctly.

Nothing to opt into: this is automatic, gated per-consumer on the same dynamic-capability-detection
pattern as `FrozenDictionary`/`#nullable` above, and the NuGet package ships a `buildTransitive`
props file that configures the one MSBuild property (`InterceptorsNamespaces`) this needs — no
manual `.csproj` edits required.

## What it doesn't do

Straightforward entity↔DTO shapes are the target use case. Not supported: dictionary mapping,
transitive (multi-level) polymorphic dispatch (`[MapInclude]` only sees one level of inheritance —
see "Polymorphic mapping" above), async mapping, before/after-map hooks, or DI-injected custom
converters (`[MapUsing]` is static-only, by design — no runtime service resolution, matching the
"no runtime cost, no runtime surprises" goal). `MaxDepth` only guards direct self-reference, not
a cycle spanning two different `[MapTo]` declarations.

# AnvilMap

[![CI](https://github.com/LogicR34P3R/AnvilMap/actions/workflows/ci.yml/badge.svg)](https://github.com/LogicR34P3R/AnvilMap/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/AnvilMap.Generator.svg)](https://www.nuget.org/packages/AnvilMap.Generator)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A compile-time, source-generator-based mapper for .NET — a reflection-free replacement for
runtime mappers, built for mapping between database entities and view models, with EF
Core-translatable SQL projection built in.

This page is a landing page — enough to install AnvilMap and see it work. For every attribute,
every diagnostic, and every behavioral edge case, see **[USAGE.md](USAGE.md)**, the canonical
reference; each section below links straight to its part of it.

## Features

- **No runtime reflection** — every mapping method is plain, direct C# emitted at compile time.
- **EF Core-translatable SQL projections** — `ProjectTo{Dest}()` builds an
  `Expression<Func<TSource,TDest>>` from inlined object initializers, not method calls, so it
  actually translates to SQL.
- **Two-way mapping** — `GenerateReverse = true` for the quick path, or independent `[MapTo]` +
  `[MapFrom]` when each direction needs its own condition/converter/default.
- **Polymorphic mapping** (`[MapInclude]`) — a base mapping dispatches on the source's runtime
  type, so a `Dog` produces a `DogDto`, not a base `AnimalDto` missing its `Breed`.
- **Custom converters** (`[MapUsing]`), optionally spliced directly into the SQL projection
  (`InlineInProjection`) instead of staying an opaque call.
- **Conditional mapping, default values, naming-convention flattening** — `[MapCondition]`,
  `[MapDefault]`, and automatic dotted-path matching (`HomeAddressCity` → `source.HomeAddress.City`).
- **Automatic enum conversions** and broad collection-shape support (`List<T>`, arrays,
  `HashSet<T>`, `ImmutableArray<T>`, `ObservableCollection<T>`) with no configuration.
- **Native AOT-compatible** and trimming-safe, verified by an app that's actually published with
  `PublishAot=true` and run, not just annotated.
- **C# 14 interceptor-based dispatch** — a statically-visible generic dispatcher call gets
  redirected straight to the concrete method, automatically, no opt-in required.
- **Fails loudly, not silently** — every gap the generator can't safely fill (an unmapped
  property, an incompatible type, a missing converter method) is a diagnostic (`AM0xx`), several
  with one-click IDE code fixes, not a silent no-op.

## Table of contents

- [Installation](#installation)
- [Quick start](#quick-start)
- [Mapping declaration](#mapping-declaration)
  - [Declaring from the destination side](#declaring-from-the-destination-side)
  - [Reverse mappings](#reverse-mappings)
  - [Customizing a mapping](#customizing-a-mapping)
  - [Naming-convention flattening](#naming-convention-flattening)
  - [Polymorphic mapping](#polymorphic-mapping)
  - [Collection shapes](#collection-shapes)
  - [Diagnostics](#diagnostics)
  - [Recursion guard for self-referential types](#recursion-guard-for-self-referential-types)
  - [Init-only and record destinations](#init-only-and-record-destinations)
- [Native AOT](#native-aot)
- [Interceptor-based dispatch (C# 14)](#interceptor-based-dispatch-c-14)
- [Project layout](#project-layout)
- [Try it](#try-it)
- [Contributing](#contributing)
- [License](#license)

## Installation

```
dotnet add package AnvilMap.Generator
```

That's the only package to add — it pulls in `AnvilMap.Abstractions` (the attributes and
`IMapper`) automatically, and the IDE code fixes ship bundled inside it too. Add it to whichever
project contains your `[MapTo]`/`[MapFrom]`-annotated types.

## Quick start

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

public sealed class UserDto
{
    public int Id { get; set; }
    public string EmailAddress { get; set; } = "";
}
```

```csharp
var dto = user.ToUserDto();           // imperative mapping
var back = dto.ToUser();              // GenerateReverse = true gave us this direction too
var query = dbContext.Users.ProjectToUserDto();  // EF Core-translatable SQL projection
```

The rest of this page is a short tour of what else is available, each pointing at
**[USAGE.md](USAGE.md)** for the full detail.

## Mapping declaration

The mapping configuration lives on the mapping declaration — either the source type (`[MapTo]`)
or the destination type (`[MapFrom]`) — never on either DTO/entity property. For each declared
mapping, the generator emits an imperative `To{Dest}()` method, an EF Core-translatable SQL
projection, and entries in the generic `Map<T>`/`IMapper` dispatcher; see USAGE.md's
**[What gets generated](USAGE.md#what-gets-generated)** table for the exact list.

### Declaring from the destination side

Put `[MapFrom(typeof(Source))]` on the DTO instead of `[MapTo(typeof(Dest))]` on the entity when
the entity shouldn't reference the DTO — same generated methods, just declared from the other
side. Every companion attribute (`[MapProperty]`, `[MapCondition]`, `[MapUsing]`, `[MapDefault]`,
`[MapInclude]`) can move there too, with its `Type` argument flipping to name the source instead.
See USAGE.md's **[Customizing a mapping](USAGE.md#customizing-a-mapping)** for exactly how that
works.

### Reverse mappings

`GenerateReverse = true` gets both directions from one declaration, though `[MapCondition]`/
`[MapUsing]`/`[MapDefault]` aren't carried over automatically. When each direction needs its own
configuration, declare `[MapFrom]` and `[MapTo]` together instead. See USAGE.md's
**[Reverse mappings](USAGE.md#reverse-mappings)** for the full walkthrough, including the one
real footgun (each direction needs its own, oppositely-oriented companion attribute).

### Customizing a mapping

`[MapProperty]` (rename, or a dotted-path source), `[MapIgnore]` (exclude a destination
property), `[MapCondition]` (gate a property behind a runtime check), `[MapUsing]` (compute a
value via a static method — optionally spliced directly into the SQL projection with
`InlineInProjection`), and `[MapDefault]` (substitute a value for `null`) cover everything a
direct name match can't. Enum-to-string/underlying-type conversions happen automatically, no
attribute needed. See USAGE.md's **[Customizing a mapping](USAGE.md#customizing-a-mapping)** for
every attribute's exact signature and diagnostics.

### Naming-convention flattening

A destination property with no exact-name match is tried against a dotted chain of nested source
properties before being left unmapped — `HomeAddressCity` resolves to `source.HomeAddress.City`
automatically. See USAGE.md's
**[Naming-convention flattening](USAGE.md#naming-convention-flattening)** for the matching rules
and what happens when a name resolves more than one way.

### Polymorphic mapping

`[MapInclude]` lets a base mapping dispatch on the source's runtime type, so a `Dog` produces a
`DogDto` carrying `Breed`, not a base `AnimalDto` missing it. Imperative and generic-dispatcher
only — it can't become a SQL projection or a two-argument overload. See USAGE.md's
**[Polymorphic mapping](USAGE.md#polymorphic-mapping)** for the full write-up, including a
`MaxDepth` interaction worth reading carefully if your hierarchy is also self-referential.

### Collection shapes

`List<T>`, arrays, `HashSet<T>`/`ISet<T>`, `ImmutableArray<T>`, and `ObservableCollection<T>`
destination properties are all wired up automatically once their element types have a mapping
between them — no attribute needed. See USAGE.md's
**[Nested objects and collections](USAGE.md#nested-objects-and-collections)** for which shapes
reach `.ProjectTo{Dest}()` and which stay imperative-only.

### Diagnostics

Every gap the generator can't safely fill is a diagnostic (`AM0xx`), not a silent no-op —
`AM001`, `AM004`, and `AM009` have one-click IDE code fixes, bundled in automatically. See
**[USAGE.md's Diagnostics table](USAGE.md#diagnostics)** for the full, current list and how to
tune severity per-diagnostic via `.editorconfig`. IDs are never reused, only retired — see
[CONTRIBUTING.md](CONTRIBUTING.md).

### Recursion guard for self-referential types

`MaxDepth` guards a mapping that maps directly into itself (`Category.Parent` mapping to
`Category`) against unbounded recursion on a cyclic runtime object graph. See USAGE.md's
**[Guarding against cyclic object graphs](USAGE.md#guarding-against-cyclic-object-graphs)** for
the details.

### Init-only and record destinations

`init`-only properties and positional records are both supported — the generator figures out
which and builds the destination via object-initializer or constructor-call syntax accordingly.
See USAGE.md's
**[Init-only and record destinations](USAGE.md#init-only-and-record-destinations)** for exactly
how constructor matching and `required` properties are handled.

## Native AOT

Everything the generator emits is plain, direct C# with no reflection at runtime, so it publishes
and runs correctly under `dotnet publish -p:PublishAot=true` — verified by `samples/AnvilMap.Sample.Aot`,
a console app that's actually published and run under Native AOT, not just annotated. See
USAGE.md's **[Native AOT](USAGE.md#native-aot)** section for the one caveat (a trim warning tied
to the SQL-projection feature specifically) and how the generator handles it for you.

## Interceptor-based dispatch (C# 14)

On a consumer targeting C# 14 (.NET 10+), a statically-visible call to the generic dispatcher
gets redirected via a C# interceptor straight to the concrete method, automatically, skipping the
dictionary lookup entirely — nothing to opt into, and it never touches `IMapper` calls (so mocking
`IMapper` in tests keeps working exactly as before). See USAGE.md's
**[Interceptor-based dispatch](USAGE.md#interceptor-based-dispatch-c-14)** section for the full
reasoning and measured numbers.

## Project layout

- `src/AnvilMap.Abstractions` — attributes (`MapTo`, `MapFrom`, `MapProperty`, `MapIgnore`, `MapCondition`, `MapUsing`, `MapDefault`, `MapInclude`) and the `IMapper` interface.
- `src/AnvilMap.Generator` — the incremental source generator.
- `src/AnvilMap.CodeFixes` — IDE code fixes for the diagnostics that have an unambiguous mechanical fix (`AM001` — add `[MapIgnore]`; `AM004`/`AM009` — generate a stub method).
- `src/AnvilMap.CodeFixContracts` — the small `StubMethodDiagnosticProperties` contract shared between the generator (reports AM004/AM009) and the code fix above (reads them back), instead of a hand-matched, string-keyed dictionary on both ends.
- `samples/AnvilMap.Sample` — a runnable console app mapping EF Core entities (SQLite in-memory) to view models, printing the SQL generated by `ProjectToBlogDto()`.
- `samples/AnvilMap.Sample.Aot` — a small, EF-Core-free console app that publishes and runs under Native AOT (`dotnet publish -r <rid>`, `PublishAot` is set in the project) — see the Native AOT section above.
- `tests/AnvilMap.Generator.Tests` — generator unit tests driven via `CSharpGeneratorDriver`.
- `tests/AnvilMap.CodeFixes.Tests` — code-fix tests driven via an `AdhocWorkspace` running the real generator.
- `benchmarks/AnvilMap.Benchmarks` — BenchmarkDotNet throughput/startup/SQL-projection comparison against AutoMapper.
- `benchmarks/AnvilMap.Benchmarks.ParityTests` — correctness (and SQL-translation) checks that must pass before the benchmark numbers above mean anything.

## Try it

```
dotnet build AnvilMap.sln
dotnet test tests/AnvilMap.Generator.Tests
dotnet test tests/AnvilMap.CodeFixes.Tests
dotnet run --project samples/AnvilMap.Sample
dotnet publish samples/AnvilMap.Sample.Aot -r <rid>   # e.g. win-x64, linux-x64, osx-arm64
```

## Contributing

Bug reports and pull requests are welcome. See **[CONTRIBUTING.md](CONTRIBUTING.md)** for how to
build, test, and where diagnostic IDs get tracked before contributing a change.

## License

[MIT](LICENSE)

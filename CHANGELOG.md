# Changelog

All notable changes to this project are documented here. Format based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); this project adheres to
[Semantic Versioning](https://semver.org/).

## [1.1.0] - 2026-09-01

### Added

- **Polymorphic mapping via `[MapInclude]`.** Map a base type to a base DTO so that a
  runtime-derived instance produces its correspondingly richer derived DTO (e.g. a `Dog` instance
  produces a `DogDto` carrying `Breed`, not a base `AnimalDto` missing it), instead of requiring a
  hand-written switch outside the generator.
- **Automatic enum conversions.** Enum-to-underlying-type and enum-to-`string()` conversions are
  now emitted inline without a hand-written `[MapUsing]` converter.
- **Broader destination collection support.** `ImmutableArray<T>` and `ObservableCollection<T>`
  destinations are now recognized alongside the existing `List<T>`/array/`IEnumerable<T>`/
  `HashSet<T>`-family support.
- **Inline `[MapUsing]` converters into SQL projections.** Opt in with
  `[MapUsing(nameof(Method), InlineInProjection = true)]` to splice a single-expression
  converter's body directly into `.ProjectTo{Dest}()`, instead of emitting a method call EF Core's
  translator can't see into.
- **AM001 now suggests the closest matching source property name** when a destination property
  looks like a typo (e.g. `FirstNam` vs `FirstName`), instead of only offering `[MapIgnore]` as a
  fix.
- 9 new diagnostics, AM022-AM030, covering the features above - see
  `src/AnvilMap.Generator/AnalyzerReleases.Shipped.md` for the full list.

### Fixed

- **AM018 false positive** breaking real builds for same-element-type collection mappings (e.g.
  `List<int>` to `HashSet<int>`), which never actually calls a nested mapping method.
- **Unrecognized destination collection types silently defaulted to `List<T>`.** For example,
  `Dictionary<string,int>` to `IReadOnlyDictionary<string,int>` produced a broken `.ToList()` call
  and a `CS0266` build error with no generator diagnostic pointing at the cause. Unsupported
  shapes now correctly fall through to the existing AM003 diagnostic instead.
- **Incremental generator caching.** `MappingDeclaration`/`ExplicitIncludeMapping` had a broken
  equality contract that could defeat the generator's own per-node incremental caching, causing
  unnecessary full regeneration on unrelated edits. Purely a generator-performance fix; generated
  output is unaffected.

## [1.0.0] - 2026-08-28

Initial public release, published as **AnvilMap**.

### Added

- Compile-time object-to-object mapping via `[MapTo]`/`[MapFrom]`, with no runtime reflection.
- `[MapProperty]`, `[MapCondition]`, `[MapUsing]`, `[MapDefault]`, and `[MapIgnore]`
  attribute-based customization.
- Naming-convention flattening (e.g. `source.Address.City` maps to `destination.AddressCity`).
- SQL-projection generation (`ProjectTo{Dest}()`) for EF Core-translatable queries, alongside
  imperative `To{Dest}()` mapping.
- Interceptor-based direct dispatch for `GeneratedMappings.Map<TSource,TDestination>(...)` call
  sites.
- Roslyn code fixes for common diagnostics.
- AM001-AM021 diagnostics.

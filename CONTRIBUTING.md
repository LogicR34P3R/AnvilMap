# Contributing

## Build & test

```
dotnet build GeneratedMapper.sln
dotnet test tests/GeneratedMapper.Generator.Tests
dotnet test tests/GeneratedMapper.CodeFixes.Tests
dotnet test benchmarks/GeneratedMapper.Benchmarks.ParityTests
```

The second command covers the generator itself; the third covers the IDE code fixes
(`src/GeneratedMapper.CodeFixes`); the fourth covers the AutoMapper comparison's correctness
checks (not the BenchmarkDotNet suite, which is a separate, manually-run thing — see
`benchmarks/GeneratedMapper.Benchmarks`).

## Packaging

`GeneratedMapper.Abstractions` and `GeneratedMapper.Generator` are the only two packable
projects (everything else opts out via `Directory.Build.props`'s default `IsPackable=false`).
To build and locally verify both packages:

```
dotnet pack GeneratedMapper.sln -c Release -o ./nupkg-out
cd smoke-test/ConsumerSmokeTest
dotnet run -c Release
```

`smoke-test/ConsumerSmokeTest` is a real console project, deliberately kept outside
`GeneratedMapper.sln`, that consumes the packed `.nupkg` files from `../../nupkg-out` via a
plain `<PackageReference>` — the same way a real external consumer would, not a
`ProjectReference`/analyzer wired in directly like the rest of this repo. `dotnet pack` alone
isn't a sufficient check: it can succeed while the resulting package silently doesn't work for a
consumer (two real examples this caught: an auto-applied `IncludeAssets` restriction on the
consumer's `PackageReference` that hid the `GeneratedMapper.Abstractions` types entirely, and a
`System.Collections.Immutable` version pinned above what the referenced Roslyn version itself
depends on, breaking generator load in an older SDK). CI runs this same smoke test on every
push/PR (`.github/workflows/ci.yml`). If you change either project's packaging metadata or
dependencies, clear the relevant NuGet cache entries first (`dotnet nuget locals all --clear`,
or just the two `generatedmapper.*` folders under `~/.nuget/packages`) — otherwise a stale
cached package can mask a real regression.

Publishing anywhere (nuget.org or otherwise) is a separate, explicit decision — nothing here
pushes a package anywhere.

## Adding a new diagnostic

Diagnostic IDs (`GM001`, `GM002`, ...) are **never reused, only retired** — once an ID has
shipped, don't repurpose it for something else later, even if the original check is removed.
Consumers may have `.editorconfig` severity overrides or `#pragma warning disable GMxxx`
pinned to a specific ID; silently renumbering breaks those without any warning.

This is mechanically enforced, not just a convention to remember:

1. Add the new `DiagnosticDescriptor` to `src/GeneratedMapper.Generator/Diagnostics.cs` with
   the next unused `GM0xx` ID.
2. Add a row for it to `src/GeneratedMapper.Generator/AnalyzerReleases.Unshipped.md` under
   "New Rules" (ID, category, severity, a short note).
3. Build. If step 2 is skipped, the build produces `RS2000: Rule 'GMxxx' is not part of any
   analyzer release` — that's the Roslyn release-tracking analyzer catching it, wired in via
   `<AdditionalFiles>` in `GeneratedMapper.Generator.csproj`.

If a diagnostic is ever removed, it needs a "Removed Rules" entry in the relevant
`AnalyzerReleases.*.md` file (see the format documented at the top of each file) — not just a
deleted `DiagnosticDescriptor`.

When a real release actually ships, move that release's entries from
`AnalyzerReleases.Unshipped.md` into a new `## Release X.Y.Z` section in
`AnalyzerReleases.Shipped.md`.

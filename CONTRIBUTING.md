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

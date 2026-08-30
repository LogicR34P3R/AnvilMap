; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AM022 | AnvilMap | Info | Property excluded from SQL projection due to an enum-to-string conversion
AM023 | AnvilMap | Info | Property excluded from SQL projection due to an unsupported collection shape
AM024 | AnvilMap | Warning | GenerateReverse is not supported on a [MapInclude] mapping
AM025 | AnvilMap | Error | [MapInclude] type isn't derived from the base mapping's source/destination
AM026 | AnvilMap | Error | [MapInclude] derived pair has no mapping of its own
AM027 | AnvilMap | Info | Two-argument mapper omitted for a polymorphic [MapInclude] mapping
AM028 | AnvilMap | Info | SQL projection not generated for a polymorphic [MapInclude] mapping
AM029 | AnvilMap | Warning | Duplicate [MapInclude] for the same derived source type

; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 0.1.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
GM001 | GeneratedMapper | Info | Destination property has no matching source
GM002 | GeneratedMapper | Info | Projection skipped due to mapping cycle
GM003 | GeneratedMapper | Error | Incompatible property types
GM004 | GeneratedMapper | Error | Condition method not found or has an invalid signature
GM005 | GeneratedMapper | Info | Property excluded from SQL projection due to [MapCondition]
GM006 | GeneratedMapper | Warning | Destination has no accessible parameterless constructor
GM007 | GeneratedMapper | Warning | [MapCondition] on an init-only destination property is not supported
GM008 | GeneratedMapper | Info | Two-argument mapper omitted for init-only destination
GM009 | GeneratedMapper | Error | Converter method not found or has an invalid signature

; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
GM010 | GeneratedMapper | Warning | Ambiguous naming-convention flattening match
GM011 | GeneratedMapper | Warning | Duplicate mapping declaration
GM012 | GeneratedMapper | Warning | An attribute override targets a property excluded by [MapIgnore]
GM013 | GeneratedMapper | Error | Required destination property has no resolved mapping
GM014 | GeneratedMapper | Warning | [MapCondition] on a required destination property is not supported
GM015 | GeneratedMapper | Warning | [MapIgnore] source type doesn't match any declared mapping
GM016 | GeneratedMapper | Info | Redundant [MapIgnore] attributes
GM017 | GeneratedMapper | Warning | Duplicate property-level attribute declaration

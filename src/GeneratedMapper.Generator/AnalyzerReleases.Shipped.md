; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.0.0

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
GM010 | GeneratedMapper | Warning | Ambiguous naming-convention flattening match
GM011 | GeneratedMapper | Warning | Duplicate mapping declaration
GM012 | GeneratedMapper | Warning | An attribute override targets a property excluded by [MapIgnore]
GM013 | GeneratedMapper | Error | Required destination property has no resolved mapping
GM014 | GeneratedMapper | Warning | [MapCondition] on a required destination property is not supported
GM015 | GeneratedMapper | Warning | [MapIgnore] source type doesn't match any declared mapping
GM016 | GeneratedMapper | Info | Redundant [MapIgnore] attributes
GM017 | GeneratedMapper | Warning | Duplicate property-level attribute declaration
GM018 | GeneratedMapper | Error | Nested or element mapping was itself skipped
GM019 | GeneratedMapper | Warning | [MapDefault] has no effect here
GM020 | GeneratedMapper | Warning | [MaxDepth] has no effect here
GM021 | GeneratedMapper | Warning | [MapProperty] source doesn't exist

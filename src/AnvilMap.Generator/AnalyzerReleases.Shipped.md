; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AM001 | AnvilMap | Info | Destination property has no matching source
AM002 | AnvilMap | Info | Projection skipped due to mapping cycle
AM003 | AnvilMap | Error | Incompatible property types
AM004 | AnvilMap | Error | Condition method not found or has an invalid signature
AM005 | AnvilMap | Info | Property excluded from SQL projection due to [MapCondition]
AM006 | AnvilMap | Warning | Destination has no accessible parameterless constructor
AM007 | AnvilMap | Warning | [MapCondition] on an init-only destination property is not supported
AM008 | AnvilMap | Info | Two-argument mapper omitted for init-only destination
AM009 | AnvilMap | Error | Converter method not found or has an invalid signature
AM010 | AnvilMap | Warning | Ambiguous naming-convention flattening match
AM011 | AnvilMap | Warning | Duplicate mapping declaration
AM012 | AnvilMap | Warning | An attribute override targets a property excluded by [MapIgnore]
AM013 | AnvilMap | Error | Required destination property has no resolved mapping
AM014 | AnvilMap | Warning | [MapCondition] on a required destination property is not supported
AM015 | AnvilMap | Warning | [MapIgnore] source type doesn't match any declared mapping
AM016 | AnvilMap | Info | Redundant [MapIgnore] attributes
AM017 | AnvilMap | Warning | Duplicate property-level attribute declaration
AM018 | AnvilMap | Error | Nested or element mapping was itself skipped
AM019 | AnvilMap | Warning | [MapDefault] has no effect here
AM020 | AnvilMap | Warning | [MaxDepth] has no effect here
AM021 | AnvilMap | Warning | [MapProperty] source doesn't exist

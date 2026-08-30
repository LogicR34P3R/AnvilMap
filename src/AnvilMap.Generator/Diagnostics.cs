using Microsoft.CodeAnalysis;

namespace AnvilMap.Generator;

// IDs are assigned in implementation order, not renumbered (AM008 came after AM009). IDs are
// never reused, only retired - see AnalyzerReleases.Shipped.md.
internal static class Diagnostics
{
    public static readonly DiagnosticDescriptor UnmappedDestinationProperty = new(
        id: "AM001",
        title: "Destination property has no matching source",
        messageFormat: "Property '{0}' on '{1}' has no matching source property and was left at its default value. Add a [MapProperty] override or a [MapIgnore] to silence this.",
        category: "AnvilMap",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    // Same AM001 - picked over the above only when NameSuggestion finds a close source property.
    public static readonly DiagnosticDescriptor UnmappedDestinationPropertyWithSuggestion = new(
        id: "AM001",
        title: "Destination property has no matching source",
        messageFormat: "Property '{0}' on '{1}' has no matching source property and was left at its default value. Did you mean '{2}'? Add a [MapProperty] override or a [MapIgnore] to silence this.",
        category: "AnvilMap",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ProjectionCycleSkipped = new(
        id: "AM002",
        title: "Projection skipped due to mapping cycle",
        messageFormat: "A SQL projection for '{0}' -> '{1}' could not be generated because the mapping graph is cyclic. The imperative mapping method is still available; use it after materializing results from the database.",
        category: "AnvilMap",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor IncompatiblePropertyTypes = new(
        id: "AM003",
        title: "Incompatible property types",
        messageFormat: "Cannot map '{0}.{1}' ({2}) to '{3}.{4}' ({5}): no implicit conversion exists and no nested mapping is declared. Add a [MapProperty] with a compatible source, declare a [MapTo] between the two property types, or add [MapIgnore].",
        category: "AnvilMap",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ConditionMethodNotFound = new(
        id: "AM004",
        title: "Condition method not found or has an invalid signature",
        messageFormat: "The [MapCondition] on '{0}' for destination property '{1}' references '{2}'. No accessible 'static bool {2}({0})' or 'static bool {2}({0}, {3}?)' method was found.",
        category: "AnvilMap",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ConditionalPropertyExcludedFromProjection = new(
        id: "AM005",
        title: "Property excluded from SQL projection due to [MapCondition]",
        messageFormat: "Property '{0}' on '{1}' -> '{2}' has a [MapCondition] and was left out of the SQL projection (it will be left at its default value there). The condition is still honored by the imperative mapper.",
        category: "AnvilMap",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NoParameterlessConstructor = new(
        id: "AM006",
        title: "Destination has no accessible parameterless constructor",
        messageFormat: "Mapping from '{0}' to '{1}' was skipped entirely because the destination has an init-only property, no accessible parameterless constructor, and no constructor whose parameters could all be matched to already-mapped, unconditioned properties by name and type (for example, a positional record with a required parameter that has no matching source property, or one gated by [MapCondition]). Add a parameterless constructor, restructure the destination, or make every constructor parameter resolvable.",
        category: "AnvilMap",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ConditionOnInitOnlyPropertyUnsupported = new(
        id: "AM007",
        title: "[MapCondition] on an init-only destination property is not supported",
        messageFormat: "Property '{0}' on '{1}' -> '{2}' combines [MapCondition] with an init-only destination setter, which isn't supported — the property was left out of the generated mapping. Remove the condition or change the destination setter to a regular 'set'.",
        category: "AnvilMap",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ConverterMethodNotFound = new(
        id: "AM009",
        title: "Converter method not found or has an invalid signature",
        messageFormat: "The [MapUsing] on '{0}' for destination property '{1}' references '{2}'. No accessible 'static {3} {2}({0})' method (or one returning a type implicitly convertible to '{3}') was found.",
        category: "AnvilMap",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TwoArgMapperOmittedInitOnly = new(
        id: "AM008",
        title: "Two-argument mapper omitted for init-only destination",
        messageFormat: "The two-argument '{2}(source, destination)' overload (and the IMapper .Map(source, destination) overload) were omitted for '{0}' -> '{1}' because the destination has init-only properties, which can't be assigned after construction. Use '{2}(source)' or Map<TDestination>(source) instead.",
        category: "AnvilMap",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AmbiguousFlattenedMapping = new(
        id: "AM010",
        title: "Ambiguous naming-convention flattening match",
        messageFormat: "Destination property '{0}' on '{1}' has no matching source property, and its name matches more than one possible chain of nested source properties (naming-convention flattening), so it was left unmapped rather than guessing. Add a [MapProperty] naming the specific dotted path (e.g. \"HomeAddress.City\") to state which one, or [MapIgnore] to silence this.",
        category: "AnvilMap",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateMappingDeclaration = new(
        id: "AM011",
        title: "Duplicate mapping declaration",
        messageFormat: "The mapping from '{0}' to '{1}' is declared more than once (via [MapTo] and/or [MapFrom], including one implied by [GenerateReverse]). Only the last one encountered is used - remove all but one, or make sure they agree.",
        category: "AnvilMap",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AttributeOnIgnoredProperty = new(
        id: "AM012",
        title: "An attribute override targets a property excluded by [MapIgnore]",
        messageFormat: "Property '{0}' on '{1}' -> '{2}' has {3} configured, but a [MapIgnore] already excludes it from this mapping, so the configuration is never consulted and the property is always left at its default value here. Remove it, or scope the [MapIgnore] to a different source type.",
        category: "AnvilMap",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor RequiredPropertyUnmapped = new(
        id: "AM013",
        title: "Required destination property has no resolved mapping",
        messageFormat: "Property '{0}' on '{1}' -> '{2}' is declared 'required' but has no resolved mapping and was left unset. The generated mapping method will fail to compile (CS9035, 'required member must be set'). Add a matching source property, a [MapProperty] override, a [MapDefault], or remove the 'required' modifier.",
        category: "AnvilMap",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ConditionOnRequiredPropertyUnsupported = new(
        id: "AM014",
        title: "[MapCondition] on a required destination property is not supported",
        messageFormat: "Property '{0}' on '{1}' -> '{2}' combines [MapCondition] with a 'required' destination property, which isn't supported - a required member has to be set unconditionally wherever the destination is constructed, so it can't also be left unmapped based on a runtime check. The property was left out of the generated mapping (see AM013). Remove the [MapCondition], or remove 'required' from the property.",
        category: "AnvilMap",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MapIgnoreSourceTypeNeverMapped = new(
        id: "AM015",
        title: "[MapIgnore] source type doesn't match any declared mapping",
        messageFormat: "The [MapIgnore(typeof({0}))] on '{1}.{2}' doesn't match any declared mapping into '{1}' - '{0}' is never actually a source for this destination, so this [MapIgnore] has no effect. Check for a typo, or remove it if it's stale.",
        category: "AnvilMap",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor RedundantMapIgnore = new(
        id: "AM016",
        title: "Redundant [MapIgnore] attributes",
        messageFormat: "Property '{0}' on '{1}' has redundant [MapIgnore] attributes - {2}",
        category: "AnvilMap",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicatePropertyAttribute = new(
        id: "AM017",
        title: "Duplicate property-level attribute declaration",
        messageFormat: "The destination property '{0}' on '{1}' is targeted by {2} more than once in this mapping - only the last one encountered is used. Remove all but one, or make sure they agree.",
        category: "AnvilMap",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NestedMappingSkipped = new(
        id: "AM018",
        title: "Nested or element mapping was itself skipped",
        messageFormat: "Property '{0}' on '{1}' -> '{2}' maps via a nested/element mapping from '{3}' to '{4}', but that mapping was itself skipped (see its own diagnostic, e.g. AM006) and has no generated method - the generated code for '{1}' -> '{2}' will fail to compile. Fix whatever skipped '{3}' -> '{4}', or add a [MapIgnore] to '{0}' here.",
        category: "AnvilMap",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MapDefaultHasNoEffect = new(
        id: "AM019",
        title: "[MapDefault] has no effect here",
        messageFormat: "The [MapDefault] for property '{0}' on '{1}' -> '{2}' has no effect: {3}. Remove it, or see MapDefaultAttribute's documentation for what it supports.",
        category: "AnvilMap",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MaxDepthHasNoEffect = new(
        id: "AM020",
        title: "[MaxDepth] has no effect here",
        messageFormat: "The MaxDepth on '{0}' -> '{1}' has no effect: {2}. Remove it, or see MapToAttribute.MaxDepth's documentation for what it guards against.",
        category: "AnvilMap",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MapPropertySourceNotFound = new(
        id: "AM021",
        title: "[MapProperty] source doesn't exist",
        messageFormat: "The [MapProperty] on '{1}' for destination property '{0}' names '{2}' as the source, but {3}. '{0}' was left at its default value - check for a typo, or update the [MapProperty] if the source was renamed.",
        category: "AnvilMap",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EnumToStringExcludedFromProjection = new(
        id: "AM022",
        title: "Property excluded from SQL projection due to an enum-to-string conversion",
        messageFormat: "Property '{0}' on '{1}' -> '{2}' converts an enum to string, which most query providers can't translate, and was left out of the SQL projection (it will be left at its default value there). The imperative mapper still emits it via .ToString().",
        category: "AnvilMap",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CollectionShapeExcludedFromProjection = new(
        id: "AM023",
        title: "Property excluded from SQL projection due to an unsupported collection shape",
        messageFormat: "Property '{0}' on '{1}' -> '{2}' materializes into '{3}', which isn't confirmed translatable by SQL query providers, and was left out of the SQL projection (it will be left at its default value there). The imperative mapper still handles it.",
        category: "AnvilMap",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);
}

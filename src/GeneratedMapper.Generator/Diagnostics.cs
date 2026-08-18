using Microsoft.CodeAnalysis;

namespace GeneratedMapper.Generator;

// GM001-GM009. IDs are assigned in implementation order, not renumbered (GM008 came after
// GM009). IDs are never reused, only retired - see AnalyzerReleases.Shipped.md.
internal static class Diagnostics
{
    public static readonly DiagnosticDescriptor UnmappedDestinationProperty = new(
        id: "GM001",
        title: "Destination property has no matching source",
        messageFormat: "Property '{0}' on '{1}' has no matching source property and was left at its default value. Add a [MapProperty] override or a [MapIgnore] to silence this.",
        category: "GeneratedMapper",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ProjectionCycleSkipped = new(
        id: "GM002",
        title: "Projection skipped due to mapping cycle",
        messageFormat: "A SQL projection for '{0}' -> '{1}' could not be generated because the mapping graph is cyclic. The imperative mapping method is still available; use it after materializing results from the database.",
        category: "GeneratedMapper",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor IncompatiblePropertyTypes = new(
        id: "GM003",
        title: "Incompatible property types",
        messageFormat: "Cannot map '{0}.{1}' ({2}) to '{3}.{4}' ({5}): no implicit conversion exists and no nested mapping is declared. Add a [MapProperty] with a compatible source, declare a [MapTo] between the two property types, or add [MapIgnore].",
        category: "GeneratedMapper",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ConditionMethodNotFound = new(
        id: "GM004",
        title: "Condition method not found or has an invalid signature",
        messageFormat: "The [MapCondition] on '{0}' for destination property '{1}' references '{2}'. No accessible 'static bool {2}({0})' or 'static bool {2}({0}, {3}?)' method was found.",
        category: "GeneratedMapper",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ConditionalPropertyExcludedFromProjection = new(
        id: "GM005",
        title: "Property excluded from SQL projection due to [MapCondition]",
        messageFormat: "Property '{0}' on '{1}' -> '{2}' has a [MapCondition] and was left out of the SQL projection (it will be left at its default value there). The condition is still honored by the imperative mapper.",
        category: "GeneratedMapper",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NoParameterlessConstructor = new(
        id: "GM006",
        title: "Destination has no accessible parameterless constructor",
        messageFormat: "Mapping from '{0}' to '{1}' was skipped entirely because the destination has an init-only property but no accessible parameterless constructor (for example, a positional record with required parameters). Add a parameterless constructor, give all positional parameters defaults, or restructure the destination.",
        category: "GeneratedMapper",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ConditionOnInitOnlyPropertyUnsupported = new(
        id: "GM007",
        title: "[MapCondition] on an init-only destination property is not supported",
        messageFormat: "Property '{0}' on '{1}' -> '{2}' combines [MapCondition] with an init-only destination setter, which isn't supported — the property was left out of the generated mapping. Remove the condition or change the destination setter to a regular 'set'.",
        category: "GeneratedMapper",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ConverterMethodNotFound = new(
        id: "GM009",
        title: "Converter method not found or has an invalid signature",
        messageFormat: "The [MapUsing] on '{0}' for destination property '{1}' references '{2}'. No accessible 'static {3} {2}({0})' method (or one returning a type implicitly convertible to '{3}') was found.",
        category: "GeneratedMapper",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TwoArgMapperOmittedInitOnly = new(
        id: "GM008",
        title: "Two-argument mapper omitted for init-only destination",
        messageFormat: "The two-argument '{2}(source, destination)' overload (and the IMapper .Map(source, destination) overload) were omitted for '{0}' -> '{1}' because the destination has init-only properties, which can't be assigned after construction. Use '{2}(source)' or Map<TDestination>(source) instead.",
        category: "GeneratedMapper",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);
}

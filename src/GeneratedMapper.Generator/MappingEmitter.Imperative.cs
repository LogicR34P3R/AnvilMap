using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace GeneratedMapper.Generator;

internal static partial class MappingEmitter
{
    // Emits the imperative To{Dest}(source) / To{Dest}(source, destination) extension methods
    // for one mapping. One of three shapes comes out, depending on the destination's property
    // set (checked once at the top, since the shape - not just the body - differs):
    //   1. Plain: sequential `destination.Prop = value;` assignments (the common case).
    //   2. Self-recursive with a MaxDepth guard: same as (1), but a private depth-counting
    //      overload is threaded through instead, to stop a cyclic runtime object graph (e.g.
    //      Category.Parent/Children both mapping to Category) from stack-overflowing.
    //   3. Init-only destination: properties that must be set at construction go into an
    //      object-initializer expression instead of sequential assignment; the two-arg
    //      overload is omitted entirely (GM008) since init setters can't be assigned into an
    //      already-constructed instance.
    private static void EmitMapping(StringBuilder sb, MappingModel mapping, System.Action<Diagnostic>? report)
    {
        var source = mapping.Source.FullyQualifiedName;
        var destination = mapping.Destination.FullyQualifiedName;
        var methodName = $"To{mapping.Destination.SimpleName}";

        var initOnlyProperties = mapping.Properties.Where(p => p.DestinationIsInitOnly).ToList();

        if (initOnlyProperties.Count == 0)
        {
            var isSelfRecursive = mapping.MaxDepth > 0 && mapping.Properties.Any(p => IsSelfRecursive(p, mapping));

            sb.AppendLine($"    public static {destination} {methodName}(this {source} source)");
            sb.AppendLine($"        => source.{methodName}(new {destination}());");
            sb.AppendLine();

            if (!isSelfRecursive)
            {
                sb.AppendLine($"    public static {destination} {methodName}(this {source} source, {destination} destination)");
                sb.AppendLine("    {");
                EmitAssignments(sb, mapping.Properties, source);
                sb.AppendLine("        return destination;");
                sb.AppendLine("    }");
                sb.AppendLine();
                return;
            }

            // MaxDepth was declared and this mapping directly references itself (e.g. a
            // Category whose Children/Parent also map to Category) — thread a depth counter
            // through a private overload so a cyclic runtime object graph can't stack-overflow.
            sb.AppendLine($"    public static {destination} {methodName}(this {source} source, {destination} destination)");
            sb.AppendLine($"        => source.{methodName}(destination, 0);");
            sb.AppendLine();

            sb.AppendLine($"    private static {destination} {methodName}(this {source} source, {destination} destination, int depth)");
            sb.AppendLine("    {");
            EmitAssignments(sb, mapping.Properties, source, mapping);
            sb.AppendLine("        return destination;");
            sb.AppendLine("    }");
            sb.AppendLine();
            return;
        }

        // Destination has init-only properties: those must be set via object-initializer
        // syntax at construction, so there's no way to assign them into a pre-existing
        // instance. The two-arg (source, destination) overload is omitted entirely (GM008);
        // any remaining regular-`set` properties are still assigned sequentially afterward.
        report?.Invoke(Diagnostic.Create(
            Diagnostics.TwoArgMapperOmittedInitOnly,
            Location.None,
            mapping.Source.DisplayName,
            mapping.Destination.DisplayName,
            methodName));

        var initializerAssignments = new List<string>();

        foreach (var property in initOnlyProperties)
        {
            if (property.ConditionMethodName is not null)
            {
                report?.Invoke(Diagnostic.Create(
                    Diagnostics.ConditionOnInitOnlyPropertyUnsupported,
                    Location.None,
                    property.DestinationPropertyName,
                    mapping.Source.DisplayName,
                    mapping.Destination.DisplayName));
                continue;
            }

            var value = BuildValueExpression(property, source);

            if (value is null)
                continue;

            initializerAssignments.Add($"{property.DestinationPropertyName} = {value}");
        }

        var remainingProperties = mapping.Properties.Where(p => !p.DestinationIsInitOnly).ToList();

        sb.AppendLine($"    public static {destination} {methodName}(this {source} source)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var destination = new {destination} {{ {string.Join(", ", initializerAssignments)} }};");
        EmitAssignments(sb, remainingProperties, source);
        sb.AppendLine("        return destination;");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    // Emits one assignment statement per property, each optionally wrapped in an `if` guard.
    // A property can be guarded by depth (MaxDepth, self-recursive properties only) and/or by
    // its [MapCondition] method - both guards are ANDed into a single `if` when both apply,
    // rather than nesting, since either one being false means the same thing: leave the
    // destination property at its default.
    private static void EmitAssignments(
        StringBuilder sb,
        IEnumerable<PropertyMappingModel> properties,
        string source,
        MappingModel? recursionContext = null)
    {
        foreach (var property in properties)
        {
            var isRecursive = recursionContext is not null && IsSelfRecursive(property, recursionContext);

            var value = isRecursive
                ? BuildRecursiveValueExpression(property)
                : BuildValueExpression(property, source);

            if (value is null)
                continue;

            var guards = new List<string>();

            if (isRecursive)
                guards.Add($"depth < {recursionContext!.MaxDepth}");

            if (property.ConditionMethodName is not null)
            {
                guards.Add(property.ConditionAcceptsDestination
                    ? $"{source}.{property.ConditionMethodName}(source, destination)"
                    : $"{source}.{property.ConditionMethodName}(source)");
            }

            if (guards.Count == 0)
            {
                sb.AppendLine($"        destination.{property.DestinationPropertyName} = {value};");
                continue;
            }

            sb.AppendLine($"        if ({string.Join(" && ", guards)})");
            sb.AppendLine($"            destination.{property.DestinationPropertyName} = {value};");
        }
    }

    private static bool IsSelfRecursive(PropertyMappingModel property, MappingModel mapping)
        => property.Kind switch
        {
            PropertyMappingKind.Nested =>
                property.SourceType.FullyQualifiedName == mapping.Source.FullyQualifiedName &&
                property.DestinationType.FullyQualifiedName == mapping.Destination.FullyQualifiedName,

            PropertyMappingKind.Enumerable =>
                property.ElementSourceType?.FullyQualifiedName == mapping.Source.FullyQualifiedName &&
                property.ElementDestinationType?.FullyQualifiedName == mapping.Destination.FullyQualifiedName,

            _ => false
        };

    // Calls the private `(source, destination, depth)` overload directly, with `depth + 1`,
    // instead of the normal public To{Dest}() entry point - going through the public method
    // would reset the counter to 0 on every hop and defeat the guard entirely.
    private static string? BuildRecursiveValueExpression(PropertyMappingModel property)
        => property.Kind switch
        {
            PropertyMappingKind.Nested =>
                property.SourceIsNullable
                    ? $"source.{property.SourcePropertyName}?.To{property.DestinationType.SimpleName}(new {property.DestinationType.FullyQualifiedName}(), depth + 1)!"
                    : $"source.{property.SourcePropertyName}.To{property.DestinationType.SimpleName}(new {property.DestinationType.FullyQualifiedName}(), depth + 1)",

            PropertyMappingKind.Enumerable when property.ElementDestinationType is not null =>
                $"source.{property.SourcePropertyName}{(property.SourceIsNullable ? "?." : ".")}Select(x => x.To{property.ElementDestinationType.SimpleName}(new {property.ElementDestinationType.FullyQualifiedName}(), depth + 1)).{MaterializeCall(property.DestinationCollectionShape)}",

            _ => null
        };

    private static string? BuildValueExpression(PropertyMappingModel property, string source)
        => property.Kind switch
        {
            PropertyMappingKind.Direct =>
                $"source.{property.SourcePropertyName}",

            PropertyMappingKind.Nested =>
                property.SourceIsNullable
                    ? $"source.{property.SourcePropertyName}?.To{property.DestinationType.SimpleName}()!"
                    : $"source.{property.SourcePropertyName}.To{property.DestinationType.SimpleName}()",

            PropertyMappingKind.Enumerable =>
                EmitEnumerableImperativeValue(property),

            PropertyMappingKind.Converted =>
                $"{source}.{property.ConverterMethodName}(source)",

            _ => null
        };

    private static string? EmitEnumerableImperativeValue(PropertyMappingModel property)
    {
        if (property.ElementSourceType is null || property.ElementDestinationType is null)
            return null;

        var accessor = property.SourceIsNullable ? "?." : ".";
        var materialize = MaterializeCall(property.DestinationCollectionShape);

        if (property.ElementSourceType.FullyQualifiedName == property.ElementDestinationType.FullyQualifiedName)
            return $"source.{property.SourcePropertyName}{accessor}{materialize}";

        return $"source.{property.SourcePropertyName}{accessor}Select(x => x.To{property.ElementDestinationType.SimpleName}()).{materialize}";
    }
}

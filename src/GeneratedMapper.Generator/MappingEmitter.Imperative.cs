using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace GeneratedMapper.Generator;

internal static partial class MappingEmitter
{
    // Emits To{Dest}(source) / To{Dest}(source, destination). Four shapes:
    //   1. Plain: sequential `destination.Prop = value;` assignments.
    //   2. Self-recursive with MaxDepth: a private depth-counting overload guards a cyclic
    //      runtime graph against stack-overflowing.
    //   3. Constructor-based (e.g. a positional record): required properties passed as
    //      constructor arguments, any remainder via object-initializer/sequential assignment.
    //      Checked first since ConstructorParameterProperties can be set independently of
    //      whether any property happens to be init-only (see MappingResolver.TryMatchConstructor).
    //   4. Init-only destination with a parameterless constructor: object-initializer syntax.
    //   Shapes 3 and 4 both omit the two-arg overload (GM008), since neither can assign into
    //   an already-constructed instance.
    private static void EmitMapping(
        StringBuilder sb,
        MappingModel mapping,
        bool useNullableReferenceTypes,
        System.Action<Diagnostic>? report)
    {
        var source = mapping.Source.FullyQualifiedName;
        var destination = mapping.Destination.FullyQualifiedName;
        var methodName = $"To{mapping.Destination.SimpleName}";

        if (mapping.ConstructorParameterProperties is { Count: > 0 } constructorProperties)
        {
            EmitConstructorBasedMapping(sb, mapping, constructorProperties, source, destination, methodName, useNullableReferenceTypes, report);
            return;
        }

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
                EmitAssignments(sb, mapping.Properties, source, useNullableReferenceTypes);
                sb.AppendLine("        return destination;");
                sb.AppendLine("    }");
                sb.AppendLine();
                return;
            }

            // MaxDepth + self-reference: thread a depth counter through a private overload.
            sb.AppendLine($"    public static {destination} {methodName}(this {source} source, {destination} destination)");
            sb.AppendLine($"        => source.{methodName}(destination, 0);");
            sb.AppendLine();

            sb.AppendLine($"    private static {destination} {methodName}(this {source} source, {destination} destination, int depth)");
            sb.AppendLine("    {");
            EmitAssignments(sb, mapping.Properties, source, useNullableReferenceTypes, mapping);
            sb.AppendLine("        return destination;");
            sb.AppendLine("    }");
            sb.AppendLine();
            return;
        }

        // Init-only properties must be set via object-initializer syntax; the two-arg overload
        // is omitted (GM008), remaining regular-`set` properties are still assigned after.
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

            var value = BuildValueExpression(property, source, useNullableReferenceTypes);

            if (value is null)
                continue;

            initializerAssignments.Add($"{property.DestinationPropertyName} = {value}");
        }

        var remainingProperties = mapping.Properties.Where(p => !p.DestinationIsInitOnly).ToList();

        sb.AppendLine($"    public static {destination} {methodName}(this {source} source)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var destination = new {destination} {{ {string.Join(", ", initializerAssignments)} }};");
        EmitAssignments(sb, remainingProperties, source, useNullableReferenceTypes);
        sb.AppendLine("        return destination;");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    // constructorProperties (in constructor-parameter order) become positional arguments;
    // any other init-only property becomes a trailing object-initializer entry; any regular
    // settable property is assigned afterward via EmitAssignments - the same three destinations
    // a property could go to in the object-initializer-only shape, just with the constructor
    // call taking some of them instead of `new Dest()`.
    private static void EmitConstructorBasedMapping(
        StringBuilder sb,
        MappingModel mapping,
        IReadOnlyList<string> constructorProperties,
        string source,
        string destination,
        string methodName,
        bool useNullableReferenceTypes,
        System.Action<Diagnostic>? report)
    {
        report?.Invoke(Diagnostic.Create(
            Diagnostics.TwoArgMapperOmittedInitOnly,
            Location.None,
            mapping.Source.DisplayName,
            mapping.Destination.DisplayName,
            methodName));

        var byName = mapping.Properties.ToDictionary(p => p.DestinationPropertyName);
        var constructorSet = new HashSet<string>(constructorProperties);

        // Every name in constructorProperties is guaranteed present, unconditioned, and
        // resolvable - MappingResolver.TryMatchConstructor only returns a match when that
        // holds for all of them.
        var constructorArgs = constructorProperties
            .Select(name => BuildValueExpression(byName[name], source, useNullableReferenceTypes)!);

        var initializerAssignments = new List<string>();

        foreach (var property in mapping.Properties.Where(p => p.DestinationIsInitOnly && !constructorSet.Contains(p.DestinationPropertyName)))
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

            var value = BuildValueExpression(property, source, useNullableReferenceTypes);

            if (value is null)
                continue;

            initializerAssignments.Add($"{property.DestinationPropertyName} = {value}");
        }

        var remainingProperties = mapping.Properties
            .Where(p => !p.DestinationIsInitOnly && !constructorSet.Contains(p.DestinationPropertyName))
            .ToList();

        var constructorCall = $"new {destination}({string.Join(", ", constructorArgs)})";
        var construction = initializerAssignments.Count > 0
            ? $"{constructorCall} {{ {string.Join(", ", initializerAssignments)} }}"
            : constructorCall;

        sb.AppendLine($"    public static {destination} {methodName}(this {source} source)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var destination = {construction};");
        EmitAssignments(sb, remainingProperties, source, useNullableReferenceTypes);
        sb.AppendLine("        return destination;");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    // Depth guard and [MapCondition] guard are ANDed into one `if` when both apply - either
    // being false means the same thing: leave the property at its default.
    private static void EmitAssignments(
        StringBuilder sb,
        IEnumerable<PropertyMappingModel> properties,
        string source,
        bool useNullableReferenceTypes,
        MappingModel? recursionContext = null)
    {
        foreach (var property in properties)
        {
            var isRecursive = recursionContext is not null && IsSelfRecursive(property, recursionContext);

            var value = isRecursive
                ? BuildRecursiveValueExpression(property, useNullableReferenceTypes)
                : BuildValueExpression(property, source, useNullableReferenceTypes);

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

    // Calls the private (source, destination, depth) overload directly with depth + 1 - the
    // public entry point would reset the counter each hop. The trailing `!` suppresses a
    // nullable warning; dropped entirely (not just no-op'd) when useNullableReferenceTypes is
    // false, since the `!` syntax itself needs C# 8+.
    private static string? BuildRecursiveValueExpression(PropertyMappingModel property, bool useNullableReferenceTypes)
        => property.Kind switch
        {
            PropertyMappingKind.Nested =>
                property.SourceIsNullable
                    ? $"source.{property.SourcePropertyName}?.To{property.DestinationType.SimpleName}(new {property.DestinationType.FullyQualifiedName}(), depth + 1){(useNullableReferenceTypes ? "!" : "")}"
                    : $"source.{property.SourcePropertyName}.To{property.DestinationType.SimpleName}(new {property.DestinationType.FullyQualifiedName}(), depth + 1)",

            PropertyMappingKind.Enumerable when property.ElementDestinationType is not null =>
                $"source.{property.SourcePropertyName}{(property.SourceIsNullable ? "?." : ".")}Select(x => x.To{property.ElementDestinationType.SimpleName}(new {property.ElementDestinationType.FullyQualifiedName}(), depth + 1)).{MaterializeCall(property.DestinationCollectionShape)}",

            _ => null
        };

    private static string? BuildValueExpression(PropertyMappingModel property, string source, bool useNullableReferenceTypes)
        => property.Kind switch
        {
            PropertyMappingKind.Direct =>
                $"source.{property.SourcePropertyName}",

            PropertyMappingKind.Nested =>
                property.SourceIsNullable
                    ? $"source.{property.SourcePropertyName}?.To{property.DestinationType.SimpleName}(){(useNullableReferenceTypes ? "!" : "")}"
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

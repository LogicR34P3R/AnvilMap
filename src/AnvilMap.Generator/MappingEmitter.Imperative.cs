using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace AnvilMap.Generator;

internal static partial class MappingEmitter
{
    // Emits To{Dest}(source) / To{Dest}(source, destination). Four shapes:
    //   1. Plain: sequential `destination.Prop = value;` assignments. If any property is a
    //      'required' member (C# 11), it's additionally (and redundantly) set inline in the
    //      one-arg overload's `new Dest { ... }` - required is enforced on the object-creation
    //      expression itself, so a bare `new Dest()` followed by a later assignment statement
    //      still fails to compile (CS9035) even though the property does get a value.
    //   2. Self-recursive with MaxDepth: a private depth-counting overload guards a cyclic
    //      runtime graph against stack-overflowing.
    //   3. Constructor-based (e.g. a positional record): properties matched to a constructor
    //      parameter passed as constructor arguments; any other init-only or required property
    //      via object-initializer; the remainder via sequential assignment. Checked first since
    //      ConstructorParameterProperties can be set independently of whether any property
    //      happens to be init-only (see MappingResolver.TryMatchConstructor).
    //   4. Init-only (or required) destination with a parameterless constructor:
    //      object-initializer syntax for those, sequential assignment for the rest.
    //   Shapes 3 and 4 both omit the two-arg overload (AM008), since neither can assign into
    //   an already-constructed instance - unlike shape 1, both are only reached when a true
    //   init-only property forces single-method construction; a required-but-mutable property
    //   alone stays on shape 1 and keeps its two-arg overload, since assigning into an
    //   already-constructed instance is fine for required (only init-only is accessor-enforced).
    private static void EmitMapping(
        CodeWriter writer,
        MappingModel mapping,
        ConsumerCapabilities capabilities,
        System.Action<Diagnostic>? report)
    {
        var useNullableReferenceTypes = capabilities.UseNullableReferenceTypes;
        var source = mapping.Source.FullyQualifiedName;
        var destination = mapping.Destination.FullyQualifiedName;
        var methodName = $"To{mapping.Destination.SimpleName}";
        var (oneArgSummary, twoArgSummary) = BuildMappingSummaries(mapping);

        if (mapping.ConstructorParameterProperties is { Count: > 0 } constructorProperties)
        {
            EmitConstructorBasedMapping(writer, mapping, constructorProperties, source, destination, methodName, capabilities, report);
            return;
        }

        var initOnlyProperties = mapping.Properties.Where(p => p.DestinationIsInitOnly).ToList();

        if (initOnlyProperties.Count == 0)
        {
            var isSelfRecursive = mapping.MaxDepth > 0 && mapping.Properties.Any(p => IsSelfRecursive(p, mapping));
            var newDestinationExpression = BuildNewDestinationExpression(mapping, destination, useNullableReferenceTypes, report);

            writer.Summary(oneArgSummary);
            writer.WriteLine($"public static {destination} {methodName}(this {source} source)");
            using (writer.Indent())
            {
                writer.WriteLine($"=> source.{methodName}({newDestinationExpression});");
            }

            writer.WriteLine();

            if (!isSelfRecursive)
            {
                writer.Summary(twoArgSummary);
                writer.WriteLine($"public static {destination} {methodName}(this {source} source, {destination} destination)");
                using (writer.Block())
                {
                    EmitAssignments(writer, mapping.Properties, useNullableReferenceTypes);
                    writer.WriteLine("return destination;");
                }

                writer.WriteLine();
                return;
            }

            // MaxDepth + self-reference: thread a depth counter through a private overload.
            writer.Summary(twoArgSummary);
            writer.WriteLine($"public static {destination} {methodName}(this {source} source, {destination} destination)");
            using (writer.Indent())
            {
                writer.WriteLine($"=> source.{methodName}(destination, 0);");
            }

            writer.WriteLine();

            writer.WriteLine($"private static {destination} {methodName}(this {source} source, {destination} destination, int depth)");
            using (writer.Block())
            {
                EmitAssignments(writer, mapping.Properties, useNullableReferenceTypes, mapping);
                writer.WriteLine("return destination;");
            }

            writer.WriteLine();
            return;
        }

        // Init-only properties must be set via object-initializer syntax; the two-arg overload
        // is omitted (AM008), remaining regular-`set` properties are still assigned after.
        report?.Invoke(Diagnostic.Create(
            Diagnostics.TwoArgMapperOmittedInitOnly,
            Location.None,
            mapping.Source.DisplayName,
            mapping.Destination.DisplayName,
            methodName));

        // Broadened to also pick up any required-but-mutable property alongside the init-only
        // ones - both need to land in this same object-initializer (see the class-level note).
        var initializerAssignments = BuildMustInitializeAssignments(mapping, mapping.Properties, exclude: null, useNullableReferenceTypes, report);

        var remainingProperties = mapping.Properties.Where(p => !p.DestinationIsInitOnly && !p.DestinationIsRequired).ToList();

        writer.Summary(oneArgSummary);
        writer.WriteLine($"public static {destination} {methodName}(this {source} source)");
        using (writer.Block())
        {
            writer.WriteLine($"var destination = new {destination} {{ {string.Join(", ", initializerAssignments)} }};");
            EmitAssignments(writer, remainingProperties, useNullableReferenceTypes);
            writer.WriteLine("return destination;");
        }

        writer.WriteLine();
    }

    private static (string OneArg, string TwoArg) BuildMappingSummaries(MappingModel mapping)
    {
        var source = CodeWriter.Escape(mapping.Source.DisplayName);
        var destination = CodeWriter.Escape(mapping.Destination.DisplayName);

        return (
            $"Maps a <c>{source}</c> to a new <c>{destination}</c>.",
            $"Maps a <c>{source}</c> onto an existing <c>{destination}</c> instance.");
    }

    // constructorProperties (in constructor-parameter order) become positional arguments;
    // any other init-only property becomes a trailing object-initializer entry; any regular
    // settable property is assigned afterward via EmitAssignments - the same three destinations
    // a property could go to in the object-initializer-only shape, just with the constructor
    // call taking some of them instead of `new Dest()`.
    private static void EmitConstructorBasedMapping(
        CodeWriter writer,
        MappingModel mapping,
        IReadOnlyList<string> constructorProperties,
        string source,
        string destination,
        string methodName,
        ConsumerCapabilities capabilities,
        System.Action<Diagnostic>? report)
    {
        var useNullableReferenceTypes = capabilities.UseNullableReferenceTypes;

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
            .Select(name => BuildValueExpression(byName[name], useNullableReferenceTypes)!);

        // Broadened to also pick up any required-but-mutable property not already claimed by
        // the constructor call, alongside the init-only ones (see the class-level note).
        var initializerAssignments = BuildMustInitializeAssignments(mapping, mapping.Properties, constructorSet, useNullableReferenceTypes, report);

        var remainingProperties = mapping.Properties
            .Where(p => !p.DestinationIsInitOnly && !p.DestinationIsRequired && !constructorSet.Contains(p.DestinationPropertyName))
            .ToList();

        var constructorCall = $"new {destination}({string.Join(", ", constructorArgs)})";
        var construction = initializerAssignments.Count > 0
            ? $"{constructorCall} {{ {string.Join(", ", initializerAssignments)} }}"
            : constructorCall;

        var (oneArgSummary, _) = BuildMappingSummaries(mapping);
        writer.Summary(oneArgSummary);
        writer.WriteLine($"public static {destination} {methodName}(this {source} source)");
        using (writer.Block())
        {
            writer.WriteLine($"var destination = {construction};");
            EmitAssignments(writer, remainingProperties, useNullableReferenceTypes);
            writer.WriteLine("return destination;");
        }

        writer.WriteLine();
    }

    // Shape 1's `new Dest()` call has no properties routed through TryMatchConstructor and no
    // true init-only properties (both handled by other shapes already) - the only reason it
    // would ever need an object-initializer instead of a bare parameterless call is a
    // required-but-mutable property, which still has to be set within this same expression.
    private static string BuildNewDestinationExpression(
        MappingModel mapping, string destination, bool useNullableReferenceTypes, System.Action<Diagnostic>? report)
    {
        var assignments = BuildMustInitializeAssignments(mapping, mapping.Properties, exclude: null, useNullableReferenceTypes, report);

        return assignments.Count > 0
            ? $"new {destination} {{ {string.Join(", ", assignments)} }}"
            : $"new {destination}()";
    }

    // Builds `Name = value` entries for every property that must be set within the same
    // object-initializer/constructor-call expression that constructs the destination: true
    // init-only properties (their accessor kind enforces it) and 'required' properties (C#
    // enforces this on the object-creation expression itself, regardless of accessor kind - a
    // later statement doesn't count, see the class-level note above). MappingResolver already
    // guarantees a 'required' property here never has an active [MapCondition] (AM014); a
    // non-required init-only property still might, and is reported (AM007) and left out of the
    // initializer - safe for init-only (merely optional), which is why only that combination is
    // still allowed to reach this point.
    private static List<string> BuildMustInitializeAssignments(
        MappingModel mapping,
        IEnumerable<PropertyMappingModel> properties,
        HashSet<string>? exclude,
        bool useNullableReferenceTypes,
        System.Action<Diagnostic>? report)
    {
        var assignments = new List<string>();

        foreach (var property in properties)
        {
            if (!(property.DestinationIsInitOnly || property.DestinationIsRequired))
            {
                continue;
            }

            if (exclude is not null && exclude.Contains(property.DestinationPropertyName))
            {
                continue;
            }

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

            var value = BuildValueExpression(property, useNullableReferenceTypes);

            if (value is not null)
            {
                assignments.Add($"{property.DestinationPropertyName} = {value}");
            }
        }

        return assignments;
    }

    // Depth guard and [MapCondition] guard are ANDed into one `if` when both apply - either
    // being false means the same thing: leave the property at its default.
    private static void EmitAssignments(
        CodeWriter writer,
        IEnumerable<PropertyMappingModel> properties,
        bool useNullableReferenceTypes,
        MappingModel? recursionContext = null)
    {
        foreach (var property in properties)
        {
            var isRecursive = recursionContext is not null && IsSelfRecursive(property, recursionContext);

            var value = isRecursive
                ? BuildRecursiveValueExpression(property, useNullableReferenceTypes)
                : BuildValueExpression(property, useNullableReferenceTypes);

            if (value is null)
            {
                continue;
            }

            var guards = new List<string>();

            if (isRecursive)
            {
                guards.Add($"depth < {recursionContext!.MaxDepth}");
            }

            if (property.ConditionMethodName is not null)
            {
                var host = property.MethodHostType!.FullyQualifiedName;
                guards.Add(property.ConditionAcceptsDestination
                    ? $"{host}.{property.ConditionMethodName}(source, destination)"
                    : $"{host}.{property.ConditionMethodName}(source)");
            }

            if (guards.Count == 0)
            {
                writer.WriteLine($"destination.{property.DestinationPropertyName} = {value};");
                continue;
            }

            writer.WriteLine($"if ({string.Join(" && ", guards)})");
            using (writer.Indent())
            {
                writer.WriteLine($"destination.{property.DestinationPropertyName} = {value};");
            }
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

    private static string? BuildValueExpression(PropertyMappingModel property, bool useNullableReferenceTypes)
    {
        var value = property.Kind switch
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
                $"{property.MethodHostType!.FullyQualifiedName}.{property.ConverterMethodName}(source)",

            PropertyMappingKind.EnumConversion =>
                property.EnumConversion == EnumConversionKind.ToUnderlyingType
                    ? $"({property.DestinationType.FullyQualifiedName})source.{property.SourcePropertyName}"
                    : $"source.{property.SourcePropertyName}.ToString()",

            _ => null
        };

        // Set only for Direct/Converted (see PropertyMappingModel) - [MapDefault]'s substitute
        // value, applied via `??` rather than assigned unconditionally.
        return value is not null && property.DefaultValueLiteral is not null
            ? $"{value} ?? {property.DefaultValueLiteral}"
            : value;
    }

    private static string? EmitEnumerableImperativeValue(PropertyMappingModel property)
    {
        if (property.ElementSourceType is null || property.ElementDestinationType is null)
        {
            return null;
        }

        var accessor = property.SourceIsNullable ? "?." : ".";
        var materialize = MaterializeCall(property.DestinationCollectionShape);

        if (property.ElementSourceType.FullyQualifiedName == property.ElementDestinationType.FullyQualifiedName)
        {
            return $"source.{property.SourcePropertyName}{accessor}{materialize}";
        }

        return $"source.{property.SourcePropertyName}{accessor}Select(x => x.To{property.ElementDestinationType.SimpleName}()).{materialize}";
    }
}

using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace GeneratedMapper.Generator;

// Builds ProjectTo{Dest}(): an Expression<Func<TSource, TDest>> from `new Dest { ... }`
// initializers only, never a method call (aside from [MapUsing], the caller's own
// responsibility to keep translatable) - that's what keeps it SQL-translatable, and why this
// can't reuse MappingEmitter.Imperative.cs's method-calling style.
internal static partial class MappingEmitter
{
    private static void EmitProjection(
        StringBuilder sb,
        MappingModel mapping,
        Dictionary<(string Source, string Destination), MappingModel> byPair,
        HashSet<(string Source, string Destination)> mappingsWithOrphanedNestedReference,
        List<string> projectionFieldInitializers,
        HashSet<string> destinationTypesUsingBind,
        System.Action<Diagnostic>? report)
    {
        // `visiting` guards against infinite recursion on a cyclic mapping graph; hitting an
        // already-visiting pair aborts the whole projection rather than truncating it.
        var visiting = new HashSet<(string, string)>();
        var body = BuildProjectionInitializer(mapping, byPair, "source", visiting, destinationTypesUsingBind, report);

        if (body is null)
        {
            // A null body isn't always an actual cycle - a Nested/Enumerable property whose own
            // mapping was dropped from byPair (GM018, already reported by
            // MappingEmitter.ReportOrphanedNestedMappings) hits the exact same code path below.
            // Only report GM002 when this mapping has no such already-explained reason.
            if (!mappingsWithOrphanedNestedReference.Contains((mapping.Source.FullyQualifiedName, mapping.Destination.FullyQualifiedName)))
            {
                report?.Invoke(Diagnostic.Create(
                    Diagnostics.ProjectionCycleSkipped,
                    Location.None,
                    mapping.Source.DisplayName,
                    mapping.Destination.DisplayName));
            }

            return;
        }

        var source = mapping.Source.FullyQualifiedName;
        var destination = mapping.Destination.FullyQualifiedName;
        var simpleName = mapping.Destination.SimpleName;

        // Qualified by source *and* destination simple name - the destination's simple name
        // alone collides whenever two different sources map into the same destination type
        // (e.g. multiple [MapFrom] on one DTO), since a static field can't be overloaded the
        // way the ToXxx extension methods are by their `this TSource` parameter type.
        var fieldName = $"{mapping.Source.SimpleName}To{simpleName}Projection";

        // No inline initializer - assigned in Emit()'s shared explicit static constructor
        // instead, since an implicit one can't carry the trim-suppression attributes it needs.
        sb.AppendLine($"    public static readonly Expression<Func<{source}, {destination}>> {fieldName};");
        sb.AppendLine();

        sb.AppendLine($"    public static IQueryable<{destination}> ProjectTo{simpleName}(this IQueryable<{source}> source)");
        sb.AppendLine($"        => source.Select({fieldName});");
        sb.AppendLine();

        projectionFieldInitializers.Add($"        {fieldName} = source => {body};");
    }

    private static string? BuildProjectionInitializer(
        MappingModel mapping,
        Dictionary<(string Source, string Destination), MappingModel> byPair,
        string sourceExpr,
        HashSet<(string, string)> visiting,
        HashSet<string> destinationTypesUsingBind,
        System.Action<Diagnostic>? report)
    {
        var pairKey = (mapping.Source.FullyQualifiedName, mapping.Destination.FullyQualifiedName);

        if (!visiting.Add(pairKey))
        {
            return null;
        }

        // Non-null only for a destination without a parameterless constructor (e.g. a
        // positional record) - see MappingResolver.TryMatchConstructor. Every name in it is
        // guaranteed unconditioned, so none of them can hit the GM005 branch below.
        var constructorSet = mapping.ConstructorParameterProperties is { Count: > 0 } names
            ? new HashSet<string>(names)
            : null;

        var valueByProperty = new Dictionary<string, string>();
        var assignments = new List<string>();

        foreach (var property in mapping.Properties)
        {
            // [MapCondition]'s method can't appear in an Expression tree, so the property is
            // dropped from the initializer entirely (GM005) - the column is never selected.
            if (property.ConditionMethodName is not null)
            {
                report?.Invoke(Diagnostic.Create(
                    Diagnostics.ConditionalPropertyExcludedFromProjection,
                    Location.None,
                    property.DestinationPropertyName,
                    mapping.Source.DisplayName,
                    mapping.Destination.DisplayName));
                continue;
            }

            string? valueExpr = property.Kind switch
            {
                PropertyMappingKind.Direct =>
                    $"{sourceExpr}.{property.SourcePropertyName}",

                PropertyMappingKind.Nested =>
                    BuildNestedProjection(property, byPair, $"{sourceExpr}.{property.SourcePropertyName}", visiting, destinationTypesUsingBind, report),

                PropertyMappingKind.Enumerable =>
                    BuildEnumerableProjection(property, byPair, sourceExpr, visiting, destinationTypesUsingBind, report),

                PropertyMappingKind.Converted =>
                    $"{property.MethodHostType!.FullyQualifiedName}.{property.ConverterMethodName}({sourceExpr})",

                _ => null
            };

            if (valueExpr is null)
            {
                visiting.Remove(pairKey);
                return null;
            }

            // Set only for Direct/Converted (see PropertyMappingModel) - [MapDefault]'s
            // substitute value. `??` is Expression.Coalesce, translated as SQL COALESCE.
            if (property.DefaultValueLiteral is not null)
            {
                valueExpr = $"{valueExpr} ?? {property.DefaultValueLiteral}";
            }

            if (constructorSet is not null && constructorSet.Contains(property.DestinationPropertyName))
            {
                valueByProperty[property.DestinationPropertyName] = valueExpr;
            }
            else
            {
                assignments.Add($"{property.DestinationPropertyName} = {valueExpr}");
            }
        }

        visiting.Remove(pairKey);

        // A trailing `{ Prop = value }` block is what compiles to Expression.Bind calls; a pure
        // Expression.New(ctor, args) with none left over doesn't, so needs no trim protection.
        if (assignments.Count > 0)
        {
            destinationTypesUsingBind.Add(mapping.Destination.FullyQualifiedName);
        }

        if (constructorSet is null)
        {
            return $"new {mapping.Destination.FullyQualifiedName} {{ {string.Join(", ", assignments)} }}";
        }

        var constructorArgs = new List<string>(mapping.ConstructorParameterProperties!.Count);
        foreach (var name in mapping.ConstructorParameterProperties!)
        {
            constructorArgs.Add(valueByProperty[name]);
        }

        var constructorCall = $"new {mapping.Destination.FullyQualifiedName}({string.Join(", ", constructorArgs)})";
        return assignments.Count > 0 ? $"{constructorCall} {{ {string.Join(", ", assignments)} }}" : constructorCall;
    }

    private static string? BuildNestedProjection(
        PropertyMappingModel property,
        Dictionary<(string Source, string Destination), MappingModel> byPair,
        string sourceExpr,
        HashSet<(string, string)> visiting,
        HashSet<string> destinationTypesUsingBind,
        System.Action<Diagnostic>? report)
    {
        if (!byPair.TryGetValue((property.SourceType.FullyQualifiedName, property.DestinationType.FullyQualifiedName), out var nested))
        {
            return null;
        }

        return BuildProjectionInitializer(nested, byPair, sourceExpr, visiting, destinationTypesUsingBind, report);
    }

    private static string? BuildEnumerableProjection(
        PropertyMappingModel property,
        Dictionary<(string Source, string Destination), MappingModel> byPair,
        string outerSourceExpr,
        HashSet<(string, string)> visiting,
        HashSet<string> destinationTypesUsingBind,
        System.Action<Diagnostic>? report)
    {
        if (property.ElementSourceType is null || property.ElementDestinationType is null)
        {
            return null;
        }

        var elementAccess = $"{outerSourceExpr}.{property.SourcePropertyName}";
        var materialize = MaterializeCall(property.DestinationCollectionShape);

        // Same element type on both sides: no `.Select(...)` projection needed, just
        // materialize the source collection directly into the destination's shape.
        if (property.ElementSourceType.FullyQualifiedName == property.ElementDestinationType.FullyQualifiedName)
        {
            return $"{elementAccess}.{materialize}";
        }

        if (!byPair.TryGetValue(
                (property.ElementSourceType.FullyQualifiedName, property.ElementDestinationType.FullyQualifiedName),
                out var elementMapping))
        {
            return null;
        }

        var elementInitializer = BuildProjectionInitializer(elementMapping, byPair, "x", visiting, destinationTypesUsingBind, report);

        if (elementInitializer is null)
        {
            return null;
        }

        return $"{elementAccess}.Select(x => {elementInitializer}).{materialize}";
    }
}

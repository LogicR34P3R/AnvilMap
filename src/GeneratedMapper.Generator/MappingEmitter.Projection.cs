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
        System.Action<Diagnostic>? report)
    {
        // `visiting` guards against infinite recursion on a cyclic mapping graph; hitting an
        // already-visiting pair aborts the whole projection (GM002) rather than truncating it.
        var visiting = new HashSet<(string, string)>();
        var body = BuildProjectionInitializer(mapping, byPair, "source", visiting, report);

        if (body is null)
        {
            report?.Invoke(Diagnostic.Create(
                Diagnostics.ProjectionCycleSkipped,
                Location.None,
                mapping.Source.DisplayName,
                mapping.Destination.DisplayName));
            return;
        }

        var source = mapping.Source.FullyQualifiedName;
        var destination = mapping.Destination.FullyQualifiedName;
        var simpleName = mapping.Destination.SimpleName;

        sb.AppendLine($"    public static readonly Expression<Func<{source}, {destination}>> To{simpleName}Projection =");
        sb.AppendLine($"        source => {body};");
        sb.AppendLine();

        sb.AppendLine($"    public static IQueryable<{destination}> ProjectTo{simpleName}(this IQueryable<{source}> source)");
        sb.AppendLine($"        => source.Select(To{simpleName}Projection);");
        sb.AppendLine();
    }

    private static string? BuildProjectionInitializer(
        MappingModel mapping,
        Dictionary<(string Source, string Destination), MappingModel> byPair,
        string sourceExpr,
        HashSet<(string, string)> visiting,
        System.Action<Diagnostic>? report)
    {
        var pairKey = (mapping.Source.FullyQualifiedName, mapping.Destination.FullyQualifiedName);

        if (!visiting.Add(pairKey))
            return null;

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
                    BuildNestedProjection(property, byPair, $"{sourceExpr}.{property.SourcePropertyName}", visiting, report),

                PropertyMappingKind.Enumerable =>
                    BuildEnumerableProjection(property, byPair, sourceExpr, visiting, report),

                PropertyMappingKind.Converted =>
                    $"{mapping.Source.FullyQualifiedName}.{property.ConverterMethodName}({sourceExpr})",

                _ => null
            };

            if (valueExpr is null)
            {
                visiting.Remove(pairKey);
                return null;
            }

            assignments.Add($"{property.DestinationPropertyName} = {valueExpr}");
        }

        visiting.Remove(pairKey);
        return $"new {mapping.Destination.FullyQualifiedName} {{ {string.Join(", ", assignments)} }}";
    }

    private static string? BuildNestedProjection(
        PropertyMappingModel property,
        Dictionary<(string Source, string Destination), MappingModel> byPair,
        string sourceExpr,
        HashSet<(string, string)> visiting,
        System.Action<Diagnostic>? report)
    {
        if (!byPair.TryGetValue((property.SourceType.FullyQualifiedName, property.DestinationType.FullyQualifiedName), out var nested))
            return null;

        return BuildProjectionInitializer(nested, byPair, sourceExpr, visiting, report);
    }

    private static string? BuildEnumerableProjection(
        PropertyMappingModel property,
        Dictionary<(string Source, string Destination), MappingModel> byPair,
        string outerSourceExpr,
        HashSet<(string, string)> visiting,
        System.Action<Diagnostic>? report)
    {
        if (property.ElementSourceType is null || property.ElementDestinationType is null)
            return null;

        var elementAccess = $"{outerSourceExpr}.{property.SourcePropertyName}";
        var materialize = MaterializeCall(property.DestinationCollectionShape);

        // Same element type on both sides: no `.Select(...)` projection needed, just
        // materialize the source collection directly into the destination's shape.
        if (property.ElementSourceType.FullyQualifiedName == property.ElementDestinationType.FullyQualifiedName)
            return $"{elementAccess}.{materialize}";

        if (!byPair.TryGetValue(
                (property.ElementSourceType.FullyQualifiedName, property.ElementDestinationType.FullyQualifiedName),
                out var elementMapping))
            return null;

        var elementInitializer = BuildProjectionInitializer(elementMapping, byPair, "x", visiting, report);

        if (elementInitializer is null)
            return null;

        return $"{elementAccess}.Select(x => {elementInitializer}).{materialize}";
    }
}

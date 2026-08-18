using System.Collections.Generic;
using System.Text;

namespace GeneratedMapper.Generator;

// Emits the runtime-dispatch layer: a type-keyed lookup from (source type, destination type)
// to the right generated To{Dest}() call, plus the IMapper service that wraps it. This exists
// for call sites that don't know the concrete source/destination types at compile time (e.g.
// a generic repository, or DI consumers only holding an IMapper) - everywhere else, prefer
// calling the statically-typed To{Dest}() extension method directly, since it skips this
// dictionary lookup entirely (and participates in Find Usages/Rename, unlike a runtime
// dispatch table). The map is built once, from a fixed compile-time-known mapping set, so a
// FrozenDictionary (immutable, optimized for repeated lookups) is a strict improvement over a
// regular Dictionary here - nothing is ever added to it after construction.
internal static partial class MappingEmitter
{
    private static void EmitGenericDispatcher(
        StringBuilder sb,
        IReadOnlyCollection<MappingModel> mappings)
    {
        sb.AppendLine("    private static readonly FrozenDictionary<(Type Source, Type Destination), Func<object, object>> _map =");
        sb.AppendLine("        new Dictionary<(Type, Type), Func<object, object>>");
        sb.AppendLine("        {");

        foreach (var mapping in mappings)
        {
            var source = mapping.Source.FullyQualifiedName;
            var destination = mapping.Destination.FullyQualifiedName;
            var simpleName = mapping.Destination.SimpleName;

            sb.AppendLine($"            [(typeof({source}), typeof({destination}))] = s => (({source})s).To{simpleName}(),");
        }

        sb.AppendLine("        }.ToFrozenDictionary();");
        sb.AppendLine();

        sb.AppendLine("    private static readonly FrozenDictionary<(Type Source, Type Destination), Func<object, object, object>> _mapInto =");
        sb.AppendLine("        new Dictionary<(Type, Type), Func<object, object, object>>");
        sb.AppendLine("        {");

        foreach (var mapping in mappings)
        {
            // Mirrors MappingEmitter.Imperative.cs: no two-arg To{Dest}(source, destination)
            // method exists for init-only destinations (GM008), so there's nothing for this
            // table to dispatch to - IMapper.Map(source, destination) simply won't resolve an
            // entry for that pair at runtime.
            if (HasInitOnlyProperty(mapping))
                continue;

            var source = mapping.Source.FullyQualifiedName;
            var destination = mapping.Destination.FullyQualifiedName;
            var simpleName = mapping.Destination.SimpleName;

            sb.AppendLine($"            [(typeof({source}), typeof({destination}))] = (s, d) => (({source})s).To{simpleName}(({destination})d),");
        }

        sb.AppendLine("        }.ToFrozenDictionary();");
        sb.AppendLine();

        sb.AppendLine("    public static TDestination Map<TDestination>(object source)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (source is null) throw new ArgumentNullException(nameof(source));");
        sb.AppendLine("        if (_map.TryGetValue((source.GetType(), typeof(TDestination)), out var mapper))");
        sb.AppendLine("            return (TDestination)mapper(source);");
        sb.AppendLine(
            "        throw new global::System.InvalidOperationException(");
        sb.AppendLine(
            "            $\"No generated mapping exists from {source.GetType()} to {typeof(TDestination)}.\");");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("    public static TDestination Map<TSource, TDestination>(TSource source)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (source is null) throw new ArgumentNullException(nameof(source));");
        sb.AppendLine("        if (_map.TryGetValue((source.GetType(), typeof(TDestination)), out var mapper))");
        sb.AppendLine("            return (TDestination)mapper(source);");
        sb.AppendLine(
            "        throw new global::System.InvalidOperationException(");
        sb.AppendLine(
            "            $\"No generated mapping exists from {typeof(TSource)} to {typeof(TDestination)}.\");");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("    public static TDestination Map<TSource, TDestination>(TSource source, TDestination destination)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (source is null) throw new ArgumentNullException(nameof(source));");
        sb.AppendLine("        if (destination is null) throw new ArgumentNullException(nameof(destination));");
        sb.AppendLine("        if (_mapInto.TryGetValue((source.GetType(), destination.GetType()), out var mapper))");
        sb.AppendLine("            return (TDestination)mapper(source, destination);");
        sb.AppendLine(
            "        throw new global::System.InvalidOperationException(");
        sb.AppendLine(
            "            $\"No generated mapping exists from {typeof(TSource)} to {typeof(TDestination)}.\");");
        sb.AppendLine("    }");
    }

    private static void EmitMapperService(StringBuilder sb, IReadOnlyCollection<MappingModel> mappings)
    {
        sb.AppendLine("public sealed class GeneratedMapperService : global::GeneratedMapper.IMapper");
        sb.AppendLine("{");
        sb.AppendLine("    public TDestination Map<TDestination>(object source) => GeneratedMappings.Map<TDestination>(source);");
        sb.AppendLine();
        sb.AppendLine("    public TDestination Map<TSource, TDestination>(TSource source) => GeneratedMappings.Map<TSource, TDestination>(source);");
        sb.AppendLine();
        sb.AppendLine("    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination) => GeneratedMappings.Map<TSource, TDestination>(source, destination);");
        sb.AppendLine("}");
    }
}

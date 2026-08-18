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
// FrozenDictionary (immutable, optimized for repeated lookups) would be a strict improvement
// over a regular Dictionary here - nothing is ever added to it after construction - but it only
// exists on .NET 8+, so it's used when the consuming compilation can actually resolve it
// (useFrozenDictionary, computed once in MappingSourceGenerator from that Compilation) and a
// plain Dictionary otherwise. Same lookup semantics either way; only the field's declared type
// and the trailing `.ToFrozenDictionary()` call differ.
internal static partial class MappingEmitter
{
    private static void EmitGenericDispatcher(
        StringBuilder sb,
        IReadOnlyCollection<MappingModel> mappings,
        bool useFrozenDictionary)
    {
        var mapType = useFrozenDictionary
            ? "FrozenDictionary<(Type Source, Type Destination), Func<object, object>>"
            : "Dictionary<(Type Source, Type Destination), Func<object, object>>";
        var mapIntoType = useFrozenDictionary
            ? "FrozenDictionary<(Type Source, Type Destination), Func<object, object, object>>"
            : "Dictionary<(Type Source, Type Destination), Func<object, object, object>>";
        var freeze = useFrozenDictionary ? ".ToFrozenDictionary()" : "";

        // Emitted into the generated file itself (not just this generator's own source) so a
        // consumer opening GeneratedMappings.g.cs can see which implementation they got and why,
        // without needing to know this generator's internals.
        sb.AppendLine(useFrozenDictionary
            ? "    // System.Collections.Frozen.FrozenDictionary is available on this target framework (.NET 8+) - used below."
            : "    // System.Collections.Frozen.FrozenDictionary is not available on this target framework (requires .NET 8+) - falling back to Dictionary.");
        sb.AppendLine($"    private static readonly {mapType} _map =");
        sb.AppendLine("        new Dictionary<(Type, Type), Func<object, object>>");
        sb.AppendLine("        {");

        foreach (var mapping in mappings)
        {
            var source = mapping.Source.FullyQualifiedName;
            var destination = mapping.Destination.FullyQualifiedName;
            var simpleName = mapping.Destination.SimpleName;

            sb.AppendLine($"            [(typeof({source}), typeof({destination}))] = s => (({source})s).To{simpleName}(),");
        }

        sb.AppendLine($"        }}{freeze};");
        sb.AppendLine();

        sb.AppendLine($"    private static readonly {mapIntoType} _mapInto =");
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

        sb.AppendLine($"        }}{freeze};");
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

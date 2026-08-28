using System.Collections.Generic;
using System.Text;

namespace GeneratedMapper.Generator;

// Type-keyed dispatch: (source type, destination type) -> the right To{Dest}() call, plus
// the IMapper service. For callers that don't know concrete types at compile time; prefer the
// statically-typed To{Dest}() extension method otherwise. Uses FrozenDictionary when the
// consumer's target framework has it (.NET 8+), a plain Dictionary otherwise - same lookup
// semantics either way.
internal static partial class MappingEmitter
{
    private static void EmitGenericDispatcher(
        StringBuilder sb,
        IReadOnlyCollection<MappingModel> mappings,
        ConsumerCapabilities capabilities)
    {
        var useFrozenDictionary = capabilities.CanUseFrozenDictionary;
        var mapType = useFrozenDictionary
            ? "FrozenDictionary<(Type Source, Type Destination), Func<object, object>>"
            : "Dictionary<(Type Source, Type Destination), Func<object, object>>";
        var mapIntoType = useFrozenDictionary
            ? "FrozenDictionary<(Type Source, Type Destination), Func<object, object, object>>"
            : "Dictionary<(Type Source, Type Destination), Func<object, object, object>>";
        var freeze = useFrozenDictionary ? ".ToFrozenDictionary()" : "";

        // Emitted into the generated file itself so a consumer can see which implementation
        // they got without reading this generator's source.
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
            // Mirrors the imperative emitter: no two-arg overload for init-only destinations
            // (GM008), so there's nothing for this table to dispatch to.
            if (HasInitOnlyProperty(mapping))
            {
                continue;
            }

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
        // `is null` against an open generic type parameter needs C# 8+ (CS8511 below that);
        // ReferenceEquals works on every version.
        sb.AppendLine("        if (object.ReferenceEquals(source, null)) throw new ArgumentNullException(nameof(source));");
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
        sb.AppendLine("        if (object.ReferenceEquals(source, null)) throw new ArgumentNullException(nameof(source));");
        sb.AppendLine("        if (object.ReferenceEquals(destination, null)) throw new ArgumentNullException(nameof(destination));");
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

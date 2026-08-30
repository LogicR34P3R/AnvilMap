using System.Collections.Generic;

namespace AnvilMap.Generator;

// Type-keyed dispatch: (source type, destination type) -> the right To{Dest}() call, plus
// the IMapper service. For callers that don't know concrete types at compile time; prefer the
// statically-typed To{Dest}() extension method otherwise. Uses FrozenDictionary when the
// consumer's target framework has it (.NET 8+), a plain Dictionary otherwise - same lookup
// semantics either way.
internal static partial class MappingEmitter
{
    private static void EmitGenericDispatcher(
        CodeWriter writer,
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
        writer.WriteLine(useFrozenDictionary
            ? "// System.Collections.Frozen.FrozenDictionary is available on this target framework (.NET 8+) - used below."
            : "// System.Collections.Frozen.FrozenDictionary is not available on this target framework (requires .NET 8+) - falling back to Dictionary.");
        writer.WriteLine($"private static readonly {mapType} _map =");
        using (writer.Indent())
        {
            writer.WriteLine("new Dictionary<(Type, Type), Func<object, object>>");
            using (writer.Block(closeSuffix: $"{freeze};"))
            {
                foreach (var mapping in mappings)
                {
                    var source = mapping.Source.FullyQualifiedName;
                    var destination = mapping.Destination.FullyQualifiedName;
                    var simpleName = mapping.Destination.SimpleName;

                    writer.WriteLine($"[(typeof({source}), typeof({destination}))] = s => (({source})s).To{simpleName}(),");

                    // Keyed by source.GetType() (the runtime type), so a derived type needs its
                    // own entry too, or polymorphic dispatch through this table would never find it.
                    if (mapping.Includes is { Count: > 0 } includes)
                    {
                        foreach (var include in includes)
                        {
                            writer.WriteLine($"[(typeof({include.DerivedSource.FullyQualifiedName}), typeof({destination}))] = s => (({include.DerivedSource.FullyQualifiedName})s).To{include.DerivedDestination.SimpleName}(),");
                        }
                    }
                }
            }
        }

        writer.WriteLine();

        writer.WriteLine($"private static readonly {mapIntoType} _mapInto =");
        using (writer.Indent())
        {
            writer.WriteLine("new Dictionary<(Type, Type), Func<object, object, object>>");
            using (writer.Block(closeSuffix: $"{freeze};"))
            {
                foreach (var mapping in mappings)
                {
                    // Mirrors the imperative emitter: no two-arg overload for init-only
                    // destinations (AM008) or a polymorphic [MapInclude] mapping (AM027), so
                    // there's nothing for this table to dispatch to.
                    if (HasNoTwoArgOverload(mapping))
                    {
                        continue;
                    }

                    var source = mapping.Source.FullyQualifiedName;
                    var destination = mapping.Destination.FullyQualifiedName;
                    var simpleName = mapping.Destination.SimpleName;

                    writer.WriteLine($"[(typeof({source}), typeof({destination}))] = (s, d) => (({source})s).To{simpleName}(({destination})d),");
                }
            }
        }

        writer.WriteLine();

        writer.Summary("Maps <paramref name=\"source\"/> to a new <typeparamref name=\"TDestination\"/> instance, resolved by its runtime type.");
        writer.WriteLine("public static TDestination Map<TDestination>(object source)");
        using (writer.Block())
        {
            writer.WriteLine("if (source is null) throw new ArgumentNullException(nameof(source));");
            writer.WriteLine("if (_map.TryGetValue((source.GetType(), typeof(TDestination)), out var mapper))");
            using (writer.Indent())
            {
                writer.WriteLine("return (TDestination)mapper(source);");
            }

            writer.WriteLine("throw new global::System.InvalidOperationException(");
            using (writer.Indent())
            {
                writer.WriteLine("$\"No generated mapping exists from {source.GetType()} to {typeof(TDestination)}.\");");
            }
        }

        writer.WriteLine();

        writer.Summary("Maps <paramref name=\"source\"/> to a new <typeparamref name=\"TDestination\"/> instance.");
        writer.WriteLine("public static TDestination Map<TSource, TDestination>(TSource source)");
        using (writer.Block())
        {
            // `is null` against an open generic type parameter needs C# 8+ (CS8511 below that);
            // ReferenceEquals works on every version.
            writer.WriteLine("if (object.ReferenceEquals(source, null)) throw new ArgumentNullException(nameof(source));");
            writer.WriteLine("if (_map.TryGetValue((source.GetType(), typeof(TDestination)), out var mapper))");
            using (writer.Indent())
            {
                writer.WriteLine("return (TDestination)mapper(source);");
            }

            writer.WriteLine("throw new global::System.InvalidOperationException(");
            using (writer.Indent())
            {
                writer.WriteLine("$\"No generated mapping exists from {typeof(TSource)} to {typeof(TDestination)}.\");");
            }
        }

        writer.WriteLine();

        writer.Summary("Maps <paramref name=\"source\"/> into the existing <paramref name=\"destination\"/> instance, overwriting its mapped properties in place.");
        writer.WriteLine("public static TDestination Map<TSource, TDestination>(TSource source, TDestination destination)");
        using (writer.Block())
        {
            writer.WriteLine("if (object.ReferenceEquals(source, null)) throw new ArgumentNullException(nameof(source));");
            writer.WriteLine("if (object.ReferenceEquals(destination, null)) throw new ArgumentNullException(nameof(destination));");
            writer.WriteLine("if (_mapInto.TryGetValue((source.GetType(), destination.GetType()), out var mapper))");
            using (writer.Indent())
            {
                writer.WriteLine("return (TDestination)mapper(source, destination);");
            }

            writer.WriteLine("throw new global::System.InvalidOperationException(");
            using (writer.Indent())
            {
                writer.WriteLine("$\"No generated mapping exists from {typeof(TSource)} to {typeof(TDestination)}.\");");
            }
        }
    }

    private static void EmitMapperService(CodeWriter writer, IReadOnlyCollection<MappingModel> mappings)
    {
        using (writer.Block("public sealed class AnvilMapService : global::AnvilMap.IMapper"))
        {
            writer.WriteLine("/// <inheritdoc/>");
            writer.WriteLine("public TDestination Map<TDestination>(object source) => GeneratedMappings.Map<TDestination>(source);");
            writer.WriteLine();
            writer.WriteLine("/// <inheritdoc/>");
            writer.WriteLine("public TDestination Map<TSource, TDestination>(TSource source) => GeneratedMappings.Map<TSource, TDestination>(source);");
            writer.WriteLine();
            writer.WriteLine("/// <inheritdoc/>");
            writer.WriteLine("public TDestination Map<TSource, TDestination>(TSource source, TDestination destination) => GeneratedMappings.Map<TSource, TDestination>(source, destination);");
        }
    }
}

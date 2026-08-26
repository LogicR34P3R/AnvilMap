using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GeneratedMapper.Generator;

/// <summary>
/// Incremental generator entry point: finds every <c>[MapTo]</c>- or <c>[MapFrom]</c>-decorated
/// type across the compilation, resolves each declared mapping's properties via <see cref="MappingResolver"/>
/// (nested/enumerable/converted/conditional matching, diagnostics for anything unmappable),
/// and emits the result as a single <c>GeneratedMappings.g.cs</c> file via
/// <see cref="MappingEmitter"/> - the imperative <c>To{Dest}()</c> extension methods, the
/// <c>ProjectTo{Dest}()</c> SQL-projection extensions, and the runtime dispatcher/
/// <c>IMapper</c> service.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class MappingSourceGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Stage 1 - discovery: ForAttributeWithMetadataName syntactically pre-filters before
        // binding symbols. Each match yields zero or more MappingDeclaration values (one per
        // [MapTo]/[MapFrom] - see MappingDiscovery), which SelectMany flattens. Two separate
        // pipelines - [MapTo] on the source type, [MapFrom] on the destination type - merged
        // into one declarations stream before Collect(); MappingDiscovery normalizes both into
        // the same MappingDeclaration shape, so nothing downstream needs to know which
        // attribute originally declared a given mapping.
        var mapToDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                GeneratorConstants.MapToAttribute,
                predicate: static (node, _) => node is ClassDeclarationSyntax or StructDeclarationSyntax,
                transform: static (ctx, _) => MappingDiscovery.Discover(ctx))
            .SelectMany(static (declarations, _) => declarations);

        var mapFromDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                GeneratorConstants.MapFromAttribute,
                predicate: static (node, _) => node is ClassDeclarationSyntax or StructDeclarationSyntax,
                transform: static (ctx, _) => MappingDiscovery.DiscoverFrom(ctx))
            .SelectMany(static (declarations, _) => declarations);

        var declarations = mapToDeclarations.Collect()
            .Combine(mapFromDeclarations.Collect())
            .Select(static (pair, _) =>
            {
                var (mapTo, mapFrom) = pair;
                return mapTo.AddRange(mapFrom);
            });

        // Collect() forces the whole pipeline to re-run whenever any mapped type changes -
        // accepted, not an oversight: resolving one mapping can depend on any other
        // (MappingResolver needs the full MappingGraph). Measured to stay well under 100ms
        // even at 1,000+ mapped types.
        var combined = context.CompilationProvider.Combine(declarations);

        context.RegisterSourceOutput(combined, static (spc, pair) =>
        {
            var (compilation, allDeclarations) = pair;

            if (allDeclarations.IsDefaultOrEmpty)
                return;

            // Stage 2 - graph construction: every declaration (plus its reverse, if declared)
            // goes into one MappingGraph before resolution starts.
            var graph = new MappingGraph();
            foreach (var declaration in allDeclarations)
                graph.Add(declaration, spc.ReportDiagnostic);

            // [MapIgnore] correctness checks that need every declaration targeting a given
            // destination at once (a stale/typo'd source type, or a redundant combination) -
            // run once per destination here, rather than once per declaration inside
            // MappingResolver.Resolve, which would report the same finding multiple times.
            MapIgnoreValidation.Validate(graph, spc.ReportDiagnostic);

            // Stage 3 - resolution: turn each raw declaration into a fully-matched MappingModel,
            // reporting a diagnostic for anything that couldn't be matched along the way.
            var models = graph.GetMappings()
                .Select(declaration => MappingResolver.Resolve(
                    compilation,
                    graph,
                    declaration,
                    spc.ReportDiagnostic))
                .ToImmutableArray();

            // FrozenDictionary only exists on .NET 8+, with no polyfill - ask the consumer's
            // own Compilation whether it resolves rather than guessing from a TFM name.
            var canUseFrozenDictionary =
                compilation.GetTypeByMetadataName("System.Collections.Frozen.FrozenDictionary`2") is not null;

            // #nullable enable and `!` both need C# 8+; netstandard2.0/net5.0 default below
            // that without an explicit <LangVersion>. Same approach: check the actual
            // compilation instead of guessing.
            var languageVersion = compilation.SyntaxTrees
                .OfType<CSharpSyntaxTree>()
                .Select(t => ((CSharpParseOptions)t.Options).LanguageVersion)
                .DefaultIfEmpty(LanguageVersion.CSharp8)
                .Max();
            var useNullableReferenceTypes = languageVersion >= LanguageVersion.CSharp8;

            // Prerequisite for any future .NET 10/C# 14-gated emission - nothing consumes this
            // yet, this just makes the capability queryable.
            var useCSharp14 = languageVersion >= LanguageVersion.CSharp14;

            // UnconditionalSuppressMessageAttribute/DynamicDependencyAttribute/
            // DynamicallyAccessedMemberTypes shipped together as part of .NET 5/6's trimming
            // annotations - not present on netstandard2.0 or older net TFMs. Same
            // ask-the-Compilation approach as canUseFrozenDictionary above.
            var canSuppressTrimWarnings =
                compilation.GetTypeByMetadataName("System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessageAttribute") is not null;

            var capabilities = new ConsumerCapabilities(canUseFrozenDictionary, useNullableReferenceTypes, useCSharp14, canSuppressTrimWarnings);

            // Stage 4 - emission: one generated file for the whole compilation.
            var source = MappingEmitter.Emit(models, capabilities, spc.ReportDiagnostic);
            spc.AddSource("GeneratedMappings.g.cs", source);
        });
    }
}

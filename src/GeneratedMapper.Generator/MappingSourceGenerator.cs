using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GeneratedMapper.Generator;

/// <summary>
/// Incremental generator entry point: finds every <c>[MapTo]</c>-decorated type across the
/// compilation, resolves each declared mapping's properties via <see cref="MappingResolver"/>
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
        // Stage 1 - discovery: ForAttributeWithMetadataName is Roslyn's fast path for "find
        // every syntax node with this exact attribute", backed by a syntactic pre-filter before
        // it ever binds symbols - much cheaper than a plain SyntaxProvider.CreateSyntaxProvider
        // walking the whole tree. Each matching type yields zero or more MappingDeclaration
        // values (one per [MapTo] on it - see MappingDiscovery), which SelectMany flattens.
        var declarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                GeneratorConstants.MapToAttribute,
                predicate: static (node, _) => node is ClassDeclarationSyntax or StructDeclarationSyntax,
                transform: static (ctx, _) => MappingDiscovery.Discover(ctx))
            .SelectMany(static (declarations, _) => declarations)
            .Collect();

        // Collect() gathers every declaration across the whole compilation into one value,
        // which is what forces the rest of this pipeline to re-run in full whenever *any*
        // mapped type changes (see docs/roadmap.md R4 for why that's an accepted tradeoff, not
        // an oversight) - resolving a single mapping can depend on any other declared mapping
        // (MappingResolver's nested/enumerable resolution needs the full MappingGraph), so
        // there's no correct way to resolve declarations one at a time in isolation.
        var combined = context.CompilationProvider.Combine(declarations);

        context.RegisterSourceOutput(combined, static (spc, pair) =>
        {
            var (compilation, allDeclarations) = pair;

            if (allDeclarations.IsDefaultOrEmpty)
                return;

            // Stage 2 - graph construction: every declaration (plus its auto-generated reverse,
            // if [MapTo(GenerateReverse = true)]) goes into one MappingGraph before anything is
            // resolved, so resolution never has to care about declaration order.
            var graph = new MappingGraph();
            foreach (var declaration in allDeclarations)
                graph.Add(declaration);

            // Stage 3 - resolution: turn each raw declaration into a fully-matched MappingModel,
            // reporting a diagnostic for anything that couldn't be matched along the way.
            var models = graph.GetMappings()
                .Select(declaration => MappingResolver.Resolve(
                    compilation,
                    graph,
                    declaration,
                    spc.ReportDiagnostic))
                .ToImmutableArray();

            // Stage 4 - emission: one generated file for the whole compilation (see
            // docs/roadmapv2.md AD2 for the tradeoffs of splitting this per-mapping instead).
            var source = MappingEmitter.Emit(models, spc.ReportDiagnostic);
            spc.AddSource("GeneratedMappings.g.cs", source);
        });
    }
}

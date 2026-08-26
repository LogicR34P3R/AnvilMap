namespace GeneratedMapper.Generator;

// What the consumer's own Compilation/LanguageVersion supports, computed once in
// MappingSourceGenerator.Initialize and threaded through MappingEmitter.Emit as a single value
// instead of one loose bool parameter per capability. Every flag here is the same "ask the
// Compilation, never assume from a TFM name" pattern: CanUseFrozenDictionary via
// GetTypeByMetadataName, the LanguageVersion-gated flags via the consumer's own CSharpParseOptions.
internal sealed record ConsumerCapabilities(
    bool CanUseFrozenDictionary,
    bool UseNullableReferenceTypes,
    bool UseCSharp14,
    // System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessageAttribute/DynamicDependency/
    // DynamicallyAccessedMemberTypes - part of the trimming annotations that shipped with .NET
    // 5/6's ILLink infrastructure, not present on netstandard2.0 or older net TFMs. A consumer
    // without these can't run the trim/AOT analyzer in the first place, so there's nothing to
    // suppress for them either - see MappingEmitter.cs's projection-field static constructor.
    bool CanSuppressTrimWarnings);

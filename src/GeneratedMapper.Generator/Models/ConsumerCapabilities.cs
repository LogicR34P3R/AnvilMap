namespace GeneratedMapper.Generator;

// What the consumer's own Compilation/LanguageVersion supports, computed once in
// MappingSourceGenerator.Initialize and threaded through MappingEmitter.Emit as a single value -
// see docs/roadmapv3.md R8. Every flag here is the same "ask the Compilation, never assume from
// a TFM name" pattern: CanUseFrozenDictionary via GetTypeByMetadataName, the LanguageVersion-gated
// flags via the consumer's own CSharpParseOptions.
internal sealed record ConsumerCapabilities(
    bool CanUseFrozenDictionary,
    bool UseNullableReferenceTypes,
    bool UseCSharp14);

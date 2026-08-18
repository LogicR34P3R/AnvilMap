namespace GeneratedMapper.Generator;

// Fully-qualified attribute names, matched as plain strings rather than referencing the
// GeneratedMapper.Abstractions types directly - this project doesn't reference that assembly
// at all (it only needs to recognize the attributes by name via Roslyn's
// SyntaxProvider.ForAttributeWithMetadataName/AttributeClass.ToDisplayString(), both of which
// work against any compilation containing a type with this exact name, including the user's
// own compilation where these attributes actually live).
internal static class GeneratorConstants
{
    public const string MapToAttribute = "GeneratedMapper.MapToAttribute";
    public const string MapPropertyAttribute = "GeneratedMapper.MapPropertyAttribute";
    public const string MapIgnoreAttribute = "GeneratedMapper.MapIgnoreAttribute";
    public const string MapConditionAttribute = "GeneratedMapper.MapConditionAttribute";
    public const string MapUsingAttribute = "GeneratedMapper.MapUsingAttribute";
}

namespace AnvilMap.Generator;

// Matched by name, not by referencing AnvilMap.Abstractions directly - this project
// doesn't reference that assembly at all.
internal static class GeneratorConstants
{
    public const string MapToAttribute = "AnvilMap.MapToAttribute";
    public const string MapFromAttribute = "AnvilMap.MapFromAttribute";
    public const string MapPropertyAttribute = "AnvilMap.MapPropertyAttribute";
    public const string MapIgnoreAttribute = "AnvilMap.MapIgnoreAttribute";
    public const string MapConditionAttribute = "AnvilMap.MapConditionAttribute";
    public const string MapUsingAttribute = "AnvilMap.MapUsingAttribute";
    public const string MapDefaultAttribute = "AnvilMap.MapDefaultAttribute";
    public const string MapIncludeAttribute = "AnvilMap.MapIncludeAttribute";
}

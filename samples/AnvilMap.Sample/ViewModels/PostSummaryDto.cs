namespace AnvilMap.Sample.ViewModels;

// Positional record: no parameterless constructor, so the generator builds this via
// constructor arguments instead of object-initializer syntax.
public sealed record PostSummaryDto(int Id, string Headline, int StatusCode);

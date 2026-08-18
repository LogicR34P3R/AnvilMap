namespace GeneratedMapper.Sample.ViewModels;

// Positional record: no parameterless constructor, so the generator builds this via
// constructor arguments (`new PostSummaryDto(source.Id, source.Headline)`) instead of
// object-initializer syntax.
public sealed record PostSummaryDto(int Id, string Headline);

namespace Bearcat.Abstractions.SeriesDatabase;

public record SeriesDatabaseDto(
    string Name,
    string ClassName,
    IReadOnlyList<string> ConfigurationKeys
);

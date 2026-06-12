namespace Bearcat.Abstractions.SeriesDatabase;

public record SeriesInfo(
    string Title,
    string? Description,
    string? CoverUrl,
    string? SeriesDatabaseUrl
);

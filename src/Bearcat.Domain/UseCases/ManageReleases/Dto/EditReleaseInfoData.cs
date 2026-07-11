namespace Bearcat.Domain.UseCases.ManageReleases.Dto;

public record EditReleaseInfoData(
    string? ReleaseName,
    string? CoverUrl,
    string? Genre,
    string? VideoType,
    string? AudioType,
    int? SizeNumber,
    string? SizeUnit,
    string? ReleaseDatabaseUrl,
    string? Description,
    string? ImdbId
);

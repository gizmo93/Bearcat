namespace Bearcat.Abstractions.NfoDatabase;

public record ReleaseInfo(
    string ReleaseName,
    string? ReleaseDatabaseUrl,
    ReleaseInfoSize? Size,
    string? VideoType,
    string? AudioType,
    string? Genre,
    string? Description,
    string? CoverUrl,
    IReadOnlyList<ExternalInfo> ExternalInfos
);

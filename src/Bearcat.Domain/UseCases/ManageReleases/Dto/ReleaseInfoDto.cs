using Bearcat.Abstractions.NfoDatabase;

namespace Bearcat.Domain.UseCases.ManageReleases.Dto;

public record ReleaseInfoDto(
    int ReleaseInfoId,
    string NfoDatabaseClassName,
    string ReleaseName,
    string? ReleaseDatabaseUrl,
    int? SizeNumber,
    string? SizeUnit,
    string? VideoType,
    string? AudioType,
    IReadOnlyList<ReleaseExternalInfoDto> ExternalInfos
);

public record ReleaseExternalInfoDto(
    int ReleaseExternalInfoId,
    ExternalInfoType Type,
    string? Title,
    IReadOnlyList<ReleaseExternalInfoUrlDto> Urls
);

public record ReleaseExternalInfoUrlDto(UrlType Type, string Url);

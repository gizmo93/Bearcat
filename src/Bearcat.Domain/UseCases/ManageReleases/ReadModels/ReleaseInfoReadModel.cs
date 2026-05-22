using Bearcat.Abstractions.NfoDatabase;

namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record ReleaseInfoReadModel(
    int ReleaseInfoId,
    string NfoDatabaseClassName,
    string ReleaseName,
    string? ReleaseDatabaseUrl,
    int? SizeNumber,
    string? SizeUnit,
    string? VideoType,
    string? AudioType,
    IReadOnlyList<ReleaseExternalInfoReadModel> ExternalInfos
);

public record ReleaseExternalInfoReadModel(
    int ReleaseExternalInfoId,
    ExternalInfoType Type,
    string? Title,
    IReadOnlyList<ReleaseExternalInfoUrlReadModel> Urls
);

public record ReleaseExternalInfoUrlReadModel(UrlType Type, string Url);

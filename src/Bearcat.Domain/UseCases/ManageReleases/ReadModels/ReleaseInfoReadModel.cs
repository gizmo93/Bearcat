using Bearcat.Abstractions.NfoDatabase;

namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record ReleaseInfoReadModel(
    string NfoDatabaseClassName,
    string ReleaseName,
    string? ReleaseDatabaseUrl,
    int? SizeNumber,
    string? SizeUnit,
    string? VideoType,
    string? AudioType,
    IReadOnlyList<ReleaseExternalInfoReadModel> ExternalInfos
);

public record ReleaseMetadataReadModel(
    string MetadataDatabaseClassName,
    string Title,
    string? Genre,
    string? Description,
    string? CoverUrl,
    string? MetadataDatabaseUrl
);

public record ReleaseNfoReadModel(int ReleaseNfoId, string FileName, string Content);

public record ReleaseExternalInfoReadModel(
    int ReleaseExternalInfoId,
    ExternalInfoType Type,
    string? Title,
    IReadOnlyList<ReleaseExternalInfoUrlReadModel> Urls
);

public record ReleaseExternalInfoUrlReadModel(UrlType Type, string Url);

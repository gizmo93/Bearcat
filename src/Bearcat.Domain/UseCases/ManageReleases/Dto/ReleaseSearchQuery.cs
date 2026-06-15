using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.Dto;

public record ReleaseSearchQuery(
    string? SearchTerm = null,
    ReleaseType? ReleaseType = null,
    ReleaseContentType? ReleaseContentType = null,
    OnlineState? OnlineState = null,
    int? HosterRegistrationId = null,
    string? ArchiverName = null,
    int? LinkCrypterRegistrationId = null,
    int? ReleaseGroupId = null,
    string? LinksDistributedTo = null,
    string? DownloadLink = null,
    string? ArchiveFileName = null,
    string? UploadId = null,
    int PageIndex = 0,
    int PageSize = 10
);

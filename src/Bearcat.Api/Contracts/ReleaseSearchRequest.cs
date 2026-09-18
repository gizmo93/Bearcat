using Bearcat.Domain.ValueObjects;

namespace Bearcat.Api.Contracts;

public record ReleaseSearchRequest
{
    public string? SearchTerm { get; init; }

    public ReleaseType? ReleaseType { get; init; }

    public ReleaseContentType? ReleaseContentType { get; init; }

    public string? PrimaryLanguageCode { get; init; }

    public OnlineState? OnlineState { get; init; }

    public int? HosterRegistrationId { get; init; }

    public string? ArchiverName { get; init; }

    public int? LinkCrypterRegistrationId { get; init; }

    public int? ReleaseGroupId { get; init; }

    public string? PostedLocationUrl { get; init; }

    public string? DownloadLink { get; init; }

    public string? ArchiveFileName { get; init; }

    public string? UploadId { get; init; }

    public int PageIndex { get; init; } = 0;

    public int PageSize { get; init; } = 10;
}

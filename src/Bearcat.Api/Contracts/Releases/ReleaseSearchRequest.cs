using Bearcat.Domain.ValueObjects;

namespace Bearcat.Api.Contracts.Releases;

public record ReleaseSearchRequest
{
    /// <summary>
    /// Case-insensitive substring match on the release name and the release folder path.
    /// </summary>
    public string? SearchTerm { get; init; }

    public ReleaseType? ReleaseType { get; init; }

    public ReleaseContentType? ReleaseContentType { get; init; }

    public string? PrimaryLanguageCode { get; init; }

    public OnlineState? OnlineState { get; init; }

    public int? HosterRegistrationId { get; init; }

    public string? ArchiverName { get; init; }

    public int? LinkCrypterRegistrationId { get; init; }

    public int? ReleaseGroupId { get; init; }

    /// <summary>
    /// Only return releases posted to a location whose url contains this value.
    /// For example "https://www.my-forum.com" will list all releases that were posted to that forum.
    /// </summary>
    public string? PostedLocationUrl { get; init; }

    /// <summary>
    /// Only return releases that have an uploaded file whose hoster link contains this value.
    /// </summary>
    public string? DownloadLink { get; init; }

    /// <summary>
    /// Only return releases that have an archive file whose name contains this value.
    /// </summary>
    public string? ArchiveFileName { get; init; }

    /// <summary>
    /// Only return the release of this upload id
    /// </summary>
    public string? UploadId { get; init; }

    public int PageIndex { get; init; } = 0;

    public int PageSize { get; init; } = 10;
}

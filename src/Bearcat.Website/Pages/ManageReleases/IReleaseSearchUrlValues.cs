namespace Bearcat.Website.Pages.ManageReleases;

public interface IReleaseSearchUrlValues
{
    string? SearchTerm { get; }
    string? ReleaseType { get; }
    string? ReleaseContentType { get; }
    string? Language { get; }
    string? OnlineState { get; }
    int? HosterRegistrationId { get; }
    string? ArchiverName { get; }
    int? LinkCrypterRegistrationId { get; }
    int? ReleaseGroupId { get; }
    string? PostedLocationUrl { get; }
    string? DownloadLink { get; }
    string? ArchiveFileName { get; }
    string? UploadId { get; }
    int? Page { get; }
    int? PageSize { get; }
}

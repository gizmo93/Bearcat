using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.ManageReleases;

public class ReleaseSearchFormModel
{
    public const string LanguageNotSetFilter = "__not_set__";

    public string? SearchTerm { get; set; }
    public ReleaseType? ReleaseType { get; set; }
    public ReleaseContentType? ReleaseContentType { get; set; }
    public string PrimaryLanguageCode { get; set; } = string.Empty;
    public OnlineState? OnlineState { get; set; }
    public int? HosterRegistrationId { get; set; }
    public string ArchiverName { get; set; } = string.Empty;
    public int? LinkCrypterRegistrationId { get; set; }
    public int ReleaseGroupId { get; set; }
    public string? PostedLocationUrl { get; set; }
    public string? DownloadLink { get; set; }
    public string? ArchiveFileName { get; set; }
    public string? UploadId { get; set; }

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchTerm)
        || !string.IsNullOrWhiteSpace(PostedLocationUrl)
        || HasActiveAdvancedFilters
        || ReleaseType is not null
        || ReleaseContentType is not null
        || !string.IsNullOrWhiteSpace(PrimaryLanguageCode)
        || OnlineState is not null
        || HosterRegistrationId is not null
        || !string.IsNullOrWhiteSpace(ArchiverName)
        || LinkCrypterRegistrationId is not null
        || ReleaseGroupId != 0;

    public bool HasActiveAdvancedFilters =>
        !string.IsNullOrWhiteSpace(DownloadLink)
        || !string.IsNullOrWhiteSpace(ArchiveFileName)
        || !string.IsNullOrWhiteSpace(UploadId);

    public static ReleaseSearchFormModel FromQuery(ReleaseSearchQuery query)
    {
        return new ReleaseSearchFormModel
        {
            SearchTerm = query.SearchTerm,
            ReleaseType = query.ReleaseType,
            ReleaseContentType = query.ReleaseContentType,
            PrimaryLanguageCode = query.PrimaryLanguageCode switch
            {
                null => string.Empty,
                "" => LanguageNotSetFilter,
                var code => code,
            },
            OnlineState = query.OnlineState,
            HosterRegistrationId = query.HosterRegistrationId,
            ArchiverName = query.ArchiverName ?? string.Empty,
            LinkCrypterRegistrationId = query.LinkCrypterRegistrationId,
            ReleaseGroupId = query.ReleaseGroupId ?? 0,
            PostedLocationUrl = query.PostedLocationUrl,
            DownloadLink = query.DownloadLink,
            ArchiveFileName = query.ArchiveFileName,
            UploadId = query.UploadId,
        };
    }

    public ReleaseSearchQuery ToQuery()
    {
        return new ReleaseSearchQuery(
            SearchTerm: SearchTerm,
            ReleaseType: ReleaseType,
            ReleaseContentType: ReleaseContentType,
            PrimaryLanguageCode: PrimaryLanguageCode switch
            {
                LanguageNotSetFilter => string.Empty,
                "" => null,
                _ => PrimaryLanguageCode,
            },
            OnlineState: OnlineState,
            HosterRegistrationId: HosterRegistrationId,
            ArchiverName: string.IsNullOrWhiteSpace(ArchiverName) ? null : ArchiverName,
            LinkCrypterRegistrationId: LinkCrypterRegistrationId,
            ReleaseGroupId: ReleaseGroupId == 0 ? null : ReleaseGroupId,
            PostedLocationUrl: PostedLocationUrl,
            DownloadLink: DownloadLink,
            ArchiveFileName: ArchiveFileName,
            UploadId: UploadId
        );
    }
}

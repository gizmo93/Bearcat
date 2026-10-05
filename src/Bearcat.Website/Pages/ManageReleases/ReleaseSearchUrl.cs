using System.Globalization;
using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Shared;

namespace Bearcat.Website.Pages.ManageReleases;

public static class ReleaseSearchUrl
{
    private const string BasePath = "/releases";
    private const string LanguageNotSetValue = "none";

    public const ReleaseSearchSortOrder DefaultSortOrder =
        ReleaseSearchSortOrder.CreatedAtDescending;

    public static SearchUrlState<ReleaseSearchQuery> Parse(IReleaseSearchUrlValues values)
    {
        var query = new ReleaseSearchQuery(
            SearchTerm: SearchUrlParameters.NormalizeText(values.SearchTerm),
            ReleaseType: SearchUrlParameters.ParseEnum<ReleaseType>(values.ReleaseType),
            ReleaseContentType: SearchUrlParameters.ParseEnum<ReleaseContentType>(
                values.ReleaseContentType
            ),
            PrimaryLanguageCode: SearchUrlParameters.NormalizeText(values.Language) switch
            {
                null => null,
                LanguageNotSetValue => string.Empty,
                var code => code,
            },
            OnlineState: SearchUrlParameters.ParseEnum<OnlineState>(values.OnlineState),
            HosterRegistrationId: values.HosterRegistrationId,
            ArchiverName: SearchUrlParameters.NormalizeText(values.ArchiverName),
            LinkCrypterRegistrationId: values.LinkCrypterRegistrationId,
            ReleaseGroupId: SearchUrlParameters.NullWhenZero(values.ReleaseGroupId),
            PostedLocationUrl: SearchUrlParameters.NormalizeText(values.PostedLocationUrl),
            DownloadLink: SearchUrlParameters.NormalizeText(values.DownloadLink),
            ArchiveFileName: SearchUrlParameters.NormalizeText(values.ArchiveFileName),
            UploadId: SearchUrlParameters.NormalizeText(values.UploadId),
            SortOrder: SearchUrlParameters.ParseEnum<ReleaseSearchSortOrder>(values.SortOrder)
                ?? DefaultSortOrder
        );

        return new SearchUrlState<ReleaseSearchQuery>(
            query,
            SearchUrlParameters.ParsePageIndex(values.Page),
            SearchUrlParameters.ParsePageSize(values.PageSize)
        );
    }

    public static string Build(ReleaseSearchQuery query, int page, int pageSize)
    {
        (string Key, string? Value)[] filterParameters =
        [
            ("q", SearchUrlParameters.NormalizeText(query.SearchTerm)),
            ("type", query.ReleaseType?.ToString()),
            ("content", query.ReleaseContentType?.ToString()),
            (
                "lang",
                query.PrimaryLanguageCode switch
                {
                    "" => LanguageNotSetValue,
                    var code => code,
                }
            ),
            ("state", query.OnlineState?.ToString()),
            ("hoster", query.HosterRegistrationId?.ToString(CultureInfo.InvariantCulture)),
            ("archiver", SearchUrlParameters.NormalizeText(query.ArchiverName)),
            ("crypter", query.LinkCrypterRegistrationId?.ToString(CultureInfo.InvariantCulture)),
            ("group", query.ReleaseGroupId?.ToString(CultureInfo.InvariantCulture)),
            ("posted", SearchUrlParameters.NormalizeText(query.PostedLocationUrl)),
            ("link", SearchUrlParameters.NormalizeText(query.DownloadLink)),
            ("file", SearchUrlParameters.NormalizeText(query.ArchiveFileName)),
            ("upload", SearchUrlParameters.NormalizeText(query.UploadId)),
            ("sort", query.SortOrder == DefaultSortOrder ? null : query.SortOrder.ToString()),
        ];

        return SearchUrlParameters.Build(BasePath, filterParameters, page, pageSize);
    }
}

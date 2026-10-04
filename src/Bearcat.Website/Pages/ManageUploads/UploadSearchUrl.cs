using System.Globalization;
using Bearcat.Domain.UseCases.ManageUploads.Dto;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Shared;

namespace Bearcat.Website.Pages.ManageUploads;

public static class UploadSearchUrl
{
    private const string BasePath = "/uploads";

    public static SearchUrlState<UploadSearchQuery> Parse(IUploadSearchUrlValues values)
    {
        var query = new UploadSearchQuery(
            SearchTerm: SearchUrlParameters.NormalizeText(values.SearchTerm),
            UploadState: SearchUrlParameters.ParseEnum<UploadState>(values.UploadState),
            OnlineState: SearchUrlParameters.ParseEnum<OnlineState>(values.OnlineState),
            HosterRegistrationId: SearchUrlParameters.NullWhenZero(values.HosterRegistrationId),
            ReleaseGroupId: SearchUrlParameters.NullWhenZero(values.ReleaseGroupId)
        );

        return new SearchUrlState<UploadSearchQuery>(
            query,
            SearchUrlParameters.ParsePageIndex(values.Page),
            SearchUrlParameters.ParsePageSize(values.PageSize)
        );
    }

    public static string Build(UploadSearchQuery query, int page, int pageSize)
    {
        (string Key, string? Value)[] filterParameters =
        [
            ("q", SearchUrlParameters.NormalizeText(query.SearchTerm)),
            ("state", query.UploadState?.ToString()),
            ("online", query.OnlineState?.ToString()),
            ("hoster", query.HosterRegistrationId?.ToString(CultureInfo.InvariantCulture)),
            ("group", query.ReleaseGroupId?.ToString(CultureInfo.InvariantCulture)),
        ];

        return SearchUrlParameters.Build(BasePath, filterParameters, page, pageSize);
    }
}

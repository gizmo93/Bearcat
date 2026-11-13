using System.Globalization;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Dto;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Shared;

namespace Bearcat.Website.Pages.ManageReleaseCollections.Search;

public static class ReleaseCollectionSearchUrl
{
    private const string BasePath = "/release-collections";
    private const string LanguageNotSetValue = "none";

    public const ReleaseCollectionSearchSortOrder DefaultSortOrder =
        ReleaseCollectionSearchSortOrder.NameAscending;

    public static SearchUrlState<ReleaseCollectionSearchQuery> Parse(
        IReleaseCollectionSearchUrlValues values
    )
    {
        var query = new ReleaseCollectionSearchQuery(
            SearchTerm: SearchUrlParameters.NormalizeText(values.SearchTerm),
            ReleaseContentType: SearchUrlParameters.ParseEnum<ReleaseContentType>(
                values.ReleaseContentType
            ),
            ReleaseGroupId: SearchUrlParameters.NullWhenZero(values.ReleaseGroupId),
            OnlineState: SearchUrlParameters.ParseEnum<OnlineState>(values.OnlineState),
            PrimaryLanguageCode: SearchUrlParameters.NormalizeText(values.Language) switch
            {
                null => null,
                LanguageNotSetValue => string.Empty,
                var code => code,
            },
            SortOrder: SearchUrlParameters.ParseEnum<ReleaseCollectionSearchSortOrder>(
                values.SortOrder
            ) ?? DefaultSortOrder
        );

        return new SearchUrlState<ReleaseCollectionSearchQuery>(
            Query: query,
            PageIndex: SearchUrlParameters.ParsePageIndex(values.Page),
            PageSize: SearchUrlParameters.ParsePageSize(values.PageSize)
        );
    }

    public static string Build(ReleaseCollectionSearchQuery query, int page, int pageSize)
    {
        (string Key, string? Value)[] filterParameters =
        [
            ("q", SearchUrlParameters.NormalizeText(query.SearchTerm)),
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
            ("group", query.ReleaseGroupId?.ToString(CultureInfo.InvariantCulture)),
            ("sort", query.SortOrder == DefaultSortOrder ? null : query.SortOrder.ToString()),
        ];

        return SearchUrlParameters.Build(BasePath, filterParameters, page, pageSize);
    }
}

using System.Globalization;
using Bearcat.Domain.UseCases.ManageArchives.Search;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Shared;

namespace Bearcat.Website.Pages.ManageArchives;

public static class ArchiveSearchUrl
{
    private const string BasePath = "/archives";

    public static SearchUrlState<ArchiveSearchQuery> Parse(IArchiveSearchUrlValues values)
    {
        var query = new ArchiveSearchQuery(
            SearchTerm: SearchUrlParameters.NormalizeText(values.SearchTerm),
            ArchiveState: SearchUrlParameters.ParseEnum<ArchiveState>(values.ArchiveState),
            ArchiverName: SearchUrlParameters.NormalizeText(values.ArchiverName),
            ReleaseGroupId: SearchUrlParameters.NullWhenZero(values.ReleaseGroupId),
            OnDiskFilter: SearchUrlParameters.ParseEnum<ArchiveOnDiskFilter>(values.OnDiskFilter)
        );

        return new SearchUrlState<ArchiveSearchQuery>(
            query,
            SearchUrlParameters.ParsePageIndex(values.Page),
            SearchUrlParameters.ParsePageSize(values.PageSize)
        );
    }

    public static string Build(ArchiveSearchQuery query, int page, int pageSize)
    {
        (string Key, string? Value)[] filterParameters =
        [
            ("q", SearchUrlParameters.NormalizeText(query.SearchTerm)),
            ("state", query.ArchiveState?.ToString()),
            ("archiver", SearchUrlParameters.NormalizeText(query.ArchiverName)),
            ("group", query.ReleaseGroupId?.ToString(CultureInfo.InvariantCulture)),
            ("disk", query.OnDiskFilter?.ToString()),
        ];

        return SearchUrlParameters.Build(BasePath, filterParameters, page, pageSize);
    }
}

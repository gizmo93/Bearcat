using Bearcat.Domain.UseCases.ManageArchives.Search;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.ManageArchives;

public class ArchiveSearchFormModel
{
    public string? SearchTerm { get; set; }
    public ArchiveState? ArchiveState { get; set; }
    public string ArchiverName { get; set; } = string.Empty;
    public int ReleaseGroupId { get; set; }
    public ArchiveOnDiskFilter? OnDiskFilter { get; set; }

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchTerm)
        || ArchiveState is not null
        || !string.IsNullOrWhiteSpace(ArchiverName)
        || ReleaseGroupId != 0
        || OnDiskFilter is not null;

    public static ArchiveSearchFormModel FromQuery(ArchiveSearchQuery query)
    {
        return new ArchiveSearchFormModel
        {
            SearchTerm = query.SearchTerm,
            ArchiveState = query.ArchiveState,
            ArchiverName = query.ArchiverName ?? string.Empty,
            ReleaseGroupId = query.ReleaseGroupId ?? 0,
            OnDiskFilter = query.OnDiskFilter,
        };
    }

    public ArchiveSearchQuery ToQuery()
    {
        return new ArchiveSearchQuery(
            SearchTerm: SearchTerm,
            ArchiveState: ArchiveState,
            ArchiverName: string.IsNullOrWhiteSpace(ArchiverName) ? null : ArchiverName,
            ReleaseGroupId: ReleaseGroupId == 0 ? null : ReleaseGroupId,
            OnDiskFilter: OnDiskFilter
        );
    }
}

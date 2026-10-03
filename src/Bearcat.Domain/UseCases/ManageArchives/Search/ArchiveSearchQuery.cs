using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageArchives.Search;

public record ArchiveSearchQuery(
    string? SearchTerm = null,
    ArchiveState? ArchiveState = null,
    string? ArchiverName = null,
    int? ReleaseGroupId = null,
    ArchiveOnDiskFilter? OnDiskFilter = null,
    int PageIndex = 0,
    int PageSize = 10
);

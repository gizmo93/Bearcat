namespace Bearcat.Website.Pages.ManageArchives;

public interface IArchiveSearchUrlValues
{
    string? SearchTerm { get; }
    string? ArchiveState { get; }
    string? ArchiverName { get; }
    int? ReleaseGroupId { get; }
    string? OnDiskFilter { get; }
    int? Page { get; }
    int? PageSize { get; }
}

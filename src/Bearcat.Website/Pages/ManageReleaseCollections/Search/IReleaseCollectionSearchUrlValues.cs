namespace Bearcat.Website.Pages.ManageReleaseCollections.Search;

public interface IReleaseCollectionSearchUrlValues
{
    string? SearchTerm { get; }
    string? ReleaseContentType { get; }
    string? Language { get; }
    string? OnlineState { get; }
    int? ReleaseGroupId { get; }
    string? SortOrder { get; }
    int? Page { get; }
    int? PageSize { get; }
}

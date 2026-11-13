using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections.Dto;

public record ReleaseCollectionSearchQuery(
    string? SearchTerm = null,
    ReleaseContentType? ReleaseContentType = null,
    int? ReleaseGroupId = null,
    OnlineState? OnlineState = null,
    string? PrimaryLanguageCode = null,
    ReleaseCollectionSearchSortOrder SortOrder = ReleaseCollectionSearchSortOrder.NameAscending,
    int PageIndex = 0,
    int PageSize = 10
);

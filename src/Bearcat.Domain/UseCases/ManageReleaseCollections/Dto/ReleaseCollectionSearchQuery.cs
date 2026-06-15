using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections.Dto;

public record ReleaseCollectionSearchQuery(
    string? SearchTerm = null,
    ReleaseContentType? ReleaseContentType = null,
    int? ReleaseGroupId = null,
    int PageIndex = 0,
    int PageSize = 10
);

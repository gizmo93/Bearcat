using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.Dto;

public record ReleaseSearchQuery(
    string? SearchTerm = null,
    OnlineState? OnlineState = null,
    int? HosterRegistrationId = null,
    string? ArchiverName = null,
    int? LinkCrypterRegistrationId = null,
    int? ReleaseGroupId = null,
    string? LinksDistributedTo = null,
    int PageIndex = 0,
    int PageSize = 10
);

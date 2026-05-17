using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.Dto;

public record ReleaseUploadLinkSearchQuery(
    int ReleaseId,
    int UploadId,
    OnlineState? OnlineState = null,
    int PageIndex = 0,
    int PageSize = 10
);

using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Dto;

public sealed record LinkCrypterImportReleaseSearchQuery(
    int LinkCrypterImportId,
    IReadOnlyList<LinkCrypterImportContainerStatus> Statuses,
    int PageIndex,
    int PageSize
);

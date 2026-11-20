using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Dto;

public sealed record LinkCrypterImportReleaseFilter(
    int LinkCrypterImportId,
    IReadOnlyList<LinkCrypterImportContainerStatus> Statuses,
    string? SearchText
);

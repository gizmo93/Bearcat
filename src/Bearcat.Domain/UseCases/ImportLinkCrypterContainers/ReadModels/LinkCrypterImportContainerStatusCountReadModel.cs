using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.ReadModels;

public sealed record LinkCrypterImportContainerStatusCountReadModel(
    LinkCrypterImportContainerStatus Status,
    int Count
);

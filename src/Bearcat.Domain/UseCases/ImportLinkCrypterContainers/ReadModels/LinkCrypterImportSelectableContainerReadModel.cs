namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.ReadModels;

public sealed record LinkCrypterImportSelectableContainerReadModel(
    int Id,
    string ReleaseName,
    bool IsSelected
);

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.ReadModels;

public sealed record LinkCrypterImportHosterRegistrationOptionReadModel(
    int Id,
    string Name,
    bool IsActive
);

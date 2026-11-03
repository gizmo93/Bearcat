namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.ReadModels;

public sealed record LinkCrypterImportHosterRegistrationReadModel(
    string HosterClassName,
    int? HosterRegistrationId,
    IReadOnlyList<LinkCrypterImportHosterRegistrationOptionReadModel> Options
);

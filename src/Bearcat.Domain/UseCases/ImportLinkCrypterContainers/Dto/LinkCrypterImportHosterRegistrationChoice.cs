namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Dto;

public sealed record LinkCrypterImportHosterRegistrationChoice(
    string HosterClassName,
    int HosterRegistrationId
);

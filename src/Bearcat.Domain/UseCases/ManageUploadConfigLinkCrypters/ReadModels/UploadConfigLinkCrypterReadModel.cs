namespace Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters.ReadModels;

public record UploadConfigLinkCrypterReadModel(
    int UploadConfigLinkCrypterId,
    string LinkCrypterName,
    string LinkCrypterRegistrationName,
    int LinkCrypterRegistrationId,
    string? Password,
    bool LinkCrypterIsActive
);

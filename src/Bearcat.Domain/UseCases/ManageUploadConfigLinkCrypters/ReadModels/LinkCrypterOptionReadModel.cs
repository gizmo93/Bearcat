namespace Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters.ReadModels;

public record LinkCrypterOptionReadModel(
    int LinkCrypterRegistrationId,
    string Name,
    bool SupportsCaptcha,
    bool SupportsContainerDownload,
    bool SupportsClickAndLoad
);

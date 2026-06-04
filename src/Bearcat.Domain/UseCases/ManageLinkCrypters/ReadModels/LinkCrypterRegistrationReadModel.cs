namespace Bearcat.Domain.UseCases.ManageLinkCrypters.ReadModels;

public record LinkCrypterRegistrationReadModel(
    int LinkCrypterRegistrationId,
    string Name,
    string LinkCrypterClassName,
    string CrypterName,
    bool IsActive,
    bool SupportsCaptcha,
    bool SupportsContainerDownload,
    bool SupportsClickAndLoad
);

using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters.ReadModels;

public record UploadConfigLinkCrypterReadModel(
    int UploadConfigLinkCrypterId,
    string LinkCrypterName,
    string LinkCrypterRegistrationName,
    int LinkCrypterRegistrationId,
    string? Password,
    LinkCrypterContainerScope ContainerScope,
    bool LinkCrypterIsActive,
    bool EnableCaptcha,
    bool EnableContainerDownload,
    bool EnableClickAndLoad,
    bool SupportsCaptcha,
    bool SupportsContainerDownload,
    bool SupportsClickAndLoad,
    int? ReleaseCollectionId
);

namespace Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;

public record CollectionUploadSlotLinkCrypterReadModel(
    int LinkCrypterRegistrationId,
    string LinkCrypterRegistrationName,
    bool IsActive,
    string? Password,
    bool EnableCaptcha,
    bool EnableContainerDownload,
    bool EnableClickAndLoad,
    int UploadConfigCount
);

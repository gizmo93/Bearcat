namespace Bearcat.Domain.UseCases.ManageReleaseCollections.Dto;

public record CollectionUploadSlotLinkCrypterSettings(
    int LinkCrypterRegistrationId,
    string? Password,
    bool EnableCaptcha,
    bool EnableContainerDownload,
    bool EnableClickAndLoad
);

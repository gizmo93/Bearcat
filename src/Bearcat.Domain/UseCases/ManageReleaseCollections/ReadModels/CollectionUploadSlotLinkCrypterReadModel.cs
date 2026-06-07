namespace Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;

public record CollectionUploadSlotLinkCrypterReadModel(
    int LinkCrypterRegistrationId,
    string LinkCrypterRegistrationName,
    bool IsActive,
    int UploadConfigCount
);

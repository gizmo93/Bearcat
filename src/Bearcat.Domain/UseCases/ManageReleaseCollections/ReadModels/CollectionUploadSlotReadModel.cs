using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;

public record CollectionUploadSlotReadModel(
    int CollectionUploadSlotId,
    string Key,
    string Name,
    bool IsRequired,
    CollectionUploadSlotPasswordPolicy PasswordPolicy,
    string? ExpectedArchivePassword,
    int UploadConfigCount,
    int UploadCount,
    IReadOnlyList<CollectionUploadSlotLinkCrypterReadModel> SharedLinkCrypters,
    IReadOnlyList<CollectionUploadSlotContainerReadModel> Containers
);

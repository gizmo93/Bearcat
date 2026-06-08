namespace Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;

public record ReleaseCollectionDetailReadModel(
    int ReleaseCollectionId,
    string Name,
    string Key,
    int ReleaseGroupId,
    string ReleaseGroupName,
    DateTime CreatedAt,
    IReadOnlyList<CollectionUploadSlotReadModel> UploadSlots,
    IReadOnlyList<ReleaseCollectionReleaseReadModel> Releases
);

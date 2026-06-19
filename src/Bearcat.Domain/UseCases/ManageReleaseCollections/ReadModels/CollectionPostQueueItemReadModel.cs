namespace Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;

public record CollectionPostQueueItemReadModel(
    int ReleaseCollectionId,
    string Name,
    DateTime LatestUploadedAt,
    IReadOnlyList<CollectionPostQueueSlotGroupReadModel> SlotGroups
);

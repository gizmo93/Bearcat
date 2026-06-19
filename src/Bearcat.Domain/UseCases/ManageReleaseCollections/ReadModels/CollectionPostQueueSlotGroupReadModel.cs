using Bearcat.Domain.Shared.PostQueue;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;

public record CollectionPostQueueSlotGroupReadModel(
    string SlotName,
    IReadOnlyList<PostQueueHosterReadModel> Hosters,
    IReadOnlyList<PostQueueContainerReadModel> Containers
);

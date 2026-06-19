using Bearcat.Domain.Shared.PostQueue;

namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record ReleasePostQueueItemReadModel(
    int ReleaseId,
    string ReleaseName,
    DateTime LatestUploadedAt,
    IReadOnlyList<ReleasePostQueueArchiveGroupReadModel> ArchiveGroups,
    IReadOnlyList<PostQueueContainerReadModel> Containers
);

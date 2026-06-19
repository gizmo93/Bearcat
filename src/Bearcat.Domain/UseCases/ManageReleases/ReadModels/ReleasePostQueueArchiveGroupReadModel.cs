using Bearcat.Domain.Shared.PostQueue;

namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record ReleasePostQueueArchiveGroupReadModel(
    string ArchiveConfigName,
    IReadOnlyList<PostQueueHosterReadModel> Hosters
);

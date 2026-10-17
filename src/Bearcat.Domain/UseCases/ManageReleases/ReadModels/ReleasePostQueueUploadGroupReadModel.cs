using Bearcat.Domain.Shared.PostQueue;

namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record ReleasePostQueueUploadGroupReadModel(
    string GroupName,
    bool IsVideoUploadGroup,
    IReadOnlyList<PostQueueHosterReadModel> Hosters
);

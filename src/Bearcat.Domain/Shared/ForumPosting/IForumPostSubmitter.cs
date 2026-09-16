using Bearcat.Abstractions.DistributionSite.Dto;

namespace Bearcat.Domain.Shared.ForumPosting;

public interface IForumPostSubmitter
{
    Task<IReadOnlyList<ExistingThread>> FindExistingThreadsAsync(
        int registrationId,
        string targetNodeId,
        string releaseName,
        CancellationToken cancellationToken = default
    );

    Task<SubmittedPost> SubmitNewThreadAsync(
        int registrationId,
        string targetNodeId,
        string title,
        IReadOnlyList<string> prefixIds,
        string body,
        CancellationToken cancellationToken = default
    );

    Task<SubmittedPost> SubmitReplyAsync(
        int registrationId,
        string threadUrl,
        string body,
        CancellationToken cancellationToken = default
    );

    Task<SubmittedPost> EditPostAsync(
        int registrationId,
        string postedUrl,
        string body,
        CancellationToken cancellationToken = default
    );
}

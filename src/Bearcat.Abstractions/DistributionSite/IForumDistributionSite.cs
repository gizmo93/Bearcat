using Bearcat.Abstractions.DistributionSite.Dto;

namespace Bearcat.Abstractions.DistributionSite;

public interface IForumDistributionSite : IDistributionSite
{
    Task<IReadOnlyList<ForumTargetNode>> GetTargetHierarchyAsync(
        DistributionSession session,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<ExistingThread>> FindExistingThreadsAsync(
        DistributionSession session,
        ForumTargetId target,
        string releaseName,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<ThreadPrefix>> GetThreadPrefixesAsync(
        DistributionSession session,
        ForumTargetId target,
        CancellationToken cancellationToken
    );

    Task<PreparedDraft> PrepareNewThreadDraftAsync(
        DistributionSession session,
        ForumTargetId target,
        string title,
        IReadOnlyList<string> prefixIds,
        string body,
        CancellationToken cancellationToken
    );

    Task<PreparedDraft> PrepareReplyDraftAsync(
        DistributionSession session,
        string threadUrl,
        string body,
        CancellationToken cancellationToken
    );

    Task<string?> ResolvePostedUrlAsync(
        DistributionSession session,
        ForumTargetId target,
        bool isNewThread,
        string threadUrl,
        string title,
        CancellationToken cancellationToken
    );
}

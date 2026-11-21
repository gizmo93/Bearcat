using Bearcat.Abstractions.DistributionSite.Dto;

namespace Bearcat.Abstractions.DistributionSite;

public interface IBlogDistributionSite : IDistributionSite
{
    Task<PreparedDraft> PrepareNewPostDraftAsync(
        DistributionSession session,
        ForumTargetId target,
        string title,
        string body,
        CancellationToken cancellationToken
    );

    Task<SubmittedPost> SubmitNewPostAsync(
        DistributionSession session,
        ForumTargetId target,
        string title,
        string body,
        CancellationToken cancellationToken
    );
}

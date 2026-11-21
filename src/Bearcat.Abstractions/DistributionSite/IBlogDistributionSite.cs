using Bearcat.Abstractions.DistributionSite.Dto;

namespace Bearcat.Abstractions.DistributionSite;

public interface IBlogDistributionSite : IDistributionSite
{
    Task<PreparedDraft> PrepareNewPostDraftAsync(
        IDistributionSiteConfig config,
        DistributionSession session,
        ForumTargetId target,
        string title,
        string body,
        string? coverImageUrl,
        CancellationToken cancellationToken
    );

    Task<SubmittedPost> SubmitNewPostAsync(
        IDistributionSiteConfig config,
        DistributionSession session,
        ForumTargetId target,
        string title,
        string body,
        string? coverImageUrl,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<ExistingBlogPost>> FindExistingPostsAsync(
        DistributionSession session,
        string title,
        CancellationToken cancellationToken
    );

    Task<SubmittedPost> PublishExistingPostAsync(
        DistributionSession session,
        string postedUrl,
        string body,
        CancellationToken cancellationToken
    );
}

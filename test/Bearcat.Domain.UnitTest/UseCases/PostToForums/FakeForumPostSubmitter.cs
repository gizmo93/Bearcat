using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Domain.Shared.ForumPosting;

namespace Bearcat.Domain.UnitTest.UseCases.PostToForums;

public sealed class FakeForumPostSubmitter : IForumPostSubmitter
{
    public List<ExistingThread> ExistingThreads { get; init; } = [];

    public string NewThreadUrl { get; init; } = "https://forum.test/threads/new";

    public string ReplyUrl { get; init; } = "https://forum.test/threads/existing#post-2";

    public Exception? ThrowOnSubmit { get; init; }

    public HashSet<int> FailingRegistrationIds { get; init; } = [];

    public List<SubmittedNewThread> NewThreads { get; } = [];

    public List<SubmittedReply> Replies { get; } = [];

    public List<EditedPost> EditedPosts { get; } = [];

    public string EditedPostUrl { get; init; } = "https://forum.test/threads/existing/post-2";

    public List<string> SearchedReleaseNames { get; } = [];

    public Task<IReadOnlyList<ExistingThread>> FindExistingThreadsAsync(
        int registrationId,
        string targetNodeId,
        string releaseName,
        CancellationToken cancellationToken = default
    )
    {
        SearchedReleaseNames.Add(releaseName);

        return Task.FromResult<IReadOnlyList<ExistingThread>>(ExistingThreads);
    }

    public Task<SubmittedPost> SubmitNewThreadAsync(
        int registrationId,
        string targetNodeId,
        string title,
        IReadOnlyList<string> prefixIds,
        string body,
        CancellationToken cancellationToken = default
    )
    {
        FailWhenRequested(registrationId);

        NewThreads.Add(
            new SubmittedNewThread(registrationId, targetNodeId, title, prefixIds, body)
        );

        return Task.FromResult(new SubmittedPost(NewThreadUrl));
    }

    public Task<SubmittedPost> SubmitReplyAsync(
        int registrationId,
        string threadUrl,
        string body,
        CancellationToken cancellationToken = default
    )
    {
        FailWhenRequested(registrationId);

        Replies.Add(new SubmittedReply(registrationId, threadUrl, body));

        return Task.FromResult(new SubmittedPost(ReplyUrl));
    }

    public Task<SubmittedPost> EditPostAsync(
        int registrationId,
        string postedUrl,
        string body,
        CancellationToken cancellationToken = default
    )
    {
        FailWhenRequested(registrationId);

        EditedPosts.Add(new EditedPost(registrationId, postedUrl, body));

        return Task.FromResult(new SubmittedPost(EditedPostUrl));
    }

    private void FailWhenRequested(int registrationId)
    {
        if (ThrowOnSubmit is not null)
        {
            throw ThrowOnSubmit;
        }

        if (FailingRegistrationIds.Contains(registrationId))
        {
            throw new InvalidOperationException($"Submit to {registrationId} failed");
        }
    }
}

using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Domain.Shared.AutoForumPosting;

namespace Bearcat.Domain.UnitTest.Shared.AutoForumPosting;

public sealed class FakeForumPostSubmitter : IForumPostSubmitter
{
    public List<ExistingThread> ExistingThreads { get; init; } = [];

    public string NewThreadUrl { get; init; } = "https://forum.test/threads/new";

    public string ReplyUrl { get; init; } = "https://forum.test/threads/existing#post-2";

    public Exception? ThrowOnSubmit { get; init; }

    public List<SubmittedNewThread> NewThreads { get; } = [];

    public List<SubmittedReply> Replies { get; } = [];

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
        if (ThrowOnSubmit is not null)
        {
            throw ThrowOnSubmit;
        }

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
        if (ThrowOnSubmit is not null)
        {
            throw ThrowOnSubmit;
        }

        Replies.Add(new SubmittedReply(registrationId, threadUrl, body));

        return Task.FromResult(new SubmittedPost(ReplyUrl));
    }
}

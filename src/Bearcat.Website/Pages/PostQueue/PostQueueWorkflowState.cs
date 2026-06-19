namespace Bearcat.Website.Pages.PostQueue;

public sealed class PostQueueWorkflowRun
{
    private readonly List<int> remaining;

    public PostQueueWorkflowRun(IReadOnlyList<int> ids)
    {
        remaining = ids.ToList();
        Total = remaining.Count;
    }

    public int Total { get; }

    public int CompletedCount { get; private set; }

    public int RemainingCount => remaining.Count;

    public int Position => Math.Min(CompletedCount + 1, Total);

    private int? CurrentId => remaining.Count > 0 ? remaining[0] : null;

    public int? Complete(int id)
    {
        if (remaining.Remove(id))
        {
            CompletedCount++;
        }

        return CurrentId;
    }

    public int? Skip(int id)
    {
        if (remaining.Count > 1 && remaining.Remove(id))
        {
            remaining.Add(id);
        }

        return CurrentId;
    }
}

public sealed class PostQueueWorkflowState
{
    private PostQueueWorkflowRun? releaseRun;

    private PostQueueWorkflowRun? collectionRun;

    public PostQueueWorkflowRun? GetRun(PostQueueWorkflowType type) =>
        type switch
        {
            PostQueueWorkflowType.Release => releaseRun,
            PostQueueWorkflowType.Collection => collectionRun,
            _ => null,
        };

    public void Start(PostQueueWorkflowType type, IReadOnlyList<int> ids)
    {
        var run = new PostQueueWorkflowRun(ids);

        switch (type)
        {
            case PostQueueWorkflowType.Release:
                releaseRun = run;
                break;
            case PostQueueWorkflowType.Collection:
                collectionRun = run;
                break;
        }
    }

    public void Clear(PostQueueWorkflowType type)
    {
        switch (type)
        {
            case PostQueueWorkflowType.Release:
                releaseRun = null;
                break;
            case PostQueueWorkflowType.Collection:
                collectionRun = null;
                break;
        }
    }
}

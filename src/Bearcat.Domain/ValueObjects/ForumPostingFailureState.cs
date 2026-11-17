namespace Bearcat.Domain.ValueObjects;

public enum ForumPostingFailureState
{
    RetryScheduled = 1,
    GaveUp = 2,
    WaitingForThreadSelection = 3,
}

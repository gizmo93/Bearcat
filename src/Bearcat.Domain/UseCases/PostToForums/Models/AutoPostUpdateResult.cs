namespace Bearcat.Domain.UseCases.PostToForums.Models;

public sealed record AutoPostUpdateResult(
    AutoPostUpdateStatus Status,
    string? PostedUrl,
    bool ReleaseMarkedPosted,
    AutoPostUpdateSkipReason? SkipReason,
    AutoPostBlockedReason? BlockedReason,
    IReadOnlyList<string> Errors
)
{
    public static AutoPostUpdateResult Updated(string url, bool releaseMarkedPosted)
    {
        return new AutoPostUpdateResult(
            Status: AutoPostUpdateStatus.Updated,
            PostedUrl: url,
            ReleaseMarkedPosted: releaseMarkedPosted,
            SkipReason: null,
            BlockedReason: null,
            Errors: []
        );
    }

    public static AutoPostUpdateResult Skipped(
        AutoPostUpdateSkipReason reason,
        AutoPostBlockedReason? blockedReason = null
    )
    {
        return new AutoPostUpdateResult(
            Status: AutoPostUpdateStatus.Skipped,
            PostedUrl: null,
            ReleaseMarkedPosted: false,
            SkipReason: reason,
            BlockedReason: blockedReason,
            Errors: []
        );
    }

    public static AutoPostUpdateResult Failed(IReadOnlyList<string> errors)
    {
        return new AutoPostUpdateResult(
            Status: AutoPostUpdateStatus.Failed,
            PostedUrl: null,
            ReleaseMarkedPosted: false,
            SkipReason: null,
            BlockedReason: null,
            Errors: errors
        );
    }
}

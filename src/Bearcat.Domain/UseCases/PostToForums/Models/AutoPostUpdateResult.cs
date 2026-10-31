namespace Bearcat.Domain.UseCases.PostToForums.Models;

public sealed record AutoPostUpdateResult(
    AutoPostUpdateStatus Status,
    string? PostedUrl,
    bool UploadsMarkedPosted,
    AutoPostUpdateSkipReason? SkipReason,
    AutoPostBlockedReason? BlockedReason,
    IReadOnlyList<string> Errors
)
{
    public static AutoPostUpdateResult Updated(string url, bool uploadsMarkedPosted)
    {
        return new AutoPostUpdateResult(
            Status: AutoPostUpdateStatus.Updated,
            PostedUrl: url,
            UploadsMarkedPosted: uploadsMarkedPosted,
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
            UploadsMarkedPosted: false,
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
            UploadsMarkedPosted: false,
            SkipReason: null,
            BlockedReason: null,
            Errors: errors
        );
    }
}

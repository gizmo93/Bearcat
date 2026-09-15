namespace Bearcat.Domain.UseCases.PostToForums;

public sealed record AutoPostExecutionResult(
    AutoPostExecutionStatus Status,
    string? PostedUrl,
    bool ReleaseMarkedPosted,
    AutoPostSkipReason? SkipReason,
    AutoPostBlockedReason? BlockedReason,
    IReadOnlyList<string> Errors
)
{
    public static AutoPostExecutionResult Posted(string url, bool releaseMarkedPosted)
    {
        return new AutoPostExecutionResult(
            Status: AutoPostExecutionStatus.Posted,
            PostedUrl: url,
            ReleaseMarkedPosted: releaseMarkedPosted,
            SkipReason: null,
            BlockedReason: null,
            Errors: []
        );
    }

    public static AutoPostExecutionResult Skipped(
        AutoPostSkipReason reason,
        AutoPostBlockedReason? blockedReason = null
    )
    {
        return new AutoPostExecutionResult(
            Status: AutoPostExecutionStatus.Skipped,
            PostedUrl: null,
            ReleaseMarkedPosted: false,
            SkipReason: reason,
            BlockedReason: blockedReason,
            Errors: []
        );
    }

    public static AutoPostExecutionResult Failed(IReadOnlyList<string> errors)
    {
        return new AutoPostExecutionResult(
            Status: AutoPostExecutionStatus.Failed,
            PostedUrl: null,
            ReleaseMarkedPosted: false,
            SkipReason: null,
            BlockedReason: null,
            Errors: errors
        );
    }
}

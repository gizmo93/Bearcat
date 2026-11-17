using Bearcat.Abstractions.DistributionSite.Dto;

namespace Bearcat.Domain.UseCases.PostToForums.Models;

public sealed record AutoPostExecutionResult(
    AutoPostExecutionStatus Status,
    string? PostedUrl,
    bool UploadsMarkedPosted,
    AutoPostSkipReason? SkipReason,
    AutoPostBlockedReason? BlockedReason,
    IReadOnlyList<string> Errors,
    IReadOnlyList<ExistingThread> ThreadCandidates
)
{
    public static AutoPostExecutionResult Posted(string url, bool uploadsMarkedPosted)
    {
        return new AutoPostExecutionResult(
            Status: AutoPostExecutionStatus.Posted,
            PostedUrl: url,
            UploadsMarkedPosted: uploadsMarkedPosted,
            SkipReason: null,
            BlockedReason: null,
            Errors: [],
            ThreadCandidates: []
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
            UploadsMarkedPosted: false,
            SkipReason: reason,
            BlockedReason: blockedReason,
            Errors: [],
            ThreadCandidates: []
        );
    }

    public static AutoPostExecutionResult SkippedForServiceRestrictions(
        IReadOnlyList<string> blockedReasons
    )
    {
        return new AutoPostExecutionResult(
            Status: AutoPostExecutionStatus.Skipped,
            PostedUrl: null,
            UploadsMarkedPosted: false,
            SkipReason: AutoPostSkipReason.ServiceRestrictionNotMet,
            BlockedReason: null,
            Errors: blockedReasons,
            ThreadCandidates: []
        );
    }

    public static AutoPostExecutionResult Failed(IReadOnlyList<string> errors)
    {
        return new AutoPostExecutionResult(
            Status: AutoPostExecutionStatus.Failed,
            PostedUrl: null,
            UploadsMarkedPosted: false,
            SkipReason: null,
            BlockedReason: null,
            Errors: errors,
            ThreadCandidates: []
        );
    }

    public static AutoPostExecutionResult ThreadSelectionNeeded(
        IReadOnlyList<ExistingThread> candidates
    )
    {
        return new AutoPostExecutionResult(
            Status: AutoPostExecutionStatus.ThreadSelectionNeeded,
            PostedUrl: null,
            UploadsMarkedPosted: false,
            SkipReason: null,
            BlockedReason: null,
            Errors: [],
            ThreadCandidates: candidates
        );
    }
}

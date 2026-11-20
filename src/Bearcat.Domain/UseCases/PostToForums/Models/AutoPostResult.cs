using Bearcat.Abstractions.DistributionSite.Dto;

namespace Bearcat.Domain.UseCases.PostToForums.Models;

public sealed record AutoPostResult
{
    private AutoPostResult(AutoPostResultStatus status)
    {
        Status = status;
    }

    public AutoPostResultStatus Status { get; }

    public string? PostedUrl { get; private init; }

    public bool UploadsMarkedPosted { get; private init; }

    public AutoPostSkipReason? SkipReason { get; private init; }

    public AutoPostBlockedReason? BlockedReason { get; private init; }

    public IReadOnlyList<string> Errors { get; private init; } = [];

    public IReadOnlyList<ExistingThread> ThreadCandidates { get; private init; } = [];

    public static AutoPostResult Posted(string url, bool uploadsMarkedPosted)
    {
        return new AutoPostResult(AutoPostResultStatus.Posted)
        {
            PostedUrl = url,
            UploadsMarkedPosted = uploadsMarkedPosted,
        };
    }

    public static AutoPostResult Updated(string url, bool uploadsMarkedPosted)
    {
        return new AutoPostResult(AutoPostResultStatus.Updated)
        {
            PostedUrl = url,
            UploadsMarkedPosted = uploadsMarkedPosted,
        };
    }

    public static AutoPostResult Skipped(
        AutoPostSkipReason reason,
        AutoPostBlockedReason? blockedReason = null
    )
    {
        return new AutoPostResult(AutoPostResultStatus.Skipped)
        {
            SkipReason = reason,
            BlockedReason = blockedReason,
        };
    }

    public static AutoPostResult SkippedForServiceRestrictions(IReadOnlyList<string> blockedReasons)
    {
        return new AutoPostResult(AutoPostResultStatus.Skipped)
        {
            SkipReason = AutoPostSkipReason.ServiceRestrictionNotMet,
            Errors = blockedReasons,
        };
    }

    public static AutoPostResult Failed(IReadOnlyList<string> errors)
    {
        return new AutoPostResult(AutoPostResultStatus.Failed) { Errors = errors };
    }

    public static AutoPostResult ThreadSelectionNeeded(IReadOnlyList<ExistingThread> candidates)
    {
        return new AutoPostResult(AutoPostResultStatus.ThreadSelectionNeeded)
        {
            ThreadCandidates = candidates,
        };
    }
}

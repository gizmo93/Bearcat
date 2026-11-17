using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.PostToForums.Models;

public sealed record AutoPostSiteEntry(
    int DistributionSiteRegistrationId,
    string DistributionSiteRegistrationName,
    bool AutomaticPostingEnabled,
    bool StripDotsForThreadSearch,
    int MinimumSecondsBetweenPosts,
    AutoPostSiteStatus Status,
    string? PostedUrl,
    AutoPostRuleMatch? Match,
    int? PostedLocationId,
    int? StoredForumPostTemplateId,
    bool NeedsContentUpdate,
    ForumPostingFailureState? FailureState,
    int FailedAttemptCount,
    DateTime? NextAttemptAt,
    string? LastErrorMessage
)
{
    public int? ResolvedForumPostTemplateId =>
        StoredForumPostTemplateId ?? Match?.ForumPostTemplateId;
}

namespace Bearcat.Domain.Shared.AutoForumPosting;

public sealed record ReleaseAutoPostPlan(
    int ReleaseId,
    string ReleaseName,
    AutoPostBlockedReason? BlockedReason,
    IReadOnlyList<AutoPostSiteEntry> Sites
)
{
    public bool IsBlocked => BlockedReason is not null;

    public IReadOnlyList<AutoPostSiteEntry> PendingSites =>
        Sites.Where(site => site.Status == AutoPostSiteStatus.Matched).ToList();
}

namespace Bearcat.Domain.UseCases.PostToForums.Models;

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

    public IReadOnlyList<AutoPostSiteEntry> UpdatableSites =>
        Sites
            .Where(site =>
                site
                    is {
                        Status: AutoPostSiteStatus.AlreadyPosted,
                        NeedsContentUpdate: true,
                        PostedLocationId: not null,
                    }
            )
            .ToList();

    public IReadOnlyList<AutoPostSiteEntry> AutomaticallyUpdatableSites =>
        UpdatableSites
            .Where(site =>
                site.AutomaticPostingEnabled && site.ResolvedForumPostTemplateId is not null
            )
            .ToList();
}

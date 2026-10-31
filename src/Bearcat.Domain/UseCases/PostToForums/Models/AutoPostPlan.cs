using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.PostToForums.Models;

public sealed record AutoPostPlan(
    ForumPostTemplateType EntityType,
    int EntityId,
    string EntityName,
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

    public AutoPostSiteEntry? FindSite(int distributionSiteRegistrationId)
    {
        return Sites.FirstOrDefault(site =>
            site.DistributionSiteRegistrationId == distributionSiteRegistrationId
        );
    }
}

namespace Bearcat.Domain.UseCases.PostToForums.Models;

public sealed record AutoPostSiteEntry(
    int DistributionSiteRegistrationId,
    string DistributionSiteRegistrationName,
    bool AutomaticPostingEnabled,
    bool StripDotsForThreadSearch,
    AutoPostSiteStatus Status,
    string? PostedUrl,
    AutoPostRuleMatch? Match,
    int? PostedLocationId,
    int? StoredForumPostTemplateId,
    bool NeedsContentUpdate
)
{
    public int? ResolvedForumPostTemplateId =>
        StoredForumPostTemplateId ?? Match?.ForumPostTemplateId;
}

namespace Bearcat.Domain.Shared.AutoForumPosting;

public sealed record AutoPostSiteEntry(
    int DistributionSiteRegistrationId,
    string DistributionSiteRegistrationName,
    AutoPostSiteStatus Status,
    string? PostedUrl,
    AutoPostRuleMatch? Match
);

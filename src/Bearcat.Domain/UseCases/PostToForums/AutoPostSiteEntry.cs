namespace Bearcat.Domain.UseCases.PostToForums;

public sealed record AutoPostSiteEntry(
    int DistributionSiteRegistrationId,
    string DistributionSiteRegistrationName,
    bool AutomaticPostingEnabled,
    AutoPostSiteStatus Status,
    string? PostedUrl,
    AutoPostRuleMatch? Match
);

using Bearcat.Abstractions.DistributionSite;

namespace Bearcat.Domain.UseCases.ManageDistributionSites.ReadModels;

public record DistributionSiteRegistrationReadModel(
    int DistributionSiteRegistrationId,
    string Name,
    string DistributionSiteClassName,
    string DistributionSiteName,
    DistributionSiteKind Kind,
    bool IsActive,
    bool HasUnreadableSecrets,
    bool EnableAutomaticPosting,
    bool StripDotsForThreadSearch,
    int PostingRuleCount
);

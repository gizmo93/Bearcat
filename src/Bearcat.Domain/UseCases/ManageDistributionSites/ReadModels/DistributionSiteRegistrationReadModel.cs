using Bearcat.Abstractions.DistributionSite;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageDistributionSites.ReadModels;

public record DistributionSiteRegistrationReadModel(
    int DistributionSiteRegistrationId,
    string Name,
    string DistributionSiteClassName,
    string DistributionSiteName,
    DistributionSiteKind Kind,
    ForumPostTemplateOutputFormat TemplateOutputFormat,
    bool IsActive,
    bool HasUnreadableSecrets,
    bool EnableAutomaticPosting,
    bool StripDotsForThreadSearch,
    int MinimumSecondsBetweenPosts,
    int PostingRuleCount
);

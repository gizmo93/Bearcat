using Bearcat.Abstractions.DistributionSite;
using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.PostToForums.Models;

public sealed record AutoPostRegistration(
    int DistributionSiteRegistrationId,
    string Name,
    DistributionSiteKind Kind,
    bool EnableAutomaticPosting,
    bool StripDotsForThreadSearch,
    int MinimumSecondsBetweenPosts,
    IReadOnlyList<ForumPostingRule> EnabledRules
);

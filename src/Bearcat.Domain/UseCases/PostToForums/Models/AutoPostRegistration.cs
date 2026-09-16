using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.PostToForums.Models;

public sealed record AutoPostRegistration(
    int DistributionSiteRegistrationId,
    string Name,
    bool EnableAutomaticPosting,
    bool StripDotsForThreadSearch,
    IReadOnlyList<ForumPostingRule> EnabledRules
);

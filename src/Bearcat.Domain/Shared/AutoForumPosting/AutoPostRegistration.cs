using Bearcat.Domain.Entities;

namespace Bearcat.Domain.Shared.AutoForumPosting;

public sealed record AutoPostRegistration(
    int DistributionSiteRegistrationId,
    string Name,
    IReadOnlyList<ForumPostingRule> EnabledRules
);

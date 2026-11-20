using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.PostToForums.Models;

public sealed record AutoPostSiteEntry(
    AutoPostRegistration Registration,
    ForumPostingRule? MatchedRule,
    AutoPostPostedLocation? PostedLocation,
    ForumPostingFailure? Failure,
    bool NeedsContentUpdate
)
{
    public int DistributionSiteRegistrationId => Registration.DistributionSiteRegistrationId;

    public AutoPostSiteStatus Status
    {
        get
        {
            if (PostedLocation is not null)
            {
                return AutoPostSiteStatus.AlreadyPosted;
            }

            return MatchedRule is not null
                ? AutoPostSiteStatus.Matched
                : AutoPostSiteStatus.NoMatch;
        }
    }

    public int? ForumPostTemplateIdForUpdate =>
        PostedLocation?.ForumPostTemplateId ?? MatchedRule?.ForumPostTemplateId;
}

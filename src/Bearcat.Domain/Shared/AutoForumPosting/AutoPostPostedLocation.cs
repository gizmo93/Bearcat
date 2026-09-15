namespace Bearcat.Domain.Shared.AutoForumPosting;

public sealed record AutoPostPostedLocation(
    int ReleaseId,
    int? DistributionSiteRegistrationId,
    string Url
);

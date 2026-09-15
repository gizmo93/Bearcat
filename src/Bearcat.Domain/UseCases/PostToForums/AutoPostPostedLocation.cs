namespace Bearcat.Domain.UseCases.PostToForums;

public sealed record AutoPostPostedLocation(
    int ReleaseId,
    int? DistributionSiteRegistrationId,
    string Url
);

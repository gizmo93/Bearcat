namespace Bearcat.Domain.UseCases.PostToForums;

public sealed record AutoPostRunFailure(
    int ReleaseId,
    string ReleaseName,
    int DistributionSiteRegistrationId,
    string DistributionSiteRegistrationName,
    IReadOnlyList<string> Messages
);

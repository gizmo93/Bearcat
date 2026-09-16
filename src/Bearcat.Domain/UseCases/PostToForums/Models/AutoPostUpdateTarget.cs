namespace Bearcat.Domain.UseCases.PostToForums.Models;

public sealed record AutoPostUpdateTarget(
    int PostedLocationId,
    int EntityId,
    string EntityName,
    int? ReleaseId,
    int DistributionSiteRegistrationId,
    string DistributionSiteRegistrationName,
    string PostedUrl,
    int? ForumPostTemplateId
);

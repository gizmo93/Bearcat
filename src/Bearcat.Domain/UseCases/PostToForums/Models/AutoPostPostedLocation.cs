namespace Bearcat.Domain.UseCases.PostToForums.Models;

public sealed record AutoPostPostedLocation(
    int PostedLocationId,
    int EntityId,
    int? DistributionSiteRegistrationId,
    int? ForumPostTemplateId,
    string Url,
    DateTime CreatedAt,
    DateTime? ContentUpdatedAt
);

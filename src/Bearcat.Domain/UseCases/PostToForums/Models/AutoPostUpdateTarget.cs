using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.PostToForums.Models;

public sealed record AutoPostUpdateTarget(
    int PostedLocationId,
    ForumPostTemplateType EntityType,
    int EntityId,
    string EntityName,
    int DistributionSiteRegistrationId,
    string DistributionSiteRegistrationName,
    string PostedUrl,
    int? ForumPostTemplateId
);

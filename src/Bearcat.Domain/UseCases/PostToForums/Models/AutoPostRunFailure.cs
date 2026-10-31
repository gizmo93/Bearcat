using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.PostToForums.Models;

public sealed record AutoPostRunFailure(
    ForumPostTemplateType EntityType,
    int EntityId,
    string EntityName,
    int DistributionSiteRegistrationId,
    string DistributionSiteRegistrationName,
    IReadOnlyList<string> Messages
);

namespace Bearcat.Domain.UseCases.ManagePostedLocations.ReadModels;

public record PostedLocationReadModel(
    int PostedLocationId,
    int? DistributionSiteRegistrationId,
    string? DistributionSiteRegistrationName,
    int? ForumPostTemplateId,
    string Url,
    DateTime CreatedAt,
    DateTime? ContentUpdatedAt
);

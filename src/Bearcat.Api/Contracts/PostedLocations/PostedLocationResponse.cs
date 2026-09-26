using Bearcat.Domain.UseCases.ManagePostedLocations.ReadModels;

namespace Bearcat.Api.Contracts.PostedLocations;

public record PostedLocationResponse(
    int Id,
    string Url,
    int? DistributionSiteRegistrationId,
    string? DistributionSiteRegistrationName,
    int? ForumPostTemplateId,
    DateTime CreatedAt,
    DateTime? ContentUpdatedAt
)
{
    public static PostedLocationResponse FromReadModel(PostedLocationReadModel readModel)
    {
        return new PostedLocationResponse(
            Id: readModel.PostedLocationId,
            Url: readModel.Url,
            DistributionSiteRegistrationId: readModel.DistributionSiteRegistrationId,
            DistributionSiteRegistrationName: readModel.DistributionSiteRegistrationName,
            ForumPostTemplateId: readModel.ForumPostTemplateId,
            CreatedAt: readModel.CreatedAt,
            ContentUpdatedAt: readModel.ContentUpdatedAt
        );
    }
}

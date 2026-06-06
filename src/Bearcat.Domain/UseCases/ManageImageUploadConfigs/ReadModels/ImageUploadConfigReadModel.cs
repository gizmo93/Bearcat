namespace Bearcat.Domain.UseCases.ManageImageUploadConfigs.ReadModels;

public record ImageUploadConfigReadModel(
    int ImageUploadConfigId,
    string Name,
    int ImageHosterRegistrationId,
    string ImageHosterRegistrationName,
    string ReleaseName,
    int ImageUploadCount
);

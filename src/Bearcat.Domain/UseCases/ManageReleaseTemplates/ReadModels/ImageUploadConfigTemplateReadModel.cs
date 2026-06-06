namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.ReadModels;

public record ImageUploadConfigTemplateReadModel(
    int ImageUploadConfigTemplateId,
    string? Name,
    string DisplayName,
    int ImageHosterRegistrationId,
    string ImageHosterRegistrationName
);

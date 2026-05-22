namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.ReadModels;

public record UploadConfigLinkCrypterTemplateReadModel(
    int UploadConfigLinkCrypterTemplateId,
    int LinkCrypterRegistrationId,
    string LinkCrypterRegistrationName,
    string LinkCrypterName,
    string? Password
);

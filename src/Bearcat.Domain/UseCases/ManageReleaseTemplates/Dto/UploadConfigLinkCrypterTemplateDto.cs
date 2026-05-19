namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.Dto;

public record UploadConfigLinkCrypterTemplateDto(
    int UploadConfigLinkCrypterTemplateId,
    int LinkCrypterRegistrationId,
    string LinkCrypterRegistrationName,
    string LinkCrypterName,
    string? Password
);

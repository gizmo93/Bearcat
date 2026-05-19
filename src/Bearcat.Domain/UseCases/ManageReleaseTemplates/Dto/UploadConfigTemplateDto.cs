namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.Dto;

public record UploadConfigTemplateDto(
    int UploadConfigTemplateId,
    string? Name,
    string DisplayName,
    int HosterRegistrationId,
    string HosterRegistrationName,
    int ArchiveConfigTemplateId,
    string ArchiveConfigTemplateName,
    IReadOnlyList<string> LinksDistributedTo,
    IReadOnlyList<UploadConfigLinkCrypterTemplateDto> LinkCrypterTemplates
);

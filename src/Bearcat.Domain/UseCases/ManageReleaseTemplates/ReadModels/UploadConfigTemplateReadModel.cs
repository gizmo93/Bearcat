namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.ReadModels;

public record UploadConfigTemplateReadModel(
    int UploadConfigTemplateId,
    string? Name,
    string DisplayName,
    int HosterRegistrationId,
    string HosterRegistrationName,
    int ArchiveConfigTemplateId,
    string ArchiveConfigTemplateName,
    IReadOnlyList<string> LinksDistributedTo,
    IReadOnlyList<UploadConfigLinkCrypterTemplateReadModel> LinkCrypterTemplates
);

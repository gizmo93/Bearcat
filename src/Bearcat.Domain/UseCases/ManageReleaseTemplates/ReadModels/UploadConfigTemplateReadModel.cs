using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.ReadModels;

public record UploadConfigTemplateReadModel(
    int UploadConfigTemplateId,
    string? Name,
    string DisplayName,
    int HosterRegistrationId,
    string HosterRegistrationName,
    int ArchiveConfigTemplateId,
    string ArchiveConfigTemplateName,
    bool PremiumOnlyDownload,
    string? CollectionUploadSlotKey,
    string? CollectionUploadSlotName,
    bool CollectionUploadSlotIsRequired,
    CollectionUploadSlotPasswordPolicy CollectionUploadSlotPasswordPolicy,
    string? CollectionUploadSlotExpectedArchivePassword,
    IReadOnlyList<string> LinksDistributedTo,
    IReadOnlyList<UploadConfigLinkCrypterTemplateReadModel> LinkCrypterTemplates
);

using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.UploadConfigTemplates;

public sealed record UploadConfigTemplateSettings(
    string? Name,
    UploadTarget UploadTarget,
    int? ArchiveConfigTemplateId,
    bool PremiumOnlyDownload,
    string? CollectionUploadSlotKey = null,
    string? CollectionUploadSlotName = null,
    bool CollectionUploadSlotIsRequired = false,
    CollectionUploadSlotPasswordPolicy CollectionUploadSlotPasswordPolicy =
        CollectionUploadSlotPasswordPolicy.Ignore,
    string? CollectionUploadSlotExpectedArchivePassword = null
);

using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.Dto;

public record ReleaseTemplateSummaryDto(
    int ReleaseTemplateId,
    string Name,
    ReleaseType ReleaseType,
    int ReleaseGroupId,
    string ReleaseGroupName,
    int ArchiveConfigTemplateCount,
    int UploadConfigTemplateCount,
    int LinkCrypterTemplateCount
);

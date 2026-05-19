using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.Dto;

public record ReleaseTemplateDetailDto(
    int ReleaseTemplateId,
    string Name,
    ReleaseType ReleaseType,
    int ReleaseGroupId,
    string ReleaseGroupName,
    IReadOnlyList<ArchiveConfigTemplateDto> ArchiveConfigTemplates,
    IReadOnlyList<UploadConfigTemplateDto> UploadConfigTemplates
);

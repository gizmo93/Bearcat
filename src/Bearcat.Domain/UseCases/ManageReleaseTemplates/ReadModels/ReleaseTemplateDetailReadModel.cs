using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.ReadModels;

public record ReleaseTemplateDetailReadModel(
    int ReleaseTemplateId,
    string Name,
    ReleaseType ReleaseType,
    int ReleaseGroupId,
    string ReleaseGroupName,
    IReadOnlyList<ArchiveConfigTemplateReadModel> ArchiveConfigTemplates,
    IReadOnlyList<UploadConfigTemplateReadModel> UploadConfigTemplates
);

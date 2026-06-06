using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.ReadModels;

public record ReleaseTemplateSummaryReadModel(
    int ReleaseTemplateId,
    string Name,
    ReleaseType ReleaseType,
    int ReleaseGroupId,
    string ReleaseGroupName,
    int ArchiveConfigTemplateCount,
    int UploadConfigTemplateCount,
    int ImageUploadConfigTemplateCount,
    int LinkCrypterTemplateCount
);

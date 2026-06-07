using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.ReadModels;

public record ReleaseTemplateSummaryReadModel(
    int ReleaseTemplateId,
    string Name,
    ReleaseType ReleaseType,
    int ReleaseGroupId,
    string ReleaseGroupName,
    bool UseReleaseCollections,
    ReleaseCollectionDetectionMode ReleaseCollectionDetectionMode,
    string? ReleaseCollectionPattern,
    string? ReleaseCollectionKeyTemplate,
    string? ReleaseCollectionNameTemplate,
    int ArchiveConfigTemplateCount,
    int UploadConfigTemplateCount,
    int ImageUploadConfigTemplateCount,
    int LinkCrypterTemplateCount
);

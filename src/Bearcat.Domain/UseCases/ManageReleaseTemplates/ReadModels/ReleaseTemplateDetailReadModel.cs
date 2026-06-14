using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.ReadModels;

public record ReleaseTemplateDetailReadModel(
    int ReleaseTemplateId,
    string Name,
    ReleaseType ReleaseType,
    int ReleaseGroupId,
    string ReleaseGroupName,
    ReleaseCollectionDetectionMode ReleaseCollectionDetectionMode,
    string? ReleaseCollectionPattern,
    string? ReleaseCollectionKeyTemplate,
    string? ReleaseCollectionNameTemplate,
    IReadOnlyList<ArchiveConfigTemplateReadModel> ArchiveConfigTemplates,
    IReadOnlyList<UploadConfigTemplateReadModel> UploadConfigTemplates,
    IReadOnlyList<ImageUploadConfigTemplateReadModel> ImageUploadConfigTemplates,
    IReadOnlyList<ImageUploadConfigTemplateReadModel> CollectionImageUploadConfigTemplates
);

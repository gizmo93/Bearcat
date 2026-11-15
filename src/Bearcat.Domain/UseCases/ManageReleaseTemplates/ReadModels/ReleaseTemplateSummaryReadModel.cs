using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.ReadModels;

public record ReleaseTemplateSummaryReadModel(
    int ReleaseTemplateId,
    string Name,
    ReleaseType ReleaseType,
    ReleaseContentType ReleaseContentType,
    int ReleaseGroupId,
    string ReleaseGroupName,
    ReleaseCollectionDetectionMode ReleaseCollectionDetectionMode,
    string? ReleaseCollectionPattern,
    string? ReleaseCollectionKeyTemplate,
    string? ReleaseCollectionNameTemplate,
    bool CreateScreenshots,
    int ScreenshotCount,
    bool CreateScreenshotGrid,
    int ScreenshotGridColumns,
    int ScreenshotGridRows,
    int ArchiveConfigTemplateCount,
    int UploadConfigTemplateCount,
    int ImageUploadConfigTemplateCount,
    int LinkCrypterTemplateCount
);

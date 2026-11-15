using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseScreenshots.ReadModels;

public record ReleaseScreenshotOverviewReadModel(
    ReleaseType ReleaseType,
    bool HasReleaseFolder,
    bool CreateScreenshots,
    bool CreateScreenshotGrid,
    bool HasMediaFiles,
    DateTime? MediaMetadataExtractedAt,
    DateTime? ScreenshotsGeneratedAt,
    string? ScreenshotGenerationErrorMessage,
    IReadOnlyList<ReleaseScreenshotReadModel> Screenshots
);

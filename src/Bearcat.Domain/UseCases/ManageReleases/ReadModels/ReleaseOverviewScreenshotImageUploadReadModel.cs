using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record ReleaseOverviewScreenshotImageUploadReadModel(
    ReleaseScreenshotKind Kind,
    int Index,
    TimeSpan? Timestamp,
    UploadState? UploadState,
    IReadOnlyList<string> ErrorMessages,
    string? PreviewUrl,
    string? FullUrl
);

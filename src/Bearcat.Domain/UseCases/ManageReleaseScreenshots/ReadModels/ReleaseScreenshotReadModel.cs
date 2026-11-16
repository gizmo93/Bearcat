using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseScreenshots.ReadModels;

public record ReleaseScreenshotReadModel(
    int ReleaseScreenshotId,
    ReleaseScreenshotKind Kind,
    int Index,
    TimeSpan? Timestamp,
    int Width,
    int Height,
    long SizeBytes,
    bool FileExists,
    string? HostedPreviewUrl,
    string? HostedFullUrl
);

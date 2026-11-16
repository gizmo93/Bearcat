using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.Domain.UseCases.ManageReleaseScreenshots.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseScreenshots.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ReleaseScreenshotReadRepository(IBearcatReadDbContext dbRead)
    : IReleaseScreenshotReadRepository
{
    public async Task<ReleaseScreenshotOverviewReadModel> GetOverviewAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var release = await dbRead
            .Releases.Where(release => release.Id == releaseId)
            .Select(release => new
            {
                release.ReleaseType,
                release.ReleaseFolderPath,
                release.CreateScreenshots,
                release.CreateScreenshotGrid,
                HasMediaFiles = release.MediaFiles.Any(),
                release.MediaMetadataExtractedAt,
                release.ScreenshotsGeneratedAt,
                release.ScreenshotGenerationErrorMessage,
                Screenshots = release
                    .Screenshots.OrderBy(screenshot => screenshot.Kind)
                    .ThenBy(screenshot => screenshot.Index)
                    .Select(screenshot => new
                    {
                        screenshot.Id,
                        screenshot.Kind,
                        screenshot.Index,
                        screenshot.Timestamp,
                        screenshot.Width,
                        screenshot.Height,
                        screenshot.SizeBytes,
                        FileExists = screenshot.FileDeletedAt == null,
                    })
                    .ToList(),
            })
            .FirstAsync(cancellationToken);

        var screenshotGenerationIsPending = await dbRead
            .Releases.Where(release => release.Id == releaseId)
            .AnyAsync(
                ReleaseScreenshotExpressions.ScreenshotGenerationIsPending,
                cancellationToken
            );

        var hostedImageUrls = await dbRead
            .ImageUploadUrls.Where(url =>
                url.ImageUpload.ReleaseScreenshot!.ReleaseId == releaseId
                && url.ImageUpload.UploadState == UploadState.Completed
            )
            .Select(url => new
            {
                ReleaseScreenshotId = url.ImageUpload.ReleaseScreenshotId!.Value,
                url.ImageUploadId,
                url.ImageSize,
                url.Url,
            })
            .ToListAsync(cancellationToken);

        var hostedImageUrlsByScreenshotId = hostedImageUrls
            .GroupBy(url => url.ReleaseScreenshotId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var firstImageUploadId = group.Min(url => url.ImageUploadId);

                    return group
                        .Where(url => url.ImageUploadId == firstImageUploadId)
                        .ToDictionary(url => url.ImageSize, url => url.Url);
                }
            );

        return new ReleaseScreenshotOverviewReadModel(
            ReleaseType: release.ReleaseType,
            HasReleaseFolder: release.ReleaseFolderPath is not null,
            CreateScreenshots: release.CreateScreenshots,
            CreateScreenshotGrid: release.CreateScreenshotGrid,
            HasMediaFiles: release.HasMediaFiles,
            MediaMetadataExtractedAt: release.MediaMetadataExtractedAt,
            ScreenshotsGeneratedAt: release.ScreenshotsGeneratedAt,
            ScreenshotGenerationErrorMessage: release.ScreenshotGenerationErrorMessage,
            ScreenshotGenerationIsPending: screenshotGenerationIsPending,
            Screenshots: release
                .Screenshots.Select(screenshot =>
                {
                    var urlsBySize = hostedImageUrlsByScreenshotId.GetValueOrDefault(
                        screenshot.Id,
                        new Dictionary<ImageSize, string>()
                    );

                    return new ReleaseScreenshotReadModel(
                        ReleaseScreenshotId: screenshot.Id,
                        Kind: screenshot.Kind,
                        Index: screenshot.Index,
                        Timestamp: screenshot.Timestamp,
                        Width: screenshot.Width,
                        Height: screenshot.Height,
                        SizeBytes: screenshot.SizeBytes,
                        FileExists: screenshot.FileExists,
                        HostedPreviewUrl: HostedScreenshotUrls.SelectPreviewUrl(
                            screenshot.Kind,
                            urlsBySize
                        ),
                        HostedFullUrl: HostedScreenshotUrls.SelectFullUrl(urlsBySize)
                    );
                })
                .ToList()
        );
    }

    public async Task<string?> GetRelativeFilePathAsync(
        int releaseScreenshotId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ReleaseScreenshots.Where(screenshot => screenshot.Id == releaseScreenshotId)
            .Select(screenshot => screenshot.RelativeFilePath)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

using Bearcat.Abstractions;
using Bearcat.Abstractions.Media;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseScreenshots.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseScreenshots.Storage;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageReleaseScreenshots.Generation;

public class ReleaseScreenshotGenerationService(
    IReleaseScreenshotGenerationRepository repository,
    IVideoScreenshotCreator videoScreenshotCreator,
    IFileSystemService fileSystemService,
    ReleaseScreenshotDirectory screenshotDirectory,
    TimeProvider timeProvider,
    ILogger<ReleaseScreenshotGenerationService> logger
)
{
    public const int ScreenshotGridWidthPixels = 1920;

    private const string ScreenshotGridFileName = "screenshot-grid.jpg";

    public async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        var releaseIds = await repository.GetReleaseIdsPendingScreenshotGenerationAsync(
            cancellationToken
        );

        foreach (var releaseId in releaseIds)
        {
            var release = await repository.GetReleaseWithMediaFilesAndScreenshotsAsync(
                releaseId,
                cancellationToken
            );

            if (release is null)
            {
                continue;
            }

            await GenerateAsync(release, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ScheduleRegenerationAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var release = await repository.GetReleaseWithMediaFilesAndScreenshotsAsync(
            releaseId,
            cancellationToken
        );

        if (release is null)
        {
            return;
        }

        repository.RemoveScreenshots(release.Screenshots);
        fileSystemService.DeleteDirectoryIfExists(screenshotDirectory.GetDirectoryPath(releaseId));
        release.ScreenshotsGeneratedAt = null;
        release.ScreenshotGenerationErrorMessage = null;

        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task GenerateAsync(Release release, CancellationToken cancellationToken)
    {
        var releaseFolderPath = release.ReleaseFolderPath!;
        var mediaFile = release.MediaFiles.MaxBy(file => file.SizeBytes)!;
        var videoFilePath = Path.Combine(releaseFolderPath, mediaFile.RelativePath);
        var metadata = MediaInfoOutputParser.Parse(mediaFile.MediaInfoJson);

        var transferCharacteristic =
            VideoTransferCharacteristicMapping.FromMediaInfoTransferCharacteristics(
                metadata?.VideoStream?.TransferCharacteristics
            );

        var preconditionErrorMessage = GetPreconditionErrorMessage(
            releaseFolderPath: releaseFolderPath,
            videoFilePath: videoFilePath,
            metadata: metadata,
            transferCharacteristic: transferCharacteristic
        );

        if (preconditionErrorMessage is not null)
        {
            logger.LogWarning(
                "Screenshots for release {ReleaseName} cannot be created: {ErrorMessage}",
                release.Name,
                preconditionErrorMessage
            );

            release.ScreenshotGenerationErrorMessage = preconditionErrorMessage;
            return;
        }

        var directoryPath = screenshotDirectory.GetDirectoryPath(release.Id);
        fileSystemService.DeleteDirectoryIfExists(directoryPath);

        try
        {
            var screenshots = await CreateScreenshotsAsync(
                release: release,
                videoFileName: Path.GetFileName(mediaFile.RelativePath),
                videoFilePath: videoFilePath,
                metadata: metadata!,
                duration: metadata!.Duration!.Value,
                transferCharacteristic: transferCharacteristic,
                directoryPath: directoryPath,
                cancellationToken: cancellationToken
            );

            release.Screenshots.AddRange(screenshots);
            release.ScreenshotsGeneratedAt = timeProvider.GetLocalNow();
            release.ScreenshotGenerationErrorMessage = null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            fileSystemService.DeleteDirectoryIfExists(directoryPath);

            logger.LogError(
                exception,
                "Failed to create screenshots for release {ReleaseName}",
                release.Name
            );

            release.ScreenshotGenerationErrorMessage = exception.Message;
        }
    }

    private string? GetPreconditionErrorMessage(
        string releaseFolderPath,
        string videoFilePath,
        MediaFileMetadata? metadata,
        VideoTransferCharacteristic transferCharacteristic
    )
    {
        if (!fileSystemService.DirectoryExists(releaseFolderPath))
        {
            return $"The release folder {releaseFolderPath} does not exist.";
        }

        if (!fileSystemService.FileExists(videoFilePath))
        {
            return $"The video file {videoFilePath} does not exist.";
        }

        if (metadata?.Duration is null)
        {
            return $"The duration of the video file {videoFilePath} is unknown.";
        }

        var isDolbyVision =
            metadata.VideoStream?.HdrFormat?.Contains("Dolby Vision", StringComparison.Ordinal)
            == true;

        if (
            isDolbyVision
            && transferCharacteristic is VideoTransferCharacteristic.StandardDynamicRange
        )
        {
            return "Dolby Vision without an HDR10 or HLG base layer is not supported.";
        }

        return null;
    }

    private async Task<List<ReleaseScreenshot>> CreateScreenshotsAsync(
        Release release,
        string videoFileName,
        string videoFilePath,
        MediaFileMetadata metadata,
        TimeSpan duration,
        VideoTransferCharacteristic transferCharacteristic,
        string directoryPath,
        CancellationToken cancellationToken
    )
    {
        var createdAt = timeProvider.GetLocalNow();
        var screenshots = new List<ReleaseScreenshot>();

        if (release.CreateScreenshots)
        {
            var screenshotFiles = await videoScreenshotCreator.CreateScreenshotsAsync(
                new ScreenshotCreationRequest(
                    VideoFilePath: videoFilePath,
                    Duration: duration,
                    TransferCharacteristic: transferCharacteristic,
                    Count: release.ScreenshotCount,
                    OutputDirectory: directoryPath
                ),
                cancellationToken
            );

            screenshots.AddRange(
                screenshotFiles.Select(
                    (screenshotFile, index) =>
                        new ReleaseScreenshot
                        {
                            Kind = ReleaseScreenshotKind.Screenshot,
                            Index = index + 1,
                            Timestamp = screenshotFile.Timestamp,
                            RelativeFilePath = ReleaseScreenshotDirectory.GetRelativeFilePath(
                                release.Id,
                                Path.GetFileName(screenshotFile.FilePath)
                            ),
                            Width = screenshotFile.Width,
                            Height = screenshotFile.Height,
                            SizeBytes = screenshotFile.SizeBytes,
                            CreatedAt = createdAt,
                        }
                )
            );
        }

        if (release.CreateScreenshotGrid)
        {
            var screenshotGridFile = await videoScreenshotCreator.CreateScreenshotGridAsync(
                new ScreenshotGridCreationRequest(
                    VideoFilePath: videoFilePath,
                    Duration: duration,
                    TransferCharacteristic: transferCharacteristic,
                    Columns: release.ScreenshotGridColumns,
                    Rows: release.ScreenshotGridRows,
                    GridWidthPixels: ScreenshotGridWidthPixels,
                    HeaderLines: ScreenshotGridHeaderLines.Build(videoFileName, metadata),
                    OutputFilePath: Path.Combine(directoryPath, ScreenshotGridFileName)
                ),
                cancellationToken
            );

            screenshots.Add(
                new ReleaseScreenshot
                {
                    Kind = ReleaseScreenshotKind.ScreenshotGrid,
                    Index = 1,
                    Timestamp = null,
                    RelativeFilePath = ReleaseScreenshotDirectory.GetRelativeFilePath(
                        release.Id,
                        ScreenshotGridFileName
                    ),
                    Width = screenshotGridFile.Width,
                    Height = screenshotGridFile.Height,
                    SizeBytes = screenshotGridFile.SizeBytes,
                    CreatedAt = createdAt,
                }
            );
        }

        return screenshots;
    }
}

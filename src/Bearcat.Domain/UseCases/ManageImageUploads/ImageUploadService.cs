using System.Globalization;
using Bearcat.Abstractions;
using Bearcat.Abstractions.ImageHoster;
using Bearcat.Abstractions.ImageHoster.Dto;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.Proxies;
using Bearcat.Domain.UseCases.ManageImageUploads.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseScreenshots.Storage;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageImageUploads;

public class ImageUploadService(
    IImageUploadRepository repository,
    IImageHosterFactory imageHosterFactory,
    ReleaseScreenshotDirectory releaseScreenshotDirectory,
    IFileSystemService fileSystemService,
    TimeProvider timeProvider,
    ILogger<ImageUploadService> logger
)
{
    public async Task ProcessAsync(CancellationToken cancellationToken = default)
    {
        await repository.CreateMissingImageUploadsAsync(
            createdAt: timeProvider.GetLocalNow(),
            cancellationToken: cancellationToken
        );

        await ProcessPendingImageUploadsAsync(cancellationToken);
        await DeleteUploadedScreenshotFilesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int imageUploadId, CancellationToken cancellationToken = default)
    {
        var imageUpload = await repository.GetByIdAsync(imageUploadId, cancellationToken);

        repository.Remove(imageUpload);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task DeleteUploadedScreenshotFilesAsync(CancellationToken cancellationToken)
    {
        var screenshots = await repository.GetScreenshotsWithFinishedUploadsAsync(
            cancellationToken
        );

        if (screenshots.Count == 0)
        {
            return;
        }

        var fileDeletedAt = timeProvider.GetLocalNow();

        foreach (var screenshot in screenshots)
        {
            fileSystemService.DeleteFileIfExists(
                releaseScreenshotDirectory.GetFilePath(screenshot.RelativeFilePath)
            );

            screenshot.FileDeletedAt = fileDeletedAt;
        }

        await repository.SaveChangesAsync(cancellationToken);

        foreach (var releaseId in screenshots.Select(screenshot => screenshot.ReleaseId).Distinct())
        {
            fileSystemService.DeleteDirectoryIfEmpty(
                releaseScreenshotDirectory.GetDirectoryPath(releaseId)
            );
        }

        logger.LogInformation(
            "Deleted {ScreenshotFileCount} uploaded screenshot files",
            screenshots.Count
        );
    }

    private async Task ProcessPendingImageUploadsAsync(CancellationToken cancellationToken)
    {
        var pendingUploads = await repository.GetPendingImageUploadsAsync(cancellationToken);

        if (pendingUploads.Count == 0)
        {
            return;
        }

        var configsByRegistrationId = await repository.GetConfigByImageHosterRegistrationIdAsync(
            cancellationToken
        );

        var imageHostersByClassName = imageHosterFactory.GetByClassName();

        foreach (var imageUpload in pendingUploads)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await ProcessImageUploadAsync(
                imageUpload: imageUpload,
                configsByRegistrationId: configsByRegistrationId,
                imageHostersByClassName: imageHostersByClassName,
                cancellationToken: cancellationToken
            );

            await repository.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task ProcessImageUploadAsync(
        ImageUpload imageUpload,
        IReadOnlyDictionary<int, string> configsByRegistrationId,
        IReadOnlyDictionary<string, IImageHoster> imageHostersByClassName,
        CancellationToken cancellationToken
    )
    {
        var registration = imageUpload.ImageUploadConfig.ImageHosterRegistration;
        var (image, errorMessage) = CreateImageToUpload(imageUpload);

        if (image is null)
        {
            imageUpload.UploadState = UploadState.Failed;
            imageUpload.ErrorMessages = [errorMessage!];
            return;
        }

        imageUpload.UploadState = UploadState.Uploading;
        await repository.SaveChangesAsync(cancellationToken);

        try
        {
            var imageHoster = imageHostersByClassName[registration.ImageHosterClassName];

            var config = imageHoster.DeserializeConfig(configsByRegistrationId[registration.Id]);

            using var proxyScope = ImageHosterRegistrationProxyScope.Enter(registration);

            var result = await imageHoster.UploadImageAsync(
                image: image,
                imageHosterConfig: config,
                cancellationToken: cancellationToken
            );

            if (!result.IsSuccess)
            {
                imageUpload.UploadState = UploadState.Failed;
                imageUpload.ErrorMessages = result.ErrorMessages.ToList();
                return;
            }

            imageUpload.UploadState = UploadState.Completed;
            imageUpload.UploadedAt = timeProvider.GetLocalNow();
            imageUpload.ImageUrls = result
                .ImageUrls.Select(url => new ImageUploadUrl { ImageSize = url.Size, Url = url.Url })
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Image upload {ImageUploadId} failed", imageUpload.Id);
            imageUpload.UploadState = UploadState.Failed;
            imageUpload.ErrorMessages = [ex.InnerException?.Message ?? ex.Message];
        }
    }

    private (ImageToUploadDto? Image, string? ErrorMessage) CreateImageToUpload(
        ImageUpload imageUpload
    )
    {
        var imageUploadConfig = imageUpload.ImageUploadConfig;

        if (imageUpload.ReleaseScreenshot is { } screenshot)
        {
            if (screenshot.FileDeletedAt is not null)
            {
                return (
                    null,
                    "Screenshot file was deleted after its uploads completed. Regenerate the screenshots to upload it."
                );
            }

            var filePath = releaseScreenshotDirectory.GetFilePath(screenshot.RelativeFilePath);

            if (!fileSystemService.FileExists(filePath))
            {
                return (
                    null,
                    $"Screenshot file '{filePath}' does not exist. Regenerate the screenshots to upload it."
                );
            }

            var screenshotImage = new ImageToUploadDto(
                Source: filePath,
                SourceType: ImageUploadSource.LocalFile,
                Name: GetScreenshotImageName(imageUploadConfig.Release!.Name, screenshot)
            );

            return (screenshotImage, null);
        }

        var (coverUrl, imageName) = imageUploadConfig switch
        {
            { Release: { } release } => (release.Metadata?.CoverUrl, release.Name),
            { ReleaseCollection: { } collection } => (
                collection.Metadata?.CoverUrl,
                collection.Name
            ),
            _ => (null, string.Empty),
        };

        if (string.IsNullOrWhiteSpace(coverUrl))
        {
            return (null, "Image upload source has no cover URL.");
        }

        var coverImage = new ImageToUploadDto(
            Source: coverUrl,
            SourceType: ImageUploadSource.Url,
            Name: imageName
        );

        return (coverImage, null);
    }

    private static string GetScreenshotImageName(string releaseName, ReleaseScreenshot screenshot)
    {
        return screenshot.Kind switch
        {
            ReleaseScreenshotKind.ScreenshotGrid => $"{releaseName} screenshot grid",
            _ =>
                $"{releaseName} screenshot {screenshot.Index.ToString("00", CultureInfo.InvariantCulture)}",
        };
    }
}

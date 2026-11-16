using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageImageUploads.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ImageUploadRepository(IBearcatWriteDbContext dbWrite, ISecretProtector secretProtector)
    : IImageUploadRepository
{
    public async Task CreateMissingImageUploadsAsync(
        DateTime createdAt,
        CancellationToken cancellationToken = default
    )
    {
        var configIdsWithoutCoverUpload = await dbWrite
            .ImageUploadConfigs.Where(config =>
                config.UploadCover
                && config.ImageHosterRegistration.IsActive
                && !config.ImageUploads.Any(upload => upload.ReleaseScreenshotId == null)
                && (
                    (
                        config.Release != null
                        && config.Release.Metadata != null
                        && config.Release.Metadata.CoverUrl != null
                    )
                    || (
                        config.ReleaseCollection != null
                        && config.ReleaseCollection.Metadata != null
                        && config.ReleaseCollection.Metadata.CoverUrl != null
                    )
                )
            )
            .OrderBy(config => config.Id)
            .Select(config => config.Id)
            .ToListAsync(cancellationToken);

        var screenshotsWithoutUpload = await dbWrite
            .ImageUploadConfigs.Where(
                ImageUploadExpressions.ImageUploadConfigUploadsScreenshotsToActiveHoster
            )
            .Join(
                dbWrite.ReleaseScreenshots,
                config => config.ReleaseId,
                screenshot => (int?)screenshot.ReleaseId,
                (config, screenshot) => new { Config = config, Screenshot = screenshot }
            )
            .Where(pair =>
                pair.Screenshot.FileDeletedAt == null
                && (
                    (
                        pair.Screenshot.Kind == ReleaseScreenshotKind.Screenshot
                        && pair.Config.UploadScreenshots
                    )
                    || (
                        pair.Screenshot.Kind == ReleaseScreenshotKind.ScreenshotGrid
                        && pair.Config.UploadScreenshotGrid
                    )
                )
                && !pair.Config.ImageUploads.Any(upload =>
                    upload.ReleaseScreenshotId == pair.Screenshot.Id
                )
            )
            .OrderBy(pair => pair.Config.Id)
            .ThenBy(pair => pair.Screenshot.Id)
            .Select(pair => new
            {
                ImageUploadConfigId = pair.Config.Id,
                ReleaseScreenshotId = pair.Screenshot.Id,
            })
            .ToListAsync(cancellationToken);

        if (configIdsWithoutCoverUpload.Count == 0 && screenshotsWithoutUpload.Count == 0)
        {
            return;
        }

        dbWrite.ImageUploads.AddRange(
            configIdsWithoutCoverUpload.Select(configId => new ImageUpload
            {
                ImageUploadConfigId = configId,
                ReleaseScreenshotId = null,
                CreatedAt = createdAt,
                UploadState = UploadState.Pending,
                ImageUrls = [],
                ErrorMessages = [],
            })
        );

        dbWrite.ImageUploads.AddRange(
            screenshotsWithoutUpload.Select(screenshot => new ImageUpload
            {
                ImageUploadConfigId = screenshot.ImageUploadConfigId,
                ReleaseScreenshotId = screenshot.ReleaseScreenshotId,
                CreatedAt = createdAt,
                UploadState = UploadState.Pending,
                ImageUrls = [],
                ErrorMessages = [],
            })
        );

        await dbWrite.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ImageUpload>> GetPendingImageUploadsAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .ImageUploads.AsSplitQuery()
            .Include(upload => upload.ImageUrls)
            .Include(upload => upload.ImageUploadConfig)
                .ThenInclude(config => config.ImageHosterRegistration)
            .Include(upload => upload.ImageUploadConfig)
                .ThenInclude(config => config.Release)
                    .ThenInclude(release => release!.Metadata)
            .Include(upload => upload.ImageUploadConfig)
                .ThenInclude(config => config.ReleaseCollection)
                    .ThenInclude(collection => collection!.Metadata)
            .Include(upload => upload.ReleaseScreenshot)
            .Where(upload =>
                upload.UploadState == UploadState.Pending
                && upload.ImageUploadConfig.ImageHosterRegistration.IsActive
            )
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReleaseScreenshot>> GetScreenshotsWithFinishedUploadsAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .ReleaseScreenshots.Where(screenshot =>
                screenshot.FileDeletedAt == null
                && screenshot.Release.ScreenshotsGeneratedAt != null
                && screenshot.Release.ImageUploadConfigs.All(config =>
                    !config.ImageHosterRegistration.IsActive
                    || !(
                        (
                            screenshot.Kind == ReleaseScreenshotKind.Screenshot
                            && config.UploadScreenshots
                        )
                        || (
                            screenshot.Kind == ReleaseScreenshotKind.ScreenshotGrid
                            && config.UploadScreenshotGrid
                        )
                    )
                    || config.ImageUploads.Any(upload =>
                        upload.ReleaseScreenshotId == screenshot.Id
                        && upload.UploadState == UploadState.Completed
                    )
                )
            )
            .OrderBy(screenshot => screenshot.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, string>> GetConfigByImageHosterRegistrationIdAsync(
        CancellationToken cancellationToken = default
    )
    {
        var configs = await dbWrite
            .ImageHosterRegistrations.Where(registration => registration.IsActive)
            .ToDictionaryAsync(
                registration => registration.Id,
                registration => registration.SerializedConfig,
                cancellationToken
            );

        return configs.ToDictionary(
            config => config.Key,
            config => secretProtector.Unprotect(config.Value)
        );
    }

    public async Task<ImageUpload> GetByIdAsync(
        int imageUploadId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite.ImageUploads.FirstAsync(
            upload => upload.Id == imageUploadId,
            cancellationToken
        );
    }

    public void Remove(ImageUpload imageUpload)
    {
        dbWrite.Remove(imageUpload);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await dbWrite.SaveChangesAsync(cancellationToken);
    }
}

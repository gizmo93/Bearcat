using System.Linq.Expressions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Infrastructure.Database.Repositories;

internal static class ImageUploadExpressions
{
    public static readonly Expression<
        Func<ImageUploadConfig, bool>
    > ImageUploadConfigIsSettledForPosting = config =>
        !config.ImageHosterRegistration.IsActive
        || (
            (
                !config.UploadCover
                || config.ImageUploads.Any(upload => upload.ReleaseScreenshotId == null)
            )
            && !config.ImageUploads.Any(upload =>
                upload.UploadState == UploadState.Pending
                || upload.UploadState == UploadState.Uploading
            )
            && config.Release!.Screenshots.All(screenshot =>
                screenshot.FileDeletedAt != null
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
                || config.ImageUploads.Any(upload => upload.ReleaseScreenshotId == screenshot.Id)
            )
        );

    public static readonly Expression<
        Func<ImageUploadConfig, bool>
    > ImageUploadConfigUploadsScreenshotsToActiveHoster = config =>
        config.ImageHosterRegistration.IsActive
        && (config.UploadScreenshots || config.UploadScreenshotGrid);
}

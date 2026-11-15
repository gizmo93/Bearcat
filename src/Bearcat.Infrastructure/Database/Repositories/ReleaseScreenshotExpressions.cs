using System.Linq.Expressions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Infrastructure.Database.Repositories;

internal static class ReleaseScreenshotExpressions
{
    public static readonly Expression<Func<Release, bool>> ScreenshotGenerationIsPending =
        release =>
            release.ReleaseType == ReleaseType.Managed
            && release.ReleaseFolderPath != null
            && (
                (
                    release.CreateScreenshots
                    && release.ImageUploadConfigs.Any(config =>
                        config.ImageHosterRegistration.IsActive && config.UploadScreenshots
                    )
                )
                || (
                    release.CreateScreenshotGrid
                    && release.ImageUploadConfigs.Any(config =>
                        config.ImageHosterRegistration.IsActive && config.UploadScreenshotGrid
                    )
                )
            )
            && (release.MediaMetadataExtractedAt == null || release.MediaFiles.Any())
            && release.ScreenshotsGeneratedAt == null
            && release.ScreenshotGenerationErrorMessage == null;

    public static readonly Expression<Func<Release, bool>> ScreenshotGenerationIsNotPending =
        Expression.Lambda<Func<Release, bool>>(
            Expression.Not(ScreenshotGenerationIsPending.Body),
            ScreenshotGenerationIsPending.Parameters
        );
}

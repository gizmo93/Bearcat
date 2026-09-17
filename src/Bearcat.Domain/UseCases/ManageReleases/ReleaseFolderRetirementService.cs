using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.ArchiveRetention;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageReleases;

public class ReleaseFolderRetirementService(
    IReleaseFolderRetirementRepository repository,
    IApplicationConfigurationProvider configuration,
    MirrorCoverageEvaluator mirrorCoverageEvaluator,
    INotificationService notificationService,
    TimeProvider timeProvider,
    ILogger<ReleaseFolderRetirementService> logger
)
{
    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var autoConvertEnabled = configuration.GetValue<ArchiveCleanupConfiguration>(c =>
            c.AutoConvertToUnmanaged
        );

        if (!autoConvertEnabled)
        {
            return;
        }

        var retentionDays = configuration.GetValue<ArchiveCleanupConfiguration>(c =>
            c.ReleaseFolderRetentionDays
        );
        var cutoff = timeProvider.GetLocalNow().AddDays(-retentionDays);

        var candidates = await repository.GetConversionCandidatesAsync(cutoff, cancellationToken);
        var convertedReleaseCount = 0;

        foreach (var release in candidates)
        {
            if (!IsRecoverableWithoutReleaseFolder(release))
            {
                logger.LogDebug(
                    "Release {ReleaseId} stays managed because not every archive config has a local archive or an online mirror",
                    release.Id
                );

                continue;
            }

            ConvertToUnmanaged(release);
            convertedReleaseCount++;
        }

        if (convertedReleaseCount == 0)
        {
            return;
        }

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Converted {ReleaseCount} releases to unmanaged because their release folder retention expired",
            convertedReleaseCount
        );
    }

    private bool IsRecoverableWithoutReleaseFolder(Release release)
    {
        if (release.ArchiveConfigs.Count == 0)
        {
            return false;
        }

        var mirrorUploads = mirrorCoverageEvaluator.GetMirrorUploadsPerArchiveConfig(release);

        return release.ArchiveConfigs.All(config =>
            config.Archives.Any(archive => archive.ArchiveState is ArchiveState.Created)
            || mirrorUploads.ContainsKey(config.Id)
        );
    }

    private void ConvertToUnmanaged(Release release)
    {
        var releaseFolderPath = release.ReleaseFolderPath!;

        logger.LogInformation(
            "Converting release {ReleaseId} to unmanaged, its release folder {ReleaseFolderPath} can be deleted by the user",
            release.Id,
            releaseFolderPath
        );

        notificationService.Create(
            kind: NotificationKind.ReleaseAutoConvertedToUnmanaged,
            message: GetNotificationMessage(release, releaseFolderPath),
            entity: release,
            selector: n => n.Release
        );

        release.ReleaseType = ReleaseType.Unmanaged;
        release.ReleaseFolderPath = null;
    }

    private static string GetNotificationMessage(Release release, string releaseFolderPath)
    {
        var hasArchivesInsideReleaseFolder = release
            .ArchiveConfigs.SelectMany(config => config.Archives)
            .Where(archive => archive.ArchiveState is ArchiveState.Created)
            .Any(archive =>
                FolderPathHelper.IsSameOrSubPath(
                    childPath: archive.ArchiveFolderPath,
                    parentPath: releaseFolderPath
                )
            );

        if (hasArchivesInsideReleaseFolder)
        {
            return $"The release was converted to unmanaged because its release folder retention expired. The archives of this release are stored inside {releaseFolderPath}, so delete only the release data there and keep the archive files. Bearcat never deletes the release folder itself.";
        }

        return $"The release was converted to unmanaged because its release folder retention expired. You can now delete the release folder yourself: {releaseFolderPath}. Bearcat never deletes the release folder itself.";
    }
}

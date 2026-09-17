using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ArchiveRetention;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageArchives;

public class ArchiveCleanupService(
    IArchiveCleanupRepository repository,
    IApplicationConfigurationProvider configuration,
    MirrorCoverageEvaluator mirrorCoverageEvaluator,
    LocalArchiveDeleter localArchiveDeleter,
    TimeProvider timeProvider,
    ILogger<ArchiveCleanupService> logger
)
{
    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var autoDeleteEnabled = configuration.GetValue<ArchiveCleanupConfiguration>(c =>
            c.AutoDeleteArchives
        );

        if (!autoDeleteEnabled)
        {
            return;
        }

        var retentionDays = configuration.GetValue<ArchiveCleanupConfiguration>(c =>
            c.ArchiveRetentionDays
        );
        var cutoff = timeProvider.GetLocalNow().AddDays(-retentionDays);

        var archives = await repository.GetDeletableArchivesAsync(cutoff, cancellationToken);
        var deletedArchiveCount = 0;

        foreach (var archive in archives)
        {
            if (!IsRecoverable(archive))
            {
                logger.LogDebug(
                    "Archive {ArchiveId} stays on disk because it has neither an online mirror nor a release folder to repack from",
                    archive.Id
                );

                continue;
            }

            try
            {
                localArchiveDeleter.DeleteLocalArchive(archive);
                deletedArchiveCount++;

                logger.LogInformation(
                    "Deleted archive {ArchiveId} at {ArchiveFolderPath}",
                    archive.Id,
                    archive.ArchiveFolderPath
                );
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Could not delete archive {ArchiveId} at {ArchiveFolderPath}",
                    archive.Id,
                    archive.ArchiveFolderPath
                );
            }
        }

        if (deletedArchiveCount == 0)
        {
            return;
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    private bool IsRecoverable(Archive archive)
    {
        var release = archive.ArchiveConfig.Release;

        return mirrorCoverageEvaluator.FindMirrorUpload(archive.ArchiveConfig) is not null
            || (
                release.ReleaseType is ReleaseType.Managed
                && !string.IsNullOrWhiteSpace(release.ReleaseFolderPath)
            );
    }
}

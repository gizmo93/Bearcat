using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ArchiveRetention;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Domain.UseCases.ManageArchives.StorageFolderMoves;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageArchives;

public class ArchiveCleanupService(
    IArchiveCleanupRepository repository,
    IApplicationConfigurationProvider configuration,
    MirrorCoverageEvaluator mirrorCoverageEvaluator,
    LocalArchiveDeleter localArchiveDeleter,
    ArchiveStorageFolderMoveService archiveStorageFolderMoveService,
    TimeProvider timeProvider,
    ILogger<ArchiveCleanupService> logger
)
{
    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var retentionAction = configuration.GetValue<
            ArchiveCleanupConfiguration,
            ArchiveRetentionAction
        >(c => c.ArchiveRetentionAction);

        if (retentionAction is ArchiveRetentionAction.Off)
        {
            return;
        }

        var retentionDays = configuration.GetValue<ArchiveCleanupConfiguration>(c =>
            c.ArchiveRetentionDays
        );
        var cutoff = timeProvider.GetLocalNow().AddDays(-retentionDays);

        var archives = await repository.GetLocalArchivesPastRetentionAsync(
            cutoff,
            cancellationToken
        );

        await (
            retentionAction switch
            {
                ArchiveRetentionAction.Delete => DeleteRecoverableArchivesAsync(
                    archives,
                    cancellationToken
                ),
                ArchiveRetentionAction.MoveToStorageFolder =>
                    archiveStorageFolderMoveService.MoveArchivesAsync(archives, cancellationToken),
                _ => throw new InvalidOperationException(
                    $"Unknown archive retention action {retentionAction}"
                ),
            }
        );
    }

    private async Task DeleteRecoverableArchivesAsync(
        IReadOnlyList<Archive> archives,
        CancellationToken cancellationToken
    )
    {
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

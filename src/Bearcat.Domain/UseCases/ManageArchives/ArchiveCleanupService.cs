using Bearcat.Abstractions;
using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageArchives;

public class ArchiveCleanupService(
    IArchiveCleanupRepository repository,
    IApplicationConfigurationProvider configuration,
    IApplicationConfigurationOverrideCache overrideCache,
    IFileSystemService fileSystemService,
    ILogger<ArchiveCleanupService> logger
)
{
    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        if (!overrideCache.IsInitialized)
        {
            logger.LogDebug("Archive cleanup skipped until configuration cache is initialized");
            return;
        }

        var autoCleanupEnabled = configuration.GetValue<ArchiveCleanupConfiguration>(c =>
            c.AutoCleanup
        );

        if (!autoCleanupEnabled)
        {
            return;
        }

        var archives = await repository.GetDeletableArchivesAsync(cancellationToken);

        if (archives.Count == 0)
        {
            return;
        }

        logger.LogInformation("Cleaning up {ArchiveCount} uploaded archives", archives.Count);

        foreach (var archive in archives)
        {
            try
            {
                fileSystemService.DeleteDirectoryIfExists(archive.ArchiveFolderPath);
                archive.ArchiveState = ArchiveState.Deleted;

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

        await repository.SaveChangesAsync(cancellationToken);
    }
}

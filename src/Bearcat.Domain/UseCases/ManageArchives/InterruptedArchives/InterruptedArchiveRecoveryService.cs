using Bearcat.Abstractions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageArchives.ArchiveFileHashes;
using Bearcat.Domain.UseCases.ManageArchives.ReleaseFolderEntriesForPacking;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageArchives.InterruptedArchives;

public class InterruptedArchiveRecoveryService(
    IArchiveCreationRepository repository,
    ILogger<InterruptedArchiveRecoveryService> logger,
    IArchiverFactory archiverFactory,
    IFileSystemService fileSystemService,
    ReleaseFolderEntriesForPackingService releaseFolderEntriesForPackingService,
    ArchiveFileHashService archiveFileHashService
)
{
    public async Task RecoverOrDeleteInterruptedArchivesAsync(CancellationToken cancellationToken)
    {
        var interruptedArchives = await repository.GetInterruptedArchivesAsync(cancellationToken);

        foreach (var archive in interruptedArchives)
        {
            DeleteReleaseFolderEntriesCopiedForPacking(archive);

            if (AllArchiveFilesExistOnDisk(archive))
            {
                await RecoverInterruptedArchiveByRehashingFilesAsync(archive, cancellationToken);
            }
            else
            {
                DeleteInterruptedArchiveAndItsFolder(archive);
            }

            await repository.SaveChangesAsync(cancellationToken: cancellationToken);
        }
    }

    private void DeleteReleaseFolderEntriesCopiedForPacking(Archive archive)
    {
        if (archive.ReleaseFolderEntriesCopiedForPacking.Count == 0)
        {
            return;
        }

        var releaseFolderPath = archive.ArchiveConfig.Release.ReleaseFolderPath;

        if (releaseFolderPath is null)
        {
            logger.LogWarning(
                "Cannot delete entries {EntryNames} copied for packing interrupted archive {ArchiveId} because its release no longer has a release folder",
                archive.ReleaseFolderEntriesCopiedForPacking,
                archive.Id
            );
        }
        else
        {
            logger.LogInformation(
                "Deleting entries {EntryNames} copied into release folder {ReleaseFolderPath} for packing interrupted archive {ArchiveId}",
                archive.ReleaseFolderEntriesCopiedForPacking,
                releaseFolderPath,
                archive.Id
            );

            releaseFolderEntriesForPackingService.DeleteFromReleaseFolder(
                releaseFolderPath,
                archive.ReleaseFolderEntriesCopiedForPacking
            );
        }

        archive.ReleaseFolderEntriesCopiedForPacking = [];
    }

    private static bool AllArchiveFilesExistOnDisk(Archive archive)
    {
        return archive.ArchiveFiles.Count > 0
            && archive.ArchiveFiles.All(f => File.Exists(f.FullFileName));
    }

    private async Task RecoverInterruptedArchiveByRehashingFilesAsync(
        Archive archive,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "Recovering interrupted archive {ArchiveId} by rehashing its {FileCount} existing files instead of repacking",
            archive.Id,
            archive.ArchiveFiles.Count
        );

        var archiver = archiverFactory.GetByName(archive.ArchiveConfig.ArchiverName);

        await archiveFileHashService.HashArchiveFilesAsync(archive, archiver, cancellationToken);
        archive.MarkAsCreatedAndSetWaitingUploadsPending();
    }

    private void DeleteInterruptedArchiveAndItsFolder(Archive archive)
    {
        logger.LogInformation(
            "Deleting interrupted archive {ArchiveId} because its files are incomplete on disk. Assigned uploads will be repacked",
            archive.Id
        );

        fileSystemService.DeleteDirectoryIfExists(archive.ArchiveFolderPath);
        repository.Remove(archive);
    }
}

using Bearcat.Abstractions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.Transfers;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageArchives.ReleaseFolderEntriesForPacking;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageArchives.ReleaseFolderPacking;

public class ReleaseFolderPackingService(
    IArchiveCreationRepository repository,
    ILogger<ReleaseFolderPackingService> logger,
    IFileSystemService fileSystemService,
    ReleaseFolderEntriesForPackingService releaseFolderEntriesForPackingService,
    ITransferProgressTracker progressTracker,
    FolderSizeProgressReporter folderSizeProgressReporter
)
{
    private const string SynologyMetadataFolderName = "@eaDir";

    public async Task<ArchiveResult> PackReleaseFolderIntoArchiveAsync(
        Archive archive,
        IArchiver archiver,
        string releaseFolderPath,
        ArchivePackingSettings packingSettings,
        CancellationToken cancellationToken
    )
    {
        var entriesForPacking =
            releaseFolderEntriesForPackingService.GetReleaseFolderEntriesForPacking(
                releaseFolderPath: releaseFolderPath,
                additionalArchiveContents: archive.ArchiveConfig.AdditionalArchiveContents,
                createNonceFile: archive.ArchiveConfig.CreateNonceFile
            );

        if (entriesForPacking.ErrorMessages.Count > 0)
        {
            return new ArchiveResult(
                IsSuccess: false,
                CreatedFileNames: [],
                ErrorMessages: entriesForPacking.ErrorMessages
            );
        }

        return await CopyAdditionalEntriesIntoReleaseFolderAndPackAsync(
            archive: archive,
            archiver: archiver,
            releaseFolderPath: releaseFolderPath,
            entriesForPacking: entriesForPacking.Entries,
            packingSettings: packingSettings,
            cancellationToken: cancellationToken
        );
    }

    private async Task<ArchiveResult> CopyAdditionalEntriesIntoReleaseFolderAndPackAsync(
        Archive archive,
        IArchiver archiver,
        string releaseFolderPath,
        IReadOnlyList<ReleaseFolderEntryForPacking> entriesForPacking,
        ArchivePackingSettings packingSettings,
        CancellationToken cancellationToken
    )
    {
        archive.ReleaseFolderEntriesCopiedForPacking = entriesForPacking
            .Select(entry => entry.Name)
            .ToList();

        await repository.SaveChangesAsync(cancellationToken: cancellationToken);

        try
        {
            var copyErrorMessage =
                await releaseFolderEntriesForPackingService.CopyIntoReleaseFolderAsync(
                    releaseFolderPath: releaseFolderPath,
                    entries: entriesForPacking,
                    cancellationToken: cancellationToken
                );

            if (copyErrorMessage is not null)
            {
                return new ArchiveResult(
                    IsSuccess: false,
                    CreatedFileNames: [],
                    ErrorMessages: [copyErrorMessage]
                );
            }

            RemoveSynologyMetadataFolders(releaseFolderPath);

            return await PackReleaseFolderWithProgressAsync(
                archive: archive,
                archiver: archiver,
                releaseFolderPath: releaseFolderPath,
                packingSettings: packingSettings,
                cancellationToken: cancellationToken
            );
        }
        finally
        {
            releaseFolderEntriesForPackingService.DeleteFromReleaseFolder(
                releaseFolderPath,
                archive.ReleaseFolderEntriesCopiedForPacking
            );
            archive.ReleaseFolderEntriesCopiedForPacking = [];
            await repository.SaveChangesAsync(cancellationToken: CancellationToken.None);
        }
    }

    private void RemoveSynologyMetadataFolders(string releasePath)
    {
        var deletedFolders = fileSystemService.DeleteDirectoriesByNameRecursively(
            rootPath: releasePath,
            directoryName: SynologyMetadataFolderName
        );

        if (deletedFolders.Count == 0)
        {
            return;
        }

        logger.LogInformation(
            "Removed {FolderCount} Synology metadata folders ({FolderName}) from release folder {ReleaseFolderPath} before archiving",
            deletedFolders.Count,
            SynologyMetadataFolderName,
            releasePath
        );
    }

    private async Task<ArchiveResult> PackReleaseFolderWithProgressAsync(
        Archive archive,
        IArchiver archiver,
        string releaseFolderPath,
        ArchivePackingSettings packingSettings,
        CancellationToken cancellationToken
    )
    {
        var transferIdentifier = new TransferIdentifier(TransferType.ArchiveCreation, archive.Id);

        var packedReleaseFolder = new TransferFile(
            FileId: 1,
            FileName: archive.ArchiveConfig.Release.Name,
            SourceName: archive.ArchiveConfig.Name,
            SizeBytes: fileSystemService.GetFolderFileCountAndSize(releaseFolderPath).TotalBytes,
            IsAlreadyTransferred: false
        );

        progressTracker.StartTracking(transferIdentifier, [packedReleaseFolder]);

        try
        {
            return await folderSizeProgressReporter.RunWhileReportingFolderSizeAsync(
                folderPath: archive.ArchiveFolderPath,
                progress: new TransferProgressReporter(
                    tracker: progressTracker,
                    identifier: transferIdentifier,
                    fileId: packedReleaseFolder.FileId,
                    fileName: packedReleaseFolder.FileName,
                    sourceName: packedReleaseFolder.SourceName
                ),
                operation: () =>
                    archiver.ArchiveAsync(
                        sourceFolderPath: releaseFolderPath,
                        destinationPath: archive.ArchiveFolderPath,
                        archiveNamePrefix: archive.ArchiveConfig.ArchiveNamePrefix
                            ?? Guid.NewGuid().ToString(),
                        targetFileSizeMb: packingSettings.ArchiveFileSizeMb,
                        password: archive.ArchiveConfig.ArchivePassword,
                        options: packingSettings.Options,
                        cancellationToken: cancellationToken
                    ),
                cancellationToken: cancellationToken
            );
        }
        finally
        {
            progressTracker.StopTracking(transferIdentifier);
        }
    }
}

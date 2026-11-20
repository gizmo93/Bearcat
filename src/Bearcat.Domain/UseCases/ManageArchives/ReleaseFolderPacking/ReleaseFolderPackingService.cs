using Bearcat.Abstractions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageArchives.ReleaseFolderEntriesForPacking;
using Bearcat.Domain.UseCases.ManageArchives.ReleaseFolderPackingExcludes;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;

namespace Bearcat.Domain.UseCases.ManageArchives.ReleaseFolderPacking;

public class ReleaseFolderPackingService(
    IArchiveCreationRepository repository,
    IFileSystemService fileSystemService,
    ReleaseFolderEntriesForPackingService releaseFolderEntriesForPackingService,
    ReleaseFolderPackingExcludeService releaseFolderPackingExcludeService,
    ITransferProgressTracker progressTracker
)
{
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

        var excludes = releaseFolderPackingExcludeService.FindExcludedEntries(releaseFolderPath);
        var releaseFolderFileCount = fileSystemService
            .GetFolderFileCountAndSize(releaseFolderPath)
            .FileCount;

        if (excludes.FileCount > 0 && releaseFolderFileCount == excludes.FileCount)
        {
            return new ArchiveResult(
                IsSuccess: false,
                CreatedFileNames: [],
                ErrorMessages:
                [
                    $"All files of the release folder \"{releaseFolderPath}\" are excluded by the packing exclude patterns.",
                ]
            );
        }

        return await CopyAdditionalEntriesIntoReleaseFolderAndPackAsync(
            archive: archive,
            archiver: archiver,
            releaseFolderPath: releaseFolderPath,
            entriesForPacking: entriesForPacking.Entries,
            packingSettings: packingSettings,
            excludes: excludes,
            cancellationToken: cancellationToken
        );
    }

    private async Task<ArchiveResult> CopyAdditionalEntriesIntoReleaseFolderAndPackAsync(
        Archive archive,
        IArchiver archiver,
        string releaseFolderPath,
        IReadOnlyList<ReleaseFolderEntryForPacking> entriesForPacking,
        ArchivePackingSettings packingSettings,
        ExcludedReleaseFolderEntries excludes,
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

            return await PackReleaseFolderWithProgressAsync(
                archive: archive,
                archiver: archiver,
                releaseFolderPath: releaseFolderPath,
                packingSettings: packingSettings,
                excludes: excludes,
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

    private async Task<ArchiveResult> PackReleaseFolderWithProgressAsync(
        Archive archive,
        IArchiver archiver,
        string releaseFolderPath,
        ArchivePackingSettings packingSettings,
        ExcludedReleaseFolderEntries excludes,
        CancellationToken cancellationToken
    )
    {
        var transferIdentifier = new TransferIdentifier(TransferType.ArchiveCreation, archive.Id);

        var packedReleaseFolder = new TransferFile(
            FileId: 1,
            FileName: archive.ArchiveConfig.Release.Name,
            SourceName: archive.ArchiveConfig.Name,
            SizeBytes: fileSystemService.GetFolderFileCountAndSize(releaseFolderPath).TotalBytes
                - excludes.TotalBytes,
            IsAlreadyTransferred: false
        );

        progressTracker.StartTracking(transferIdentifier, [packedReleaseFolder]);

        try
        {
            return await FolderSizeProgressReporter.RunWhileReportingFolderSizeAsync(
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
                        options: packingSettings.Options with
                        {
                            ExcludedRelativePaths = excludes.RelativePaths,
                        },
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

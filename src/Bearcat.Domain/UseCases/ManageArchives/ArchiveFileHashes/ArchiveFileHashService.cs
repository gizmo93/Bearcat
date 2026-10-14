using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Transfers;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Domain.UseCases.ManageArchives.StorageFolderLocalWorkingCopies;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageArchives.ArchiveFileHashes;

public class ArchiveFileHashService(
    IArchiveCreationRepository repository,
    ILogger<ArchiveFileHashService> logger,
    IApplicationConfigurationProvider configurationProvider,
    ITransferProgressTracker progressTracker
)
{
    public async Task HashArchiveFilesAsync(
        Archive archive,
        IArchiver archiver,
        CancellationToken cancellationToken
    )
    {
        if (archiver.CanChangeHashInPlace)
        {
            await AppendNullBytesUntilHashesAreUniqueAsync(
                archive: archive,
                archiveFiles: archive.ArchiveFiles,
                transferType: TransferType.ArchiveHashing,
                cancellationToken: cancellationToken
            );
        }
        else
        {
            await ComputeAndStoreArchiveFileHashesAsync(archive, cancellationToken);
        }
    }

    public async Task AppendNullBytesUntilHashesAreUniqueAsync(
        Archive archive,
        IReadOnlyList<ArchiveFile> archiveFiles,
        TransferType transferType,
        CancellationToken cancellationToken
    )
    {
        var knownArchiveFileHashes = new KnownArchiveFileHashes(
            await repository.GetKnownArchiveFileHashesAsync(
                archiveConfigId: archive.ArchiveConfig.Id,
                cancellationToken: cancellationToken
            )
        );

        await HashArchiveFilesInParallelWithProgressAsync(
            archive: archive,
            archiveFiles: archiveFiles,
            transferType: transferType,
            hashArchiveFileAsync: async (archiveFile, progress, fileCancellationToken) =>
            {
                if (!File.Exists(archiveFile.FullFileName))
                {
                    logger.LogWarning(
                        "Cannot change MD5 hash for missing archive file {ArchiveFileName} in archive {ArchiveId}",
                        archiveFile.FullFileName,
                        archive.Id
                    );

                    return;
                }

                var archiveFileIsInStorageFolder =
                    ArchiveLocalWorkingCopyFiles.IsFileInStorageFolder(archive, archiveFile);

                archiveFile.Md5Hash = null;

                if (archiveFileIsInStorageFolder)
                {
                    archiveFile.Md5HashInStorageFolder = null;
                }

                progress.BeginFile(new FileInfo(archiveFile.FullFileName).Length);

                var nullByteSuffixHash =
                    await Md5FileHash.ComputeHashWithShortestUnknownNullByteSuffixAsync(
                        fullFileName: archiveFile.FullFileName,
                        addHashIfUnknown: knownArchiveFileHashes.AddIfUnknown,
                        progress: progress,
                        cancellationToken: fileCancellationToken
                    );

                await AppendNullBytesAsync(
                    fullFileName: archiveFile.FullFileName,
                    nullByteCount: nullByteSuffixHash.NullByteCount,
                    cancellationToken: fileCancellationToken
                );

                archiveFile.Md5Hash = nullByteSuffixHash.Md5Hash;

                if (archiveFileIsInStorageFolder)
                {
                    archiveFile.Md5HashInStorageFolder = nullByteSuffixHash.Md5Hash;
                }
            },
            cancellationToken: cancellationToken
        );

        logger.LogInformation(
            "Changed MD5 hash for {FileCount} archive files in archive {ArchiveId}",
            archiveFiles.Count,
            archive.Id
        );
    }

    private async Task ComputeAndStoreArchiveFileHashesAsync(
        Archive archive,
        CancellationToken cancellationToken
    )
    {
        await HashArchiveFilesInParallelWithProgressAsync(
            archive: archive,
            archiveFiles: archive.ArchiveFiles,
            transferType: TransferType.ArchiveHashing,
            hashArchiveFileAsync: async (archiveFile, progress, fileCancellationToken) =>
            {
                if (!File.Exists(archiveFile.FullFileName))
                {
                    logger.LogWarning(
                        "Cannot compute MD5 hash for missing archive file {ArchiveFileName} in archive {ArchiveId}",
                        archiveFile.FullFileName,
                        archive.Id
                    );

                    return;
                }

                archiveFile.Md5Hash = await ComputeMd5HashAsync(
                    archiveFile.FullFileName,
                    progress,
                    fileCancellationToken
                );
            },
            cancellationToken: cancellationToken
        );
    }

    private async Task HashArchiveFilesInParallelWithProgressAsync(
        Archive archive,
        IReadOnlyList<ArchiveFile> archiveFiles,
        TransferType transferType,
        Func<ArchiveFile, ITransferProgress, CancellationToken, Task> hashArchiveFileAsync,
        CancellationToken cancellationToken
    )
    {
        var transferIdentifier = new TransferIdentifier(transferType, archive.Id);

        var plannedFilesPerArchiveFile = archiveFiles
            .Select(
                (archiveFile, index) =>
                    (
                        ArchiveFile: archiveFile,
                        PlannedFile: new TransferFile(
                            FileId: index + 1,
                            FileName: Path.GetFileName(archiveFile.FullFileName),
                            SourceName: archive.ArchiveConfig.Name,
                            SizeBytes: FileSizes.GetFileSizeBytesOrZeroWhenMissing(
                                archiveFile.FullFileName
                            ),
                            IsAlreadyTransferred: false
                        )
                    )
            )
            .ToList();

        progressTracker.StartTracking(
            transferIdentifier,
            plannedFilesPerArchiveFile.Select(entry => entry.PlannedFile).ToList()
        );

        try
        {
            await Parallel.ForEachAsync(
                plannedFilesPerArchiveFile,
                CreateHashingParallelOptions(cancellationToken),
                async (entry, fileCancellationToken) =>
                    await hashArchiveFileAsync(
                        entry.ArchiveFile,
                        new TransferProgressReporter(
                            tracker: progressTracker,
                            identifier: transferIdentifier,
                            fileId: entry.PlannedFile.FileId,
                            fileName: entry.PlannedFile.FileName,
                            sourceName: entry.PlannedFile.SourceName
                        ),
                        fileCancellationToken
                    )
            );
        }
        finally
        {
            progressTracker.StopTracking(transferIdentifier);
        }
    }

    private static async Task<string> ComputeMd5HashAsync(
        string fullFileName,
        ITransferProgress progress,
        CancellationToken cancellationToken
    )
    {
        progress.BeginFile(new FileInfo(fullFileName).Length);

        return await Md5FileHash.ComputeAsync(fullFileName, progress, cancellationToken);
    }

    private ParallelOptions CreateHashingParallelOptions(CancellationToken cancellationToken)
    {
        return new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(
                1,
                configurationProvider.GetValue<ArchiveRepackagingConfiguration>(c =>
                    c.MaxParallelHashOperations
                )
            ),
            CancellationToken = cancellationToken,
        };
    }

    private static async Task AppendNullBytesAsync(
        string fullFileName,
        int nullByteCount,
        CancellationToken cancellationToken
    )
    {
        await using var stream = new FileStream(
            path: fullFileName,
            mode: FileMode.Append,
            access: FileAccess.Write,
            share: FileShare.Read
        );
        await stream.WriteAsync(new byte[nullByteCount], cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}

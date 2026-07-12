using System.Security.Cryptography;
using Bearcat.Abstractions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageArchives;

public class ArchiveCreationService(
    IArchiveCreationRepository repository,
    ILogger<ArchiveCreationService> logger,
    IArchiverFactory archiverFactory,
    IFileSystemService fileSystemService,
    TimeProvider timeProvider,
    INotificationService notificationService,
    IApplicationConfigurationProvider configurationProvider
)
{
    private const string UniqueFileName = "__nonce.txt";
    private const string SynologyMetadataFolderName = "@eaDir";

    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        await HandleInterruptedArchivesAsync(cancellationToken);
        await ProcessUploadsWithoutArchiveAsync(cancellationToken);
    }

    private async Task HandleInterruptedArchivesAsync(CancellationToken cancellationToken)
    {
        var interruptedArchives = await repository.GetInterruptedArchivesAsync(cancellationToken);

        foreach (var archive in interruptedArchives)
        {
            if (AllArchiveFilesExistOnDisk(archive))
            {
                await RecoverInterruptedArchiveAsync(archive, cancellationToken);
            }
            else
            {
                DeleteInterruptedArchive(archive);
            }

            await repository.SaveChangesAsync(cancellationToken: cancellationToken);
        }
    }

    private static bool AllArchiveFilesExistOnDisk(Archive archive)
    {
        return archive.ArchiveFiles.Count > 0
            && archive.ArchiveFiles.All(f => File.Exists(f.FullFileName));
    }

    private async Task RecoverInterruptedArchiveAsync(
        Archive archive,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "Recovering interrupted archive {ArchiveId} by rehashing its {FileCount} existing files instead of repacking",
            archive.Id,
            archive.ArchiveFiles.Count
        );

        await HashArchiveFilesAsync(archive, archive.ArchiveConfig, cancellationToken);
        FinalizeArchive(archive);
    }

    private void DeleteInterruptedArchive(Archive archive)
    {
        logger.LogInformation(
            "Deleting interrupted archive {ArchiveId} because its files are incomplete on disk. Assigned uploads will be repacked",
            archive.Id
        );

        fileSystemService.DeleteDirectoryIfExists(archive.ArchiveFolderPath);
        repository.Remove(archive);
    }

    private static void FinalizeArchive(Archive archive)
    {
        archive.ArchiveState = ArchiveState.Created;

        foreach (
            var upload in archive.Uploads.Where(u => u.UploadState == UploadState.WaitingForArchive)
        )
        {
            upload.UploadState = UploadState.Pending;
        }
    }

    private async Task HashArchiveFilesAsync(
        Archive archive,
        ArchiveConfig archiveConfig,
        CancellationToken cancellationToken
    )
    {
        var archiver = archiverFactory.GetByName(archiveConfig.ArchiverName);

        if (archiver.CanChangeHashInPlace)
        {
            await ChangeArchiveFileHashesAsync(
                archive: archive,
                knownHashes: await LoadKnownHashesAsync(archiveConfig.Id, cancellationToken),
                cancellationToken: cancellationToken
            );
        }
        else
        {
            await StoreArchiveFileHashesAsync(archive, cancellationToken);
        }
    }

    private async Task ProcessUploadsWithoutArchiveAsync(CancellationToken cancellationToken)
    {
        var uploadsWithoutArchive = await repository.GetUploadsWithoutArchiveAsync(
            cancellationToken
        );
        var archivesToCreate = new Dictionary<ArchiveConfig, List<Upload>>();
        var uploadsByArchiveConfigId = new Dictionary<int, List<Upload>>();
        var archiveConfigsById = new Dictionary<int, ArchiveConfig>();

        foreach (var upload in uploadsWithoutArchive)
        {
            archiveConfigsById.TryAdd(
                upload.UploadConfig.ArchiveConfigId,
                upload.UploadConfig.ArchiveConfig
            );

            if (!uploadsByArchiveConfigId.TryAdd(upload.UploadConfig.ArchiveConfigId, [upload]))
            {
                uploadsByArchiveConfigId[upload.UploadConfig.ArchiveConfigId].Add(upload);
            }
        }

        foreach (var (archiveConfigId, uploads) in uploadsByArchiveConfigId)
        {
            logger.LogInformation(
                "Processing uploads {UploadIds} for UploadConfig {UploadConfigIds} without archive",
                uploads.Select(u => u.Id),
                uploads.Select(u => u.UploadConfigId)
            );

            var archiveConfig = archiveConfigsById[archiveConfigId];
            var existingArchiveWasHandled = await TryAssignExistingArchiveAsync(
                archiveConfig,
                uploads,
                cancellationToken
            );

            if (existingArchiveWasHandled)
            {
                continue;
            }

            // We only create new archives for managed releases, for unmanaged releases ("bring your own archives")
            // there is always an ArchiveConfig + Archive existing
            if (uploads.First().UploadConfig.Release.ReleaseType is ReleaseType.Managed)
            {
                archivesToCreate.Add(archiveConfig, uploads);
            }
        }

        if (archivesToCreate.Count == 0)
        {
            return;
        }

        logger.LogInformation(
            "Creating {ArchiveCount} new archives for uploads",
            archivesToCreate.Count
        );

        foreach (var (archiveConfig, uploads) in archivesToCreate)
        {
            await CreateArchiveAsync(archiveConfig, uploads, cancellationToken);
        }
    }

    private async Task<bool> TryAssignExistingArchiveAsync(
        ArchiveConfig archiveConfig,
        IReadOnlyList<Upload> uploads,
        CancellationToken cancellationToken
    )
    {
        var assignableArchive = await repository.GetPossibleAssignableArchiveAsync(
            archiveConfigId: archiveConfig.Id,
            cancellationToken: cancellationToken
        );

        if (assignableArchive is null)
        {
            logger.LogInformation(
                "Could not find existing archive for ArchiveConfig {ArchiveConfigId}",
                archiveConfig.Id
            );

            return false;
        }

        var archiveNeedsHashChange = await ArchiveNeedsHashChangeAsync(
            archiveConfig: archiveConfig,
            uploads: uploads,
            cancellationToken: cancellationToken
        );

        if (archiveNeedsHashChange)
        {
            var archiveHasActiveUpload = await repository.HasActiveUploadAsync(
                archiveId: assignableArchive.Id,
                cancellationToken: cancellationToken
            );

            if (archiveHasActiveUpload)
            {
                logger.LogInformation(
                    "Existing archive {ArchiveId} is currently used by an active upload. Skipping {UploadCount} uploads until the next archive creation run",
                    assignableArchive.Id,
                    uploads.Count
                );

                return true;
            }

            var archiver = archiverFactory.GetByName(archiveConfig.ArchiverName);

            if (!archiver.CanChangeHashInPlace)
            {
                logger.LogInformation(
                    "Archiver {ArchiverName} does not support changing hashes in place. Creating a new archive instead of reusing archive {ArchiveId}.",
                    archiver.Name,
                    assignableArchive.Id
                );

                return false;
            }

            await ChangeArchiveFileHashesAsync(
                archive: assignableArchive,
                knownHashes: await LoadKnownHashesAsync(archiveConfig.Id, cancellationToken),
                cancellationToken: cancellationToken
            );
        }

        foreach (var upload in uploads)
        {
            upload.ArchiveId = assignableArchive.Id;
            upload.UploadState = UploadState.Pending;

            // Take the newest still online file for a previous upload of the current upload
            // and copy it over to the new upload, so we only upload files that were offline (for PartiallyOnline uploads)
            CarryOverOnlineFiles(upload, assignableArchive);
        }

        await repository.SaveChangesAsync(cancellationToken: cancellationToken);

        logger.LogInformation(
            "Assigned existing archive {ArchiveId} to {UploadCount} uploads",
            assignableArchive.Id,
            uploads.Count
        );

        return true;
    }

    private void CarryOverOnlineFiles(Upload newUpload, Archive assignableArchive)
    {
        var reusableOnlineFiles = assignableArchive
            .Uploads.Where(u =>
                u.Id != newUpload.Id && u.UploadConfigId == newUpload.UploadConfigId
            )
            .SelectMany(u => u.UploadedFiles)
            .Where(uf =>
                uf.OnlineState == OnlineState.Online
                && !string.IsNullOrWhiteSpace(uf.HosterFileLink)
            )
            .GroupBy(uf => uf.ArchiveFileId)
            .Select(group => group.MaxBy(uf => uf.UploadId)!)
            .ToList();

        if (reusableOnlineFiles.Count == 0)
        {
            return;
        }

        newUpload.UploadedFiles = reusableOnlineFiles
            .Select(source => new UploadedFile
            {
                Upload = newUpload,
                ArchiveFileId = source.ArchiveFileId,
                HosterFileLink = source.HosterFileLink,
                ExternalId = source.ExternalId,
                HosterFolderId = source.HosterFolderId,
                OnlineState = OnlineState.Online,
                CreatedAt = source.CreatedAt,
                CheckedAt = source.CheckedAt,
            })
            .ToList();

        logger.LogInformation(
            "Carried over {FileCount} online files to reupload {UploadId} from previous uploads of archive {ArchiveId}",
            newUpload.UploadedFiles.Count,
            newUpload.Id,
            assignableArchive.Id
        );
    }

    private async Task<bool> ArchiveNeedsHashChangeAsync(
        ArchiveConfig archiveConfig,
        IReadOnlyList<Upload> uploads,
        CancellationToken cancellationToken
    )
    {
        foreach (
            var hosterClassName in uploads
                .Select(u => u.UploadConfig.HosterRegistration.HosterClassName)
                .Distinct(StringComparer.Ordinal)
        )
        {
            if (
                await repository.HasCompletedUploadForHosterAsync(
                    archiveConfigId: archiveConfig.Id,
                    hosterClassName: hosterClassName,
                    cancellationToken: cancellationToken
                )
            )
            {
                return true;
            }
        }

        return false;
    }

    private async Task<HashSet<string>> LoadKnownHashesAsync(
        int archiveConfigId,
        CancellationToken cancellationToken
    )
    {
        var hashes = await repository.GetKnownArchiveFileHashesAsync(
            archiveConfigId: archiveConfigId,
            cancellationToken: cancellationToken
        );

        return hashes.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task ChangeArchiveFileHashesAsync(
        Archive archive,
        HashSet<string> knownHashes,
        CancellationToken cancellationToken
    )
    {
        foreach (var archiveFile in archive.ArchiveFiles)
        {
            if (!File.Exists(archiveFile.FullFileName))
            {
                logger.LogWarning(
                    "Cannot change MD5 hash for missing archive file {ArchiveFileName} in archive {ArchiveId}",
                    archiveFile.FullFileName,
                    archive.Id
                );

                continue;
            }

            string hash;
            do
            {
                await AppendNullByteAsync(archiveFile.FullFileName, cancellationToken);
                hash = await ComputeMd5HashAsync(archiveFile.FullFileName, cancellationToken);
            } while (!knownHashes.Add(hash));

            archiveFile.Md5Hash = hash;
        }

        logger.LogInformation(
            "Changed MD5 hash for {FileCount} archive files in archive {ArchiveId}",
            archive.ArchiveFiles.Count,
            archive.Id
        );
    }

    private async Task StoreArchiveFileHashesAsync(
        Archive archive,
        CancellationToken cancellationToken
    )
    {
        foreach (var archiveFile in archive.ArchiveFiles)
        {
            if (!File.Exists(archiveFile.FullFileName))
            {
                logger.LogWarning(
                    "Cannot compute MD5 hash for missing archive file {ArchiveFileName} in archive {ArchiveId}",
                    archiveFile.FullFileName,
                    archive.Id
                );

                continue;
            }

            archiveFile.Md5Hash = await ComputeMd5HashAsync(
                archiveFile.FullFileName,
                cancellationToken
            );
        }
    }

    private static async Task AppendNullByteAsync(
        string fullFileName,
        CancellationToken cancellationToken
    )
    {
        await using var stream = new FileStream(
            path: fullFileName,
            mode: FileMode.Append,
            access: FileAccess.Write,
            share: FileShare.Read
        );
        stream.WriteByte(0);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<string> ComputeMd5HashAsync(
        string fullFileName,
        CancellationToken cancellationToken
    )
    {
        await using var stream = SequentialFileReader.OpenRead(fullFileName);
        using var md5 = MD5.Create();
        var hash = await md5.ComputeHashAsync(stream, cancellationToken);

        return Convert.ToHexString(hash);
    }

    private async Task CreateArchiveAsync(
        ArchiveConfig config,
        IReadOnlyList<Upload> uploads,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "Creating archive for ArchiveConfig {ArchiveConfigId} with {UploadCount} uploads and archiver {ArchiverClassName}",
            config.Id,
            uploads.Count,
            config.ArchiverName
        );

        var releaseFolderPath = config.Release.ReleaseFolderPath;

        if (string.IsNullOrEmpty(releaseFolderPath) || !Directory.Exists(releaseFolderPath))
        {
            logger.LogError(
                "Release folder path {ReleaseFolderPath} does not exist for ArchiveConfig {ArchiveConfigId}",
                releaseFolderPath,
                config.Id
            );

            foreach (var upload in uploads)
            {
                upload.UploadState = UploadState.Failed;
                notificationService.CreateError(
                    message: $"Release folder path {releaseFolderPath} does not exist.",
                    entity: upload,
                    selector: n => n.Upload
                );
            }
            await repository.SaveChangesAsync(cancellationToken: cancellationToken);
            return;
        }

        var archiver = archiverFactory.GetByName(config.ArchiverName);
        var archiveDirectoryPath = fileSystemService.CreateTempDirectory(
            config.ArchiveFilesBasePath
        );

        var lastArchiveHasUnknownHashes = await repository.LastArchiveHasFilesWithoutHashAsync(
            archiveConfigId: config.Id,
            cancellationToken: cancellationToken
        );

        var useHashAppendStrategy = archiver.CanChangeHashInPlace && !lastArchiveHasUnknownHashes;

        var archiveSettings = await ResolveArchiveSettingsAsync(
            config: config,
            useHashAppendStrategy: useHashAppendStrategy,
            cancellationToken: cancellationToken
        );

        var archive = new Archive
        {
            ArchiveConfig = config,
            ArchiveFolderPath = archiveDirectoryPath,
            ArchiveFiles = [],
            ArchiveState = ArchiveState.Creating,
            ArchiveFileSizeMb = archiveSettings.ArchiveFileSizeMb,
            CreatedAt = timeProvider.GetLocalNow(),
            Uploads = uploads.ToList(),
            ErrorMessages = [],
        };

        repository.Add(archive);
        await repository.SaveChangesAsync(cancellationToken: cancellationToken);

        // For people that host Bearcat on a Synology NAS: DSM adds that nasty hidden @eaDir folder everywhere where media is.
        // So we should remove it before archiving.
        RemoveSynologyMetadataFolders(releaseFolderPath);

        await CreateOrUpdateUniqueFileAsync(releaseFolderPath, cancellationToken);

        var archiveResult = await archiver.ArchiveAsync(
            sourceFolderPath: releaseFolderPath,
            destinationPath: archiveDirectoryPath,
            archiveNamePrefix: config.ArchiveNamePrefix ?? Guid.NewGuid().ToString(),
            targetFileSizeMb: archiveSettings.ArchiveFileSizeMb,
            password: config.ArchivePassword,
            options: archiveSettings.Options,
            cancellationToken: cancellationToken
        );

        if (!archiveResult.IsSuccess)
        {
            logger.LogError(
                "Failed to create archive for ArchiveConfig {ArchiveConfigId}: {ErrorMessages}",
                config.Id,
                string.Join(",  ", archiveResult.ErrorMessages ?? [])
            );

            archive.ArchiveState = ArchiveState.CreationFailed;
            archive.ErrorMessages.AddRange(archiveResult.ErrorMessages ?? []);

            notificationService.CreateError(
                message: $"Failed to create archive: {string.Join(", ", archiveResult.ErrorMessages ?? [])}",
                entity: archive,
                selector: n => n.Archive
            );

            await repository.SaveChangesAsync(cancellationToken: cancellationToken);

            return;
        }

        archive.ArchiveFiles = archiveResult
            .CreatedFileNames.Select(f => new ArchiveFile { FullFileName = f })
            .ToList();

        await repository.SaveChangesAsync(cancellationToken: cancellationToken);

        await HashArchiveFilesAsync(archive, config, cancellationToken);
        FinalizeArchive(archive);

        await repository.SaveChangesAsync(cancellationToken: cancellationToken);

        logger.LogInformation(
            "Created archive {ArchiveId} for ArchiveConfig {ArchiveConfigId} with {FileCount} files",
            archive.Id,
            config.Id,
            archive.ArchiveFiles.Count
        );
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

    private static async Task CreateOrUpdateUniqueFileAsync(
        string releasePath,
        CancellationToken cancellationToken
    )
    {
        var uniqueFilePath = Path.Join(releasePath, UniqueFileName);
        await File.WriteAllTextAsync(
            path: uniqueFilePath,
            contents: Guid.NewGuid().ToString(),
            cancellationToken: cancellationToken
        );
    }

    private async Task<ArchiveSettings> ResolveArchiveSettingsAsync(
        ArchiveConfig config,
        bool useHashAppendStrategy,
        CancellationToken cancellationToken
    )
    {
        var strategy = configurationProvider.GetValue<ArchiveRepackagingConfiguration>(c =>
            c.Strategy
        );

        if (
            string.Equals(
                strategy,
                ArchiveRepackagingStrategies.NonceOnly,
                StringComparison.Ordinal
            )
        )
        {
            return new ArchiveSettings(
                ArchiveFileSizeMb: config.ArchiveFileSizeMb,
                Options: new ArchiveOptions(UseCompression: false, UseSolidArchive: false)
            );
        }

        if (
            string.Equals(
                strategy,
                ArchiveRepackagingStrategies.SolidCompression,
                StringComparison.Ordinal
            )
        )
        {
            return new ArchiveSettings(
                ArchiveFileSizeMb: config.ArchiveFileSizeMb,
                Options: new ArchiveOptions(UseCompression: true, UseSolidArchive: true)
            );
        }

        if (
            !string.Equals(
                strategy,
                ArchiveRepackagingStrategies.IncrementArchiveFileSize,
                StringComparison.Ordinal
            )
        )
        {
            logger.LogWarning(
                "Unknown archive repackaging strategy {Strategy}. Falling back to {DefaultStrategy}.",
                strategy,
                ArchiveRepackagingStrategies.IncrementArchiveFileSize
            );
        }

        if (useHashAppendStrategy)
        {
            return new ArchiveSettings(
                ArchiveFileSizeMb: config.ArchiveFileSizeMb,
                Options: new ArchiveOptions(UseCompression: false, UseSolidArchive: false)
            );
        }

        var lastArchiveFileSizeMb = await repository.GetLastArchiveFileSizeMbAsync(
            archiveConfigId: config.Id,
            cancellationToken: cancellationToken
        );

        return new ArchiveSettings(
            ArchiveFileSizeMb: lastArchiveFileSizeMb is null
                ? config.ArchiveFileSizeMb
                : lastArchiveFileSizeMb.Value + 1,
            Options: new ArchiveOptions(UseCompression: false, UseSolidArchive: false)
        );
    }

    private sealed record ArchiveSettings(int ArchiveFileSizeMb, ArchiveOptions Options);
}

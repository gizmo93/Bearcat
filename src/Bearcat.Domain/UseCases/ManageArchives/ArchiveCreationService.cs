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

    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        await repository.DeleteOrphanedArchivesAsync(cancellationToken);
        await ProcessUploadsWithoutArchiveAsync(cancellationToken);
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

            await ChangeArchiveFileHashesAsync(assignableArchive, cancellationToken);
        }

        foreach (var upload in uploads)
        {
            upload.ArchiveId = assignableArchive.Id;
            upload.UploadState = UploadState.Pending;
        }

        await repository.SaveChangesAsync(cancellationToken: cancellationToken);

        logger.LogInformation(
            "Assigned existing archive {ArchiveId} to {UploadCount} uploads",
            assignableArchive.Id,
            uploads.Count
        );

        return true;
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

    private async Task ChangeArchiveFileHashesAsync(
        Archive archive,
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

            await using var stream = new FileStream(
                path: archiveFile.FullFileName,
                mode: FileMode.Append,
                access: FileAccess.Write,
                share: FileShare.Read
            );
            stream.WriteByte(0);
            await stream.FlushAsync(cancellationToken);
        }

        logger.LogInformation(
            "Changed MD5 hash for {FileCount} archive files in existing archive {ArchiveId}",
            archive.ArchiveFiles.Count,
            archive.Id
        );
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

        if (!Directory.Exists(config.Release.ReleaseFolderPath))
        {
            logger.LogError(
                "Release folder path {ReleaseFolderPath} does not exist for ArchiveConfig {ArchiveConfigId}",
                config.Release.ReleaseFolderPath,
                config.Id
            );

            foreach (var upload in uploads)
            {
                upload.UploadState = UploadState.Failed;
                upload.Notifications.Add(
                    new Notification
                    {
                        Message =
                            $"Release folder path {config.Release.ReleaseFolderPath} does not exist.",
                        CreatedAt = timeProvider.GetLocalNow(),
                        Upload = upload,
                    }
                );
            }
            await repository.SaveChangesAsync(cancellationToken: cancellationToken);
            return;
        }

        var archiver = archiverFactory.GetByName(config.ArchiverName);
        var archiveDirectoryPath = fileSystemService.CreateTempDirectory(
            config.ArchiveFilesBasePath
        );
        var archiveSettings = await ResolveArchiveSettingsAsync(config, cancellationToken);

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

        await CreateOrUpdateUniqueFileAsync(config.Release.ReleaseFolderPath, cancellationToken);

        var archiveResult = await archiver.ArchiveAsync(
            sourceFolderPath: config.Release.ReleaseFolderPath,
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

        foreach (var upload in uploads)
        {
            upload.UploadState = UploadState.Pending;
        }

        archive.ArchiveState = ArchiveState.Created;
        archive.ArchiveFiles = archiveResult
            .CreatedFileNames.Select(f => new ArchiveFile { FullFileName = f })
            .ToList();

        await repository.SaveChangesAsync(cancellationToken: cancellationToken);

        logger.LogInformation(
            "Created archive {ArchiveId} for ArchiveConfig {ArchiveConfigId} with {FileCount} files",
            archive.Id,
            config.Id,
            archive.ArchiveFiles.Count
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

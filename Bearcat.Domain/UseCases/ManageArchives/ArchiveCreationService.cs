using Bearcat.Abstractions;
using Bearcat.Abstractions.Archiver;
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
    INotificationService notificationService)
{
    private const string UniqueFileName = "__nonce";

    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        await repository.DeleteOrphanedArchivesAsync(cancellationToken);
        await ProcessUploadsWithoutArchiveAsync(cancellationToken);
    }

    private async Task ProcessUploadsWithoutArchiveAsync(CancellationToken cancellationToken)
    {
        var uploadsWithoutArchive = await repository.GetUploadsWithoutArchiveAsync(cancellationToken);
        var archivesToCreate = new Dictionary<ArchiveConfig, List<Upload>>();

        foreach (var upload in uploadsWithoutArchive)
        {
            logger.LogInformation("Processing upload {UploadId} for UploadConfig {UploadConfigId} without archive",
                upload.Id,
                upload.UploadConfigId);

            var existingArchiveCanBeAssigned = await TryAssignExistingArchiveAsync(upload, cancellationToken);

            if (existingArchiveCanBeAssigned)
            {
                continue;
            }

            if (!archivesToCreate.TryAdd(upload.UploadConfig.ArchiveConfig, [upload]))
            {
                archivesToCreate[upload.UploadConfig.ArchiveConfig].Add(upload);
            }
        }

        if (archivesToCreate.Count == 0)
        {
            return;
        }

        logger.LogInformation("Creating {ArchiveCount} new archives for uploads", archivesToCreate.Count);

        foreach (var (archiveConfig, uploads) in archivesToCreate)
        {
            await CreateArchiveAsync(archiveConfig, uploads, cancellationToken);
        }
    }

    private async Task<bool> TryAssignExistingArchiveAsync(Upload upload, CancellationToken cancellationToken)
    {
        var assignableArchiveId = await repository.GetPossibleAssignableArchiveId(
            archiveConfigId: upload.UploadConfig.ArchiveConfigId,
            cancellationToken: cancellationToken);

        if (assignableArchiveId is null)
        {
            logger.LogInformation("Could not find existing archive for upload {UploadId} with UploadConfig {UploadConfigId}",
                upload.Id,
                upload.UploadConfigId);

            return false;
        }

        upload.ArchiveId = assignableArchiveId;
        upload.UploadState = UploadState.Pending;
        await repository.SaveChangesAsync(cancellationToken: cancellationToken);

        logger.LogInformation("Assigned existing archive {ArchiveId} to upload {UploadId}",
            assignableArchiveId,
            upload.Id);

        return true;
    }

    private async Task CreateArchiveAsync(
        ArchiveConfig config,
        IReadOnlyList<Upload> uploads,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Creating archive for ArchiveConfig {ArchiveConfigId} with {UploadCount} uploads and archiver {ArchiverClassName}",
            config.Id,
            uploads.Count,
            config.ArchiverName);

        var archiver = archiverFactory.GetByName(config.ArchiverName);
        var archiveDirectoryPath = fileSystemService.CreateTempDirectory(config.ArchiveFilesBasePath);

        var archive = new Archive
        {
            ArchiveConfig = config,
            ArchiveFolderPath = archiveDirectoryPath,
            ArchiveFiles = [],
            ArchiveState = ArchiveState.Creating,
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
            targetFileSizeMb: config.ArchiveFileSizeMb,
            password: config.ArchivePassword,
            cancellationToken: cancellationToken);

        if (!archiveResult.IsSuccess)
        {
            logger.LogError("Failed to create archive for ArchiveConfig {ArchiveConfigId}: {ErrorMessages}",
                config.Id,
                string.Join(",  ", archiveResult.ErrorMessages ?? []));

            archive.ArchiveState = ArchiveState.CreationFailed;
            archive.ErrorMessages.AddRange(archiveResult.ErrorMessages ?? []);

            notificationService
                .CreateError(
                    message: $"Failed to create archive: {string.Join(", ", archiveResult.ErrorMessages ?? [])}",
                    entity: archive,
                    selector: n => n.Archive);

            await repository.SaveChangesAsync(cancellationToken: cancellationToken);

            return;
        }

        foreach (var upload in uploads)
        {
            upload.UploadState = UploadState.Pending;
        }

        archive.ArchiveState = ArchiveState.Created;
        archive.ArchiveFiles = archiveResult.CreatedFileNames
            .Select(f => new ArchiveFile { FullFileName = f })
            .ToList();

        await repository.SaveChangesAsync(cancellationToken: cancellationToken);

        logger.LogInformation("Created archive {ArchiveId} for ArchiveConfig {ArchiveConfigId} with {FileCount} files",
            archive.Id,
            config.Id,
            archive.ArchiveFiles.Count);
    }

    private static async Task CreateOrUpdateUniqueFileAsync(string releasePath, CancellationToken cancellationToken)
    {
        var uniqueFilePath = Path.Join(releasePath, UniqueFileName);
        await File.WriteAllTextAsync(
            path: uniqueFilePath,
            contents: Guid.NewGuid().ToString(),
            cancellationToken: cancellationToken);
    }
}

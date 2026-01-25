using BearCat.Core.Domain.Abstractions;
using BearCat.Core.Domain.Abstractions.Archiver;
using BearCat.Core.Domain.Entities;
using BearCat.Core.Domain.UseCases.ManageArchives.Repositories;
using BearCat.Core.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace BearCat.Core.Domain.UseCases.ManageArchives;

public class ArchiveCreationService(
    IArchiveCreationRepository repository,
    ILogger<ArchiveCreationService> logger,
    IArchiverFactory archiverFactory,
    IFileSystemService fileSystemService,
    TimeProvider timeProvider)
{
    public async Task ProcessUploadsWithoutArchiveAsync(CancellationToken cancellationToken)
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

        var archiveResult = await archiver.ArchiveAsync(
            sourceFolderPath: config.Release.ReleaseFolderPath,
            destinationPath: archiveDirectoryPath,
            archiveNamePrefix: config.ArchiveNamePrefix,
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
            await repository.SaveChangesAsync(cancellationToken: cancellationToken);
            
            return;
        }

        foreach (var upload in uploads)
        {
            upload.UploadState = UploadState.Pending;
        }
        
        await repository.SaveChangesAsync(cancellationToken: cancellationToken);

        logger.LogInformation("Created archive {ArchiveId} for ArchiveConfig {ArchiveConfigId} with {FileCount} files",
            archive.Id,
            config.Id,
            archive.ArchiveFiles.Count);
    }
}

using BearCat.Core.Domain.Entities;
using BearCat.Core.Domain.UseCases.ManageArchives.Repositories;
using BearCat.Core.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace BearCat.Core.Domain.UseCases.ManageArchives;

public class ArchiveCreationService(
    IArchiveCreationRepository repository,
    ILogger<ArchiveCreationService> logger,
    ArchiverInstanceService archiverInstanceService)
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
        var alreadyUsedArchiveIds = upload.UploadConfig.Uploads
            .Select(u => u.ArchiveId)
            .OfType<int>()
            .ToHashSet();

        var availableArchive = upload.UploadConfig.ArchiveConfig
            .Archives
            .Where(a => !alreadyUsedArchiveIds.Contains(a.Id))
            .OrderBy(a => a.Id)
            .FirstOrDefault();

        if (availableArchive is null)
        {
            logger.LogInformation("Could not find existing archive for upload {UploadId} with UploadConfig {UploadConfigId}",
                upload.Id,
                upload.UploadConfigId);
            
            return false;
        }

        upload.Archive = availableArchive;
        upload.UploadState = UploadState.Pending;
        await repository.SaveChangesAsync(cancellationToken: cancellationToken);
        
        logger.LogInformation("Assigned existing archive {ArchiveId} to upload {UploadId}",
            availableArchive.Id,
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
            config.ArchiverFullClassName);

        var archiver = archiverInstanceService.GetByFullClassName(config.ArchiverFullClassName);
        var archiveDirectoryPath = CreateArchiveDirectory(config.ArchiveFilesBasePath);

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

            return;
        }

        var archive = new Archive
        {
            ArchiveConfig = config,
            ArchiveFolderPath = archiveDirectoryPath,
            ArchiveFiles = archiveResult.CreatedFileNames
                .Select(f => new ArchiveFile { FullFileName = f })
                .ToList(),
            Uploads = uploads.ToList()
        };

        foreach (var upload in uploads)
        {
            upload.UploadState = UploadState.Pending;
        }
        
        repository.Add(archive);
        await repository.SaveChangesAsync(cancellationToken: cancellationToken);
        
        logger.LogInformation("Created archive {ArchiveId} for ArchiveConfig {ArchiveConfigId} with {FileCount} files",
            archive.Id,
            config.Id,
            archive.ArchiveFiles.Count);
    }

    private static string CreateArchiveDirectory(string basePath)
    {
        var folderPath = Path.Combine(basePath, Guid.NewGuid().ToString("N"));
        return Directory.CreateDirectory(folderPath).FullName;
    }
}

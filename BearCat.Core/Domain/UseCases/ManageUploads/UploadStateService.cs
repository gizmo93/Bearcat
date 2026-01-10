using BearCat.Core.Domain.Abstractions.Hoster;
using BearCat.Core.Domain.Entities;
using BearCat.Core.Domain.UseCases.ManageUploads.Repositories;
using BearCat.Core.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace BearCat.Core.Domain.UseCases.ManageUploads;

public class UploadStateService(
    IUploadStateRepository uploadStateRepository,
    HosterInstanceService hosterInstanceService,
    ILogger<UploadStateService> logger)
{
    public async Task CheckUploadStatesAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        await ProcessUploadStateChecksAsync(utcNow, cancellationToken);
        await CreateMissingUploadsAsync(cancellationToken);
    }

    private async Task ProcessUploadStateChecksAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        var uploadsToCheck = await uploadStateRepository.GetUploadsToCheckAsync(utcNow, cancellationToken);

        foreach (var uploadGroup in uploadsToCheck.GroupBy(u => u.UploadConfig.HosterRegistration.HosterFullClassName))
        {
            var hoster = hosterInstanceService.GetByFullClassName(uploadGroup.Key);
            var hosterConfig = hoster.DeserializeHosterConfig(
                uploadGroup.First().UploadConfig.HosterRegistration.SerializedConfig);
            
            foreach (var upload in uploadGroup)
            {
                logger.LogInformation("Checking online status for Upload {UploadId} on hoster {Hoster}",
                    upload.Id,
                    hoster.Name);
                
                await UpdateOnlineStatusAsync(
                    hoster: hoster,
                    hosterConfig: hosterConfig,
                    upload: upload,
                    cancellationToken: cancellationToken);
                
                CreateNewUploadIfNeeded(upload);

                await uploadStateRepository.SaveChangesAsync(cancellationToken);
                
                logger.LogInformation("Updated online status for Upload {UploadId} to {OnlineState}",
                    upload.Id,
                    upload.OnlineState);
            }
        }
    }

    private async Task UpdateOnlineStatusAsync(
        IHoster hoster,
        IHosterConfig hosterConfig,
        Upload upload,
        CancellationToken cancellationToken)
    {
        var filesByUrl = upload.UploadedFiles
            .ToDictionary(f => f.HosterFileLink);

        var result = await hoster.CheckFilesExistAsync(
            hosterConfig: hosterConfig,
            fileUrls: filesByUrl.Keys.ToList(),
            cancellationToken: cancellationToken);
        
        if (!result.IsSuccess)
        {
            logger.LogError("Failed to check file existence for Upload {UploadId}: {ErrorMessages}",
                upload.Id,
                string.Join("; ", result.ErrorMessages));
            
            return;
        }

        foreach (var (url, exists) in result.StatusPerFileUrl)
        {
            var file = filesByUrl[url];

            file.OnlineState = exists ? OnlineState.Online : OnlineState.Offline;
            file.CheckedAt = DateTime.UtcNow;
        }
        
        var offlineFilesCount = upload.UploadedFiles.Count(f => f.OnlineState == OnlineState.Offline);

        if (offlineFilesCount > 0)
        {
            upload.OnlineState = offlineFilesCount == upload.UploadedFiles.Count
                ? OnlineState.Offline
                : OnlineState.PartiallyOnline;
        }
        else
        {
            upload.OnlineState = OnlineState.Online;
        }
    }

    private void CreateNewUploadIfNeeded(Upload upload)
    {
        if (upload.OnlineState == OnlineState.Online)
        {
            return;
        }

        var newUpload = new Upload
        {
            UploadConfig = upload.UploadConfig,
            CreatedAt = DateTime.UtcNow,
            UploadedAt = null,
            UploadState = UploadState.WaitingForArchive,
            OnlineState = OnlineState.Unknown,
        };
        
        uploadStateRepository.Add(newUpload);
    }

    private async Task CreateMissingUploadsAsync(CancellationToken cancellationToken)
    {
        var uploadConfigsWithoutUploads = await uploadStateRepository.GetUploadConfigsWithoutUploadsAsync(cancellationToken);
        
        foreach (var uploadConfig in uploadConfigsWithoutUploads)
        {
            var upload = new Upload
            {
                UploadConfig = uploadConfig,
                CreatedAt = DateTime.UtcNow,
                UploadState = UploadState.WaitingForArchive,
                OnlineState = OnlineState.Unknown,
            };
            uploadStateRepository.Add(upload);
            
            logger.LogInformation("Created missing upload for UploadConfig {UploadConfigId}", uploadConfig.Id);
        }
        
        await uploadStateRepository.SaveChangesAsync(cancellationToken);
    }
}

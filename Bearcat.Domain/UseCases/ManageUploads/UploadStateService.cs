using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageUploads;

public class UploadStateService(
    IUploadStateRepository uploadStateRepository,
    IHosterFactory hosterFactory,
    TimeProvider timeProvider,
    INotificationService notificationService,
    ILogger<UploadStateService> logger)
{
    public async Task CheckUploadStatesAsync(DateTime localNow, CancellationToken cancellationToken)
    {
        await ProcessUploadStateChecksAsync(localNow, cancellationToken);
        await CreateMissingUploadsAsync(cancellationToken);
        await ProcessOfflineUploadsWithoutReuploadsAsync(cancellationToken);
    }

    private async Task ProcessUploadStateChecksAsync(DateTime localNow, CancellationToken cancellationToken)
    {
        var uploadsToCheck = await uploadStateRepository.GetUploadsToCheckAsync(localNow, cancellationToken);

        foreach (var uploadGroup in uploadsToCheck.GroupBy(u => u.UploadConfig.HosterRegistration.HosterClassName))
        {
            var hoster = hosterFactory.GetByName(uploadGroup.Key);
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

            notificationService.CreateError(
                message: "Failed to check file existence on hoster.",
                entity: upload,
                selector: u => u.Upload);

            return;
        }

        foreach (var (url, exists) in result.StatusPerFileUrl)
        {
            var file = filesByUrl[url];

            file.OnlineState = exists ? OnlineState.Online : OnlineState.Offline;
            file.CheckedAt = timeProvider.GetLocalNow();
        }

        var offlineFilesCount = upload.UploadedFiles.Count(f => f.OnlineState == OnlineState.Offline);

        if (offlineFilesCount > 0)
        {
            upload.OnlineState = offlineFilesCount == upload.UploadedFiles.Count
                ? OnlineState.Offline
                : OnlineState.PartiallyOnline;

            notificationService.CreateWarning(message: "Some files are offline on the hoster",
                entity: upload,
                selector: u => u.Upload);
        }
        else
        {
            upload.OnlineState = OnlineState.Online;
        }
    }

    private async Task ProcessOfflineUploadsWithoutReuploadsAsync(CancellationToken cancellationToken)
    {
        var uploads = await uploadStateRepository.GetOfflineUploadsWithoutReuploadAsync(cancellationToken);

        foreach (var upload in uploads)
        {
            CreateNewUploadIfNeeded(upload);
        }

        await uploadStateRepository.SaveChangesAsync(cancellationToken);
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
            CreatedAt = timeProvider.GetLocalNow(),
            UploadedAt = null,
            UploadState = UploadState.WaitingForArchive,
            OnlineState = OnlineState.Unknown,
        };

        notificationService.CreateInfo(message: "Reupload scheduled due to offline files",
            entity: upload,
            selector: u => u.Upload);

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
                CreatedAt = timeProvider.GetLocalNow(),
                UploadState = UploadState.WaitingForArchive,
                OnlineState = OnlineState.Unknown,
            };
            uploadStateRepository.Add(upload);

            notificationService.CreateInfo(message: "Initial upload created for release",
                entity: upload,
                selector: u => u.Upload);

            logger.LogInformation("Created missing upload for UploadConfig {UploadConfigId}", uploadConfig.Id);
        }

        await uploadStateRepository.SaveChangesAsync(cancellationToken);
    }
}

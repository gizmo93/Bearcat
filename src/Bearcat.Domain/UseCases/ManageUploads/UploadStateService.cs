using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.Configurations;
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
    IApplicationConfigurationProvider configuration,
    INotificationService notificationService,
    ILogger<UploadStateService> logger
)
{
    public async Task CheckUploadStatesAsync(DateTime localNow, CancellationToken cancellationToken)
    {
        await ProcessUploadStateChecksAsync(localNow, cancellationToken);
        await CreateMissingUploadsAsync(localNow, cancellationToken);
        await ProcessAutomaticReuploadsAsync(localNow, cancellationToken);
    }

    public async Task<int> CreateManualReuploadAsync(
        int uploadId,
        CancellationToken cancellationToken = default
    )
    {
        var upload = await uploadStateRepository.GetUploadForReuploadAsync(
            uploadId,
            cancellationToken
        );

        if (
            upload.UploadState != UploadState.Canceled
            && upload.OnlineState is not OnlineState.Offline and not OnlineState.PartiallyOnline
        )
        {
            throw new InvalidOperationException(
                "Manual reuploads can only be created for offline, partially online, or canceled uploads."
            );
        }

        if (HasBlockingReupload(upload))
        {
            throw new InvalidOperationException(
                "A replacement upload already exists or is pending for this upload config."
            );
        }

        var newUpload = CreateNewUpload(upload, "Manual reupload scheduled");

        await uploadStateRepository.SaveChangesAsync(cancellationToken);

        return newUpload.Id;
    }

    public async Task<bool> CancelUploadAsync(
        int uploadId,
        CancellationToken cancellationToken = default
    )
    {
        var upload = await uploadStateRepository.GetByIdAsync(uploadId, cancellationToken);

        if (upload is null)
        {
            return false;
        }

        if (upload.UploadState == UploadState.CancellationRequested)
        {
            return true;
        }

        if (upload.UploadState is not UploadState.Pending and not UploadState.Uploading)
        {
            return false;
        }

        upload.UploadState = UploadState.CancellationRequested;

        notificationService.CreateInfo(
            message: "Upload cancellation requested",
            entity: upload,
            selector: u => u.Upload
        );

        await uploadStateRepository.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeleteUploadAsync(
        int uploadId,
        CancellationToken cancellationToken = default
    )
    {
        var upload = await uploadStateRepository.GetByIdAsync(uploadId, cancellationToken);

        if (upload is null)
        {
            return false;
        }

        if (!CanDeleteUpload(upload.UploadState))
        {
            return false;
        }

        uploadStateRepository.Remove(upload);
        await uploadStateRepository.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task ProcessUploadStateChecksAsync(
        DateTime localNow,
        CancellationToken cancellationToken
    )
    {
        var uploadsToCheck = await uploadStateRepository.GetUploadsToCheckAsync(
            localNow,
            cancellationToken
        );

        foreach (
            var uploadGroup in uploadsToCheck.GroupBy(u =>
                u.UploadConfig.HosterRegistration.HosterClassName
            )
        )
        {
            var hoster = hosterFactory.GetByName(uploadGroup.Key);
            var hosterConfig = hoster.DeserializeHosterConfig(
                uploadGroup.First().UploadConfig.HosterRegistration.SerializedConfig
            );

            foreach (var upload in uploadGroup)
            {
                logger.LogInformation(
                    "Checking online status for Upload {UploadId} on hoster {Hoster}",
                    upload.Id,
                    hoster.Name
                );

                await UpdateOnlineStatusAsync(
                    hoster: hoster,
                    hosterConfig: hosterConfig,
                    upload: upload,
                    cancellationToken: cancellationToken
                );

                await uploadStateRepository.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Updated online status for Upload {UploadId} to {OnlineState}",
                    upload.Id,
                    upload.OnlineState
                );
            }
        }
    }

    private async Task UpdateOnlineStatusAsync(
        IHoster hoster,
        IHosterConfig hosterConfig,
        Upload upload,
        CancellationToken cancellationToken
    )
    {
        var filesByUrl = upload.UploadedFiles.ToDictionary(f => f.HosterFileLink);

        var result = await hoster.CheckFilesExistAsync(
            hosterConfig: hosterConfig,
            fileUrls: filesByUrl.Keys.ToList(),
            cancellationToken: cancellationToken
        );

        if (!result.IsSuccess)
        {
            logger.LogError(
                "Failed to check file existence for Upload {UploadId}: {ErrorMessages}",
                upload.Id,
                string.Join("; ", result.ErrorMessages)
            );

            notificationService.CreateError(
                message: "Failed to check file existence on hoster.",
                entity: upload,
                selector: u => u.Upload
            );

            return;
        }

        foreach (var (url, exists) in result.StatusPerFileUrl)
        {
            var file = filesByUrl[url];

            file.OnlineState = exists ? OnlineState.Online : OnlineState.Offline;
            file.CheckedAt = timeProvider.GetLocalNow();
        }

        var offlineFilesCount = upload.UploadedFiles.Count(f =>
            f.OnlineState == OnlineState.Offline
        );

        if (offlineFilesCount > 0)
        {
            upload.OnlineState =
                offlineFilesCount == upload.UploadedFiles.Count
                    ? OnlineState.Offline
                    : OnlineState.PartiallyOnline;

            notificationService.CreateWarning(
                message: "Some files are offline on the hoster",
                entity: upload,
                selector: u => u.Upload
            );
        }
        else
        {
            upload.OnlineState = OnlineState.Online;
        }
    }

    private async Task ProcessAutomaticReuploadsAsync(
        DateTime localNow,
        CancellationToken cancellationToken
    )
    {
        var uploads = (
            await uploadStateRepository.GetUploadsEligibleForAutomaticReuploadAsync(
                cancellationToken
            )
        )
            .GroupBy(u => u.UploadConfigId)
            .Select(g => g.MaxBy(u => u.Id))
            .OfType<Upload>()
            .ToList();

        foreach (var upload in uploads)
        {
            if (IsAutomaticReuploadDue(upload, localNow))
            {
                CreateNewUpload(upload, "Automatic reupload scheduled due to offline files");
            }
        }

        await uploadStateRepository.SaveChangesAsync(cancellationToken);
    }

    private Upload CreateNewUpload(Upload upload, string notificationMessage)
    {
        var newUpload = new Upload
        {
            UploadConfig = upload.UploadConfig,
            CreatedAt = timeProvider.GetLocalNow(),
            UploadedAt = null,
            UploadState = UploadState.WaitingForArchive,
            OnlineState = OnlineState.Unknown,
        };

        notificationService.CreateInfo(
            message: notificationMessage,
            entity: upload,
            selector: u => u.Upload
        );

        uploadStateRepository.Add(newUpload);
        return newUpload;
    }

    private static bool IsAutomaticReuploadDue(Upload upload, DateTime localNow)
    {
        var releaseGroup = upload.UploadConfig.Release.ReleaseGroup;

        if (!releaseGroup.EnableAutomaticReuploads || upload.UploadedFiles.Count == 0)
        {
            return false;
        }

        if (upload.UploadedFiles.Any(f => f.CheckedAt is null))
        {
            return false;
        }

        var oldestCheckedAt = upload.UploadedFiles.Min(f => f.CheckedAt!.Value);
        var threshold = localNow.AddHours(-releaseGroup.NumberOfHoursUntilReupload);

        return oldestCheckedAt <= threshold;
    }

    private static bool HasBlockingReupload(Upload upload)
    {
        List<UploadState> reuploadBlockingStates =
        [
            UploadState.Pending,
            UploadState.Uploading,
            UploadState.WaitingForArchive,
            UploadState.Failed,
            UploadState.CancellationRequested,
        ];

        return upload.UploadConfig.Uploads.Any(ru =>
            ru.Id != upload.Id
            && (
                ru.OnlineState == OnlineState.Online
                || reuploadBlockingStates.Contains(ru.UploadState)
            )
        );
    }

    private static bool CanDeleteUpload(UploadState uploadState) =>
        uploadState
            is UploadState.Pending
                or UploadState.Completed
                or UploadState.Failed
                or UploadState.Canceled;

    private async Task CreateMissingUploadsAsync(
        DateTime localNow,
        CancellationToken cancellationToken
    )
    {
        var cooldownMinutes = Math.Max(
            0,
            configuration.GetValue<InitialUploadConfiguration>(c => c.CooldownMinutes)
        );
        var releaseCreatedBefore = localNow.AddMinutes(-cooldownMinutes);
        var uploadConfigsWithoutUploads =
            await uploadStateRepository.GetUploadConfigsWithoutUploadsAsync(
                releaseCreatedBefore,
                cancellationToken
            );

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

            notificationService.CreateInfo(
                message: "Initial upload created for release",
                entity: upload,
                selector: u => u.Upload
            );

            logger.LogInformation(
                "Created missing upload for UploadConfig {UploadConfigId}",
                uploadConfig.Id
            );
        }

        await uploadStateRepository.SaveChangesAsync(cancellationToken);
    }
}

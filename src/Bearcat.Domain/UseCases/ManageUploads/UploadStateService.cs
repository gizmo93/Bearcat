using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.QualityGate;
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
    HosterCaptchaVerificationService captchaVerificationService,
    ILogger<UploadStateService> logger,
    ISecretProtector secretProtector,
    QualityGateEvaluator qualityGateEvaluator
)
{
    private static readonly TimeSpan FailedCheckNotificationThreshold = TimeSpan.FromHours(3);

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
            && upload.UploadState != UploadState.Failed
            && upload.OnlineState is not OnlineState.Offline and not OnlineState.PartiallyOnline
        )
        {
            throw new InvalidOperationException(
                "Manual reuploads can only be created for offline, partially online, canceled, or failed uploads."
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

        notificationService.Create(
            kind: NotificationKind.UploadCancellationRequested,
            message: "Upload cancellation requested",
            entity: upload,
            selector: u => u.Upload
        );

        await uploadStateRepository.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> ResumeUploadAsync(
        int uploadId,
        CancellationToken cancellationToken = default
    )
    {
        var upload = await uploadStateRepository.GetByIdAsync(uploadId, cancellationToken);

        if (upload is null)
        {
            return false;
        }

        if (upload.UploadState != UploadState.Canceled)
        {
            return false;
        }

        upload.UploadState = UploadState.Pending;

        await uploadStateRepository.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> CheckUploadStateNowAsync(
        int uploadId,
        CancellationToken cancellationToken = default
    )
    {
        var upload = await uploadStateRepository.GetUploadForOnlineCheckAsync(
            uploadId,
            cancellationToken
        );

        if (upload is null || upload.UploadedFiles.Count == 0)
        {
            return false;
        }

        var hoster = hosterFactory.GetByName(
            upload.UploadConfig.HosterRegistration.HosterClassName
        );
        var hosterConfig = hoster.DeserializeHosterConfig(
            secretProtector.Unprotect(upload.UploadConfig.HosterRegistration.SerializedConfig)
        );

        logger.LogInformation(
            "Checking online status on demand for Upload {UploadId} on hoster {Hoster}",
            upload.Id,
            hoster.Name
        );

        await UpdateOnlineStatusAsync(
            hoster: hoster,
            hosterConfig: hosterConfig,
            upload: upload,
            localNow: timeProvider.GetLocalNow(),
            cancellationToken: cancellationToken
        );

        await uploadStateRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Updated online status on demand for Upload {UploadId} to {OnlineState}",
            upload.Id,
            upload.OnlineState
        );

        return true;
    }

    public async Task<bool> SetUploadOfflineAsync(
        int uploadId,
        CancellationToken cancellationToken = default
    )
    {
        var upload = await uploadStateRepository.GetUploadForOnlineCheckAsync(
            uploadId: uploadId,
            cancellationToken: cancellationToken
        );

        if (upload is null)
        {
            return false;
        }

        if (upload.OnlineState is not OnlineState.Online and not OnlineState.PartiallyOnline)
        {
            return false;
        }

        var localNow = timeProvider.GetLocalNow();

        foreach (var file in upload.UploadedFiles)
        {
            file.OnlineState = OnlineState.Offline;
            file.CheckedAt = localNow;
        }

        SetOnlineState(upload: upload, onlineState: OnlineState.Offline, localNow: localNow);

        notificationService.Create(
            kind: NotificationKind.UploadMarkedOffline,
            message: "Upload manually marked as offline",
            entity: upload,
            selector: u => u.Upload
        );

        await uploadStateRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Upload {UploadId} was manually marked as offline", upload.Id);

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
                secretProtector.Unprotect(
                    uploadGroup.First().UploadConfig.HosterRegistration.SerializedConfig
                )
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
                    localNow: localNow,
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
        DateTime localNow,
        CancellationToken cancellationToken
    )
    {
        var previousOnlineState = upload.OnlineState;
        var filesWithoutUrl = upload
            .UploadedFiles.Where(f => string.IsNullOrWhiteSpace(f.HosterFileLink))
            .ToList();

        foreach (var file in filesWithoutUrl)
        {
            file.OnlineState = OnlineState.Offline;
            file.CheckedAt = localNow;
        }

        if (filesWithoutUrl.Count > 0)
        {
            logger.LogWarning(
                "Skipping {FileCount} uploaded files without hoster links for Upload {UploadId}",
                filesWithoutUrl.Count,
                upload.Id
            );
        }

        var filesByUrl = upload
            .UploadedFiles.Where(f => !string.IsNullOrWhiteSpace(f.HosterFileLink))
            .DistinctBy(h => h.HosterFileLink)
            .ToDictionary(f => f.HosterFileLink);

        if (filesByUrl.Count == 0)
        {
            SetOnlineState(upload, OnlineState.Offline, localNow);
            return;
        }

        FileExistResult result;

        try
        {
            result = await hoster.CheckFilesExistAsync(
                hosterConfig: hosterConfig,
                files: filesByUrl
                    .Values.Select(file => new FileUrlToCheckDto(
                        Url: file.HosterFileLink,
                        ExternalId: file.ExternalId,
                        HosterFolderId: file.HosterFolderId
                    ))
                    .ToList(),
                cancellationToken: cancellationToken
            );
        }
        catch (CaptchaVerificationRequiredException ex)
        {
            captchaVerificationService.MarkRequired(upload, ex.Message);
            return;
        }

        if (!result.IsSuccess)
        {
            logger.LogError(
                "Failed to check file existence for Upload {UploadId}: {ErrorMessages}",
                upload.Id,
                string.Join("; ", result.ErrorMessages)
            );

            var lastOnlineCheck = upload
                .UploadedFiles.Where(f => f.OnlineState == OnlineState.Online)
                .Min(f => f.CheckedAt);

            if (
                lastOnlineCheck is not null
                && localNow - lastOnlineCheck.Value >= FailedCheckNotificationThreshold
            )
            {
                notificationService.Create(
                    kind: NotificationKind.HosterStatusCheckFailed,
                    message: $"Failed to check file existence on hoster, Error messages: {string.Join(", ", result.ErrorMessages)}",
                    entity: upload,
                    selector: u => u.Upload
                );
            }

            return;
        }

        foreach (var (url, exists) in result.StatusPerFileUrl)
        {
            if (!filesByUrl.TryGetValue(url, out var file))
            {
                logger.LogWarning(
                    "Hoster {Hoster} returned an unknown file URL while checking Upload {UploadId}: {Url}",
                    hoster.Name,
                    upload.Id,
                    url
                );

                continue;
            }

            file.OnlineState = exists ? OnlineState.Online : OnlineState.Offline;
            file.CheckedAt = localNow;

            if (result.DownloadCountPerFileUrl?.TryGetValue(url, out var downloadCount) == true)
            {
                file.DownloadCount = downloadCount;
            }
        }

        var offlineFilesCount = upload.UploadedFiles.Count(f =>
            f.OnlineState == OnlineState.Offline
        );

        var newOnlineState = offlineFilesCount switch
        {
            0 => OnlineState.Online,
            _ when offlineFilesCount == upload.UploadedFiles.Count => OnlineState.Offline,
            _ => OnlineState.PartiallyOnline,
        };

        SetOnlineState(upload, newOnlineState, localNow);

        CreateOfflineNotificationIfNeeded(upload, previousOnlineState);
    }

    private static void SetOnlineState(Upload upload, OnlineState onlineState, DateTime localNow)
    {
        upload.OnlineState = onlineState;

        if (onlineState is OnlineState.PartiallyOnline or OnlineState.Offline)
        {
            upload.NotFullyOnlineSince ??= localNow;
        }
        else
        {
            upload.NotFullyOnlineSince = null;
        }

        if (onlineState is OnlineState.Offline)
        {
            upload.FullyOfflineSince ??= localNow;
        }
        else
        {
            upload.FullyOfflineSince = null;
        }
    }

    private void CreateOfflineNotificationIfNeeded(Upload upload, OnlineState previousOnlineState)
    {
        var becameOffline =
            upload.OnlineState != previousOnlineState
            && upload.OnlineState is OnlineState.Offline or OnlineState.PartiallyOnline;

        if (!becameOffline)
        {
            return;
        }

        var allOrSome = upload.OnlineState is OnlineState.Offline ? "All" : "Some";

        notificationService.Create(
            kind: NotificationKind.FilesOffline,
            message: $"{allOrSome} files are offline on the hoster",
            entity: upload,
            selector: u => u.Upload
        );
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

        var evaluatedReleaseIds = new HashSet<int>();

        foreach (var upload in uploads.Where(u => IsAutomaticReuploadDue(u, localNow)))
        {
            var release = upload.UploadConfig.Release;

            if (!EvaluateAndCheckQualityGate(release, localNow, evaluatedReleaseIds))
            {
                logger.LogInformation(
                    "Skipping automatic reupload for Release {ReleaseId} because the quality gate is not satisfied",
                    release.Id
                );
                continue;
            }

            CreateNewUpload(upload, "Automatic reupload scheduled due to offline files");
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
            PremiumOnlyDownload = upload.UploadConfig.PremiumOnlyDownload,
        };

        notificationService.Create(
            kind: NotificationKind.AutomaticReuploadCreated,
            message: notificationMessage,
            entity: upload,
            selector: u => u.Upload
        );

        uploadStateRepository.Add(newUpload);
        return newUpload;
    }

    private bool EvaluateAndCheckQualityGate(
        Release release,
        DateTime localNow,
        HashSet<int> evaluatedReleaseIds
    )
    {
        // We can't execute most checks of unmanaged Releases as we just have a set of RAR files to work with
        // and no Release folder with uncompressed data, so we skip them.
        if (release.ReleaseType is ReleaseType.Unmanaged)
        {
            return true;
        }

        if (release.ReleaseGroup.QualityProfileId is null)
        {
            return true;
        }

        if (
            evaluatedReleaseIds.Add(release.Id)
            && release.QualityGateState is QualityGateState.NotEvaluated or QualityGateState.Passed
        )
        {
            qualityGateEvaluator.EvaluateAndApply(release, localNow);
        }

        return release.QualityGateState
            is QualityGateState.Passed
                or QualityGateState.ManuallyApproved;
    }

    private static bool IsAutomaticReuploadDue(Upload upload, DateTime localNow)
    {
        var releaseGroup = upload.UploadConfig.Release.ReleaseGroup;
        var hosterRegistration = upload.UploadConfig.HosterRegistration;

        if (!releaseGroup.EnableAutomaticReuploads || upload.UploadedFiles.Count == 0)
        {
            return false;
        }

        var trigger =
            hosterRegistration.ReuploadTriggerOverride ?? ReuploadTrigger.PartiallyOrFullyOffline;

        var offlineSince =
            trigger is ReuploadTrigger.OnlyWhenFullyOffline
                ? upload.FullyOfflineSince
                : upload.NotFullyOnlineSince;

        if (offlineSince is null)
        {
            return false;
        }

        var numberOfHoursUntilReupload =
            hosterRegistration.NumberOfHoursUntilReuploadOverride
            ?? releaseGroup.NumberOfHoursUntilReupload;

        var threshold = localNow.AddHours(-numberOfHoursUntilReupload);

        return offlineSince <= threshold;
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

        var evaluatedReleaseIds = new HashSet<int>();

        foreach (var releaseUploadConfigs in uploadConfigsWithoutUploads.GroupBy(uc => uc.Release))
        {
            var release = releaseUploadConfigs.Key;

            if (!EvaluateAndCheckQualityGate(release, localNow, evaluatedReleaseIds))
            {
                logger.LogInformation(
                    "Skipping upload creation for Release {ReleaseId} because the quality gate is not satisfied",
                    release.Id
                );
                continue;
            }

            foreach (var uploadConfig in releaseUploadConfigs)
            {
                var upload = new Upload
                {
                    UploadConfig = uploadConfig,
                    CreatedAt = timeProvider.GetLocalNow(),
                    UploadState = UploadState.WaitingForArchive,
                    OnlineState = OnlineState.Unknown,
                    PremiumOnlyDownload = uploadConfig.PremiumOnlyDownload,
                };
                uploadStateRepository.Add(upload);

                notificationService.Create(
                    kind: NotificationKind.InitialUploadCreated,
                    message: "Initial upload created for release",
                    entity: upload,
                    selector: u => u.Upload
                );

                logger.LogInformation(
                    "Created missing upload for UploadConfig {UploadConfigId}",
                    uploadConfig.Id
                );
            }
        }

        await uploadStateRepository.SaveChangesAsync(cancellationToken);
    }
}

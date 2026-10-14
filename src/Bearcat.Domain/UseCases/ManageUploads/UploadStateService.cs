using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Proxies;
using Bearcat.Domain.Shared.QualityGate;
using Bearcat.Domain.UseCases.ManageUploads.Exceptions;
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
    HosterCredentialsRejectionService credentialsRejectionService,
    ILogger<UploadStateService> logger,
    ISecretProtector secretProtector,
    QualityGateEvaluator qualityGateEvaluator
)
{
    private const int MaximumUploadsToCheckPerHosterRegistrationPerRun = 100;
    private const int MaximumAutomaticReuploadsPerRun = 100;
    private static readonly TimeSpan FailedCheckNotificationThreshold = TimeSpan.FromHours(3);

    public TimeSpan LinkCheckTimeout { get; set; } = TimeSpan.FromMinutes(2);

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

        if (!upload.CanBeManuallyReuploaded())
        {
            throw new InvalidUploadStateException(
                "Manual reuploads can only be created for offline, partially online, canceled, or failed uploads."
            );
        }

        if (upload.HasExistingOrPendingReplacementUpload())
        {
            throw new InvalidUploadStateException(
                "A replacement upload already exists or is pending for this upload config."
            );
        }

        var newUpload = AddReuploadOfUpload(upload);

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

        upload.UploadState = upload.ArchiveId is null
            ? UploadState.WaitingForArchive
            : UploadState.Pending;

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

        var registration = upload.HosterRegistration!;

        logger.LogInformation(
            "Checking online status on demand for Upload {UploadId} on hoster registration {HosterRegistration}",
            upload.Id,
            registration.Name
        );

        await UpdateOnlineStatusAsync(
            registration: registration,
            uploads: [upload],
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
            file.SetOnlineStateAndCheckedAt(OnlineState.Offline, localNow);
        }

        upload.SetOnlineStateAndOfflineSinceTimestamps(OnlineState.Offline, localNow);

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

        if (!upload.CanBeDeleted())
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
        var registrations = await uploadStateRepository.GetActiveHosterRegistrationsAsync(
            cancellationToken
        );

        foreach (var registration in registrations)
        {
            var uploads = await uploadStateRepository.GetLeastRecentlyCheckedUploadsToCheckAsync(
                hosterRegistrationId: registration.Id,
                localNow: localNow,
                maximumUploadCount: MaximumUploadsToCheckPerHosterRegistrationPerRun,
                cancellationToken: cancellationToken
            );

            if (uploads.Count == 0)
            {
                continue;
            }

            await UpdateOnlineStatusAsync(
                registration: registration,
                uploads: uploads,
                localNow: localNow,
                cancellationToken: cancellationToken
            );

            await uploadStateRepository.SaveChangesAsync(cancellationToken);

            foreach (var upload in uploads)
            {
                logger.LogInformation(
                    "Updated online status for Upload {UploadId} to {OnlineState}",
                    upload.Id,
                    upload.OnlineState
                );
            }
        }
    }

    private async Task UpdateOnlineStatusAsync(
        HosterRegistration registration,
        IReadOnlyList<Upload> uploads,
        DateTime localNow,
        CancellationToken cancellationToken
    )
    {
        foreach (var upload in uploads)
        {
            MarkFilesWithoutLinkOffline(upload, localNow);
        }

        var uploadsWithLinks = uploads
            .Where(upload => upload.UploadedFiles.Any(file => file.HasHosterFileLink()))
            .ToList();

        foreach (var upload in uploads.Except(uploadsWithLinks))
        {
            upload.SetOnlineStateAndOfflineSinceTimestamps(OnlineState.Offline, localNow);
        }

        if (uploadsWithLinks.Count == 0)
        {
            return;
        }

        var filesByUrl = uploadsWithLinks
            .SelectMany(u => u.UploadedFiles)
            .Where(file => file.HasHosterFileLink())
            .ToLookup(f => f.HosterFileLink);

        var hoster = hosterFactory.GetByName(registration.HosterClassName);
        var hosterConfig = hoster.DeserializeHosterConfig(
            secretProtector.Unprotect(registration.SerializedConfig)
        );

        logger.LogInformation(
            "Checking {FileCount} links of {UploadCount} uploads on hoster {Hoster} (registration {HosterRegistration})",
            filesByUrl.Count,
            uploadsWithLinks.Count,
            hoster.Name,
            registration.Name
        );

        FileExistResult result;

        using var linkCheckCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkCheckCancellationTokenSource.CancelAfter(LinkCheckTimeout);

        try
        {
            using var proxyScope = HosterRegistrationProxyScope.EnterForUpload(registration);

            result = await hoster.CheckFilesExistAsync(
                hosterConfig: hosterConfig,
                files: filesByUrl
                    .Select(files => files.First())
                    .Select(file => new FileUrlToCheckDto(
                        Url: file.HosterFileLink,
                        ExternalId: file.ExternalId,
                        HosterFolderId: file.HosterFolderId
                    ))
                    .ToList(),
                cancellationToken: linkCheckCancellationTokenSource.Token
            );
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result = new FileExistResult(
                IsSuccess: false,
                ErrorMessages: [$"The link check did not finish within {LinkCheckTimeout}."],
                StatusPerFileUrl: new Dictionary<string, bool>()
            );
        }
        catch (CaptchaVerificationRequiredException ex)
        {
            await captchaVerificationService.MarkRequiredAsync(
                registration,
                ex.Message,
                cancellationToken
            );
            return;
        }
        catch (HosterCredentialsRejectedException ex)
        {
            await credentialsRejectionService.DeactivateAndNotifyAsync(
                registration,
                ex.Message,
                cancellationToken
            );
            return;
        }

        if (!result.IsSuccess)
        {
            logger.LogError(
                "Failed to check file existence for {UploadCount} uploads on hoster registration {HosterRegistration}: {ErrorMessages}",
                uploadsWithLinks.Count,
                registration.Name,
                string.Join("; ", result.ErrorMessages)
            );

            await CreateCheckFailedNotificationIfNeededAsync(
                registration: registration,
                uploads: uploadsWithLinks,
                result: result,
                localNow: localNow,
                cancellationToken: cancellationToken
            );

            return;
        }

        ApplyFileStates(hoster, filesByUrl, result, localNow);

        foreach (var upload in uploadsWithLinks)
        {
            var previousOnlineState = upload.OnlineState;

            upload.SetOnlineStateAndOfflineSinceTimestamps(
                upload.CalculateOnlineStateFromUploadedFiles(),
                localNow
            );

            CreateOfflineNotificationIfNeeded(upload, previousOnlineState);
        }
    }

    private void MarkFilesWithoutLinkOffline(Upload upload, DateTime localNow)
    {
        var filesWithoutLink = upload.UploadedFiles.Where(f => !f.HasHosterFileLink()).ToList();

        if (filesWithoutLink.Count == 0)
        {
            return;
        }

        foreach (var file in filesWithoutLink)
        {
            file.SetOnlineStateAndCheckedAt(OnlineState.Offline, localNow);
        }

        logger.LogWarning(
            "Skipping {FileCount} uploaded files without hoster links for Upload {UploadId}",
            filesWithoutLink.Count,
            upload.Id
        );
    }

    private void ApplyFileStates(
        IHoster hoster,
        ILookup<string, UploadedFile> filesByUrl,
        FileExistResult result,
        DateTime localNow
    )
    {
        foreach (var (url, exists) in result.StatusPerFileUrl)
        {
            if (!filesByUrl.Contains(url))
            {
                logger.LogWarning(
                    "Hoster {Hoster} returned an unknown file URL: {Url}",
                    hoster.Name,
                    url
                );

                continue;
            }

            foreach (var file in filesByUrl[url])
            {
                file.SetOnlineStateAndCheckedAt(
                    exists ? OnlineState.Online : OnlineState.Offline,
                    localNow
                );

                if (result.DownloadCountPerFileUrl?.TryGetValue(url, out var downloadCount) == true)
                {
                    file.DownloadCount = downloadCount;
                }
            }
        }
    }

    private async Task CreateCheckFailedNotificationIfNeededAsync(
        HosterRegistration registration,
        IReadOnlyList<Upload> uploads,
        FileExistResult result,
        DateTime localNow,
        CancellationToken cancellationToken
    )
    {
        var uploadsWithOldOnlineCheckCount = uploads.Count(upload =>
            IsLastOnlineCheckOlderThanThreshold(upload, localNow)
        );

        if (uploadsWithOldOnlineCheckCount == 0)
        {
            return;
        }

        var hasUnresolvedNotification =
            await uploadStateRepository.HasUnresolvedNotificationForHosterRegistrationAsync(
                hosterRegistrationId: registration.Id,
                kind: NotificationKind.HosterStatusCheckFailed,
                cancellationToken: cancellationToken
            );

        if (hasUnresolvedNotification)
        {
            return;
        }

        notificationService.Create(
            kind: NotificationKind.HosterStatusCheckFailed,
            message: $"Failed to check file existence on hoster registration '{registration.Name}' for {uploadsWithOldOnlineCheckCount} uploads, Error messages: {string.Join(", ", result.ErrorMessages)}",
            entity: registration,
            selector: n => n.HosterRegistration
        );
    }

    private static bool IsLastOnlineCheckOlderThanThreshold(Upload upload, DateTime localNow)
    {
        var lastOnlineCheck = upload
            .UploadedFiles.Where(f => f.OnlineState == OnlineState.Online)
            .Min(f => f.CheckedAt);

        return lastOnlineCheck is not null
            && localNow - lastOnlineCheck.Value >= FailedCheckNotificationThreshold;
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
        var uploads = await uploadStateRepository.GetUploadsDueForAutomaticReuploadAsync(
            localNow: localNow,
            maximumUploadCount: MaximumAutomaticReuploadsPerRun,
            cancellationToken: cancellationToken
        );

        var evaluatedReleaseIds = new HashSet<int>();

        foreach (var upload in uploads)
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

            AddReuploadOfUpload(upload);

            notificationService.Create(
                kind: NotificationKind.AutomaticReuploadCreated,
                message: "Automatic reupload scheduled due to offline files",
                entity: upload,
                selector: u => u.Upload
            );
        }

        await uploadStateRepository.SaveChangesAsync(cancellationToken);
    }

    private Upload AddReuploadOfUpload(Upload previousUpload)
    {
        var reupload = new Upload
        {
            UploadConfig = previousUpload.UploadConfig,
            HosterRegistrationId = GetHosterRegistrationIdForReupload(previousUpload),
            CreatedAt = timeProvider.GetLocalNow(),
            UploadedAt = null,
            UploadState = UploadState.WaitingForArchive,
            OnlineState = OnlineState.Unknown,
            PremiumOnlyDownload = previousUpload.UploadConfig.PremiumOnlyDownload,
        };

        uploadStateRepository.Add(reupload);
        return reupload;
    }

    private static int? GetHosterRegistrationIdForReupload(Upload previousUpload)
    {
        var uploadConfig = previousUpload.UploadConfig;

        if (uploadConfig.HosterAccountPoolId is null)
        {
            return uploadConfig.HosterRegistrationId;
        }

        var previousRegistration = previousUpload.HosterRegistration;

        var previousRegistrationIsStillUsable =
            previousRegistration is not null
            && previousRegistration.IsUsableForUploads()
            && uploadConfig.HosterAccountPool!.Members.Any(member =>
                member.HosterRegistrationId == previousRegistration.Id
            );

        return previousRegistrationIsStillUsable ? previousRegistration!.Id : null;
    }

    private bool EvaluateAndCheckQualityGate(
        Release release,
        DateTime localNow,
        HashSet<int> evaluatedReleaseIds
    )
    {
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
                    HosterRegistrationId = uploadConfig.HosterRegistrationId,
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

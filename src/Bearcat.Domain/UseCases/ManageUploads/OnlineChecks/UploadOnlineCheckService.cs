using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Proxies;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageUploads.OnlineChecks;

public class UploadOnlineCheckService(
    IUploadStateRepository uploadStateRepository,
    IHosterFactory hosterFactory,
    TimeProvider timeProvider,
    INotificationService notificationService,
    HosterCaptchaVerificationService captchaVerificationService,
    HosterCredentialsRejectionService credentialsRejectionService,
    ILogger<UploadOnlineCheckService> logger,
    ISecretProtector secretProtector
)
{
    private const int MaximumUploadsToCheckPerHosterRegistrationPerRun = 100;
    private static readonly TimeSpan FailedCheckNotificationThreshold = TimeSpan.FromHours(3);

    public TimeSpan LinkCheckTimeout { get; set; } = TimeSpan.FromMinutes(2);

    public async Task CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync(
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

            await CheckOnlineStateOfUploadsAsync(
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

    public async Task<bool> CheckOnlineStateOfUploadNowAsync(
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

        await CheckOnlineStateOfUploadsAsync(
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

    private async Task CheckOnlineStateOfUploadsAsync(
        HosterRegistration registration,
        IReadOnlyList<Upload> uploads,
        DateTime localNow,
        CancellationToken cancellationToken
    )
    {
        foreach (var upload in uploads)
        {
            MarkFilesWithoutHosterFileLinkOffline(upload, localNow);
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

        var result = await CheckFilesExistOnHosterAsync(
            registration: registration,
            hoster: hoster,
            hosterConfig: hosterConfig,
            filesByUrl: filesByUrl,
            cancellationToken: cancellationToken
        );

        if (result is null)
        {
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

            await CreateCheckFailedNotificationWhenLastOnlineCheckIsTooOldAsync(
                registration: registration,
                uploads: uploadsWithLinks,
                result: result,
                localNow: localNow,
                cancellationToken: cancellationToken
            );

            return;
        }

        ApplyFileExistResultToUploadedFiles(hoster, filesByUrl, result, localNow);

        UpdateUploadOnlineStatesAndNotifyNewlyOfflineUploads(uploadsWithLinks, localNow);
    }

    private void MarkFilesWithoutHosterFileLinkOffline(Upload upload, DateTime localNow)
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

    private async Task<FileExistResult?> CheckFilesExistOnHosterAsync(
        HosterRegistration registration,
        IHoster hoster,
        IHosterConfig hosterConfig,
        ILookup<string, UploadedFile> filesByUrl,
        CancellationToken cancellationToken
    )
    {
        using var linkCheckCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkCheckCancellationTokenSource.CancelAfter(LinkCheckTimeout);

        try
        {
            using var proxyScope = HosterRegistrationProxyScope.EnterForUpload(registration);

            return await hoster.CheckFilesExistAsync(
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
            return new FileExistResult(
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
            return null;
        }
        catch (HosterCredentialsRejectedException ex)
        {
            await credentialsRejectionService.DeactivateAndNotifyAsync(
                registration,
                ex.Message,
                cancellationToken
            );
            return null;
        }
    }

    private void ApplyFileExistResultToUploadedFiles(
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

    private void UpdateUploadOnlineStatesAndNotifyNewlyOfflineUploads(
        IReadOnlyList<Upload> uploads,
        DateTime localNow
    )
    {
        foreach (var upload in uploads)
        {
            var previousOnlineState = upload.OnlineState;

            upload.SetOnlineStateAndOfflineSinceTimestamps(
                upload.CalculateOnlineStateFromUploadedFiles(),
                localNow
            );

            CreateFilesOfflineNotificationWhenUploadWentOffline(upload, previousOnlineState);
        }
    }

    private void CreateFilesOfflineNotificationWhenUploadWentOffline(
        Upload upload,
        OnlineState previousOnlineState
    )
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

    private async Task CreateCheckFailedNotificationWhenLastOnlineCheckIsTooOldAsync(
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
}

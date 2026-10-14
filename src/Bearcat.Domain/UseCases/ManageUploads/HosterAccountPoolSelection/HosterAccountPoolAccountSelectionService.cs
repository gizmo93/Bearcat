using System.Globalization;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Proxies;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Bearcat.Domain.ValueObjects;
using Humanizer;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageUploads.HosterAccountPoolSelection;

public class HosterAccountPoolAccountSelectionService(
    IUploadFilesRepository repository,
    IHosterFactory hosterFactory,
    TimeProvider timeProvider,
    INotificationService notificationService,
    HosterCredentialsRejectionService credentialsRejectionService,
    HosterCaptchaVerificationService captchaVerificationService,
    HosterStorageAvailabilityCache storageAvailabilityCache,
    ILogger<HosterAccountPoolAccountSelectionService> logger
)
{
    public TimeSpan StorageQuotaCacheDuration { get; set; } = TimeSpan.FromMinutes(5);

    public async Task<HosterRegistration?> SelectHosterRegistrationAsync(
        Upload upload,
        long requiredBytes,
        IReadOnlyDictionary<int, string> hosterConfigsByRegistrationId,
        IReadOnlyDictionary<int, long> remainingBytesOfRunningUploadsByHosterRegistrationId,
        CancellationToken cancellationToken
    )
    {
        var hosterAccountPool = upload.UploadConfig.HosterAccountPool!;

        var candidateRegistrations = hosterAccountPool
            .Members.OrderBy(member => member.Priority)
            .Select(member => member.HosterRegistration)
            .Where(registration =>
                registration.IsUsableForUploads()
                && hosterConfigsByRegistrationId.ContainsKey(registration.Id)
            )
            .ToList();

        if (candidateRegistrations.Count == 0)
        {
            await ReportNoUsableAccountAsync(
                upload: upload,
                hosterAccountPool: hosterAccountPool,
                cancellationToken: cancellationToken
            );

            return null;
        }

        foreach (var registration in candidateRegistrations)
        {
            var storageAvailability = await GetStorageAvailabilityAsync(
                registration: registration,
                hosterAccountPool: hosterAccountPool,
                serializedHosterConfig: hosterConfigsByRegistrationId[registration.Id],
                reservedBytes: remainingBytesOfRunningUploadsByHosterRegistrationId.GetValueOrDefault(
                    registration.Id
                ),
                cancellationToken: cancellationToken
            );

            if (
                storageAvailability is null
                || !storageAvailability.CanStore(
                    requiredBytes + hosterAccountPool.MinimumFreeSpaceBytes
                )
            )
            {
                continue;
            }

            storageAvailabilityCache.ReduceAvailableBytes(registration.Id, requiredBytes);

            logger.LogInformation(
                "Assigned upload {UploadId} to account {HosterRegistration} of hoster account pool {HosterAccountPool}",
                upload.Id,
                registration.Name,
                hosterAccountPool.Name
            );

            return registration;
        }

        await ReportNoAccountHasEnoughFreeStorageAsync(
            upload: upload,
            hosterAccountPool: hosterAccountPool,
            requiredBytes: requiredBytes,
            cancellationToken: cancellationToken
        );

        return null;
    }

    private async Task<HosterStorageAvailability?> GetStorageAvailabilityAsync(
        HosterRegistration registration,
        HosterAccountPool hosterAccountPool,
        string serializedHosterConfig,
        long reservedBytes,
        CancellationToken cancellationToken
    )
    {
        var now = timeProvider.GetLocalNow();
        var cachedAvailability = storageAvailabilityCache.GetUnexpired(registration.Id, now);

        if (cachedAvailability is not null)
        {
            return cachedAvailability;
        }

        var expiresAt = now.Add(StorageQuotaCacheDuration);
        HosterStorageAvailability storageAvailability;

        try
        {
            var storageQuota = await GetStorageQuotaAsync(
                registration,
                serializedHosterConfig,
                cancellationToken
            );

            storageAvailability = HosterStorageAvailability.FromQuota(
                storageQuota,
                reservedBytes,
                expiresAt
            );
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
        catch (CaptchaVerificationRequiredException ex)
        {
            await captchaVerificationService.MarkRequiredAsync(
                registration,
                ex.Message,
                cancellationToken
            );

            return null;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                ex,
                "Failed to get the storage quota of account {HosterRegistration} of hoster account pool {HosterAccountPool}, skipping the account for {SkipDuration}",
                registration.Name,
                hosterAccountPool.Name,
                StorageQuotaCacheDuration
            );

            storageAvailability = HosterStorageAvailability.ForFailedQuotaRequest(expiresAt);
        }

        storageAvailabilityCache.Set(registration.Id, storageAvailability);

        return storageAvailability;
    }

    private async Task<HosterStorageQuota> GetStorageQuotaAsync(
        HosterRegistration registration,
        string serializedHosterConfig,
        CancellationToken cancellationToken
    )
    {
        var hoster = (IHosterWithStorageQuota)hosterFactory.GetByName(registration.HosterClassName);
        var hosterConfig = hoster.DeserializeHosterConfig(serializedHosterConfig);

        using var proxyScope = HosterRegistrationProxyScope.EnterForUpload(registration);

        return await hoster.GetStorageQuotaAsync(hosterConfig, cancellationToken);
    }

    private async Task ReportNoUsableAccountAsync(
        Upload upload,
        HosterAccountPool hosterAccountPool,
        CancellationToken cancellationToken
    )
    {
        var hasUnresolvedNotification = await repository.HasUnresolvedNotificationForUploadAsync(
            upload.Id,
            NotificationKind.HosterAccountPoolWithoutUsableAccount,
            cancellationToken
        );

        if (hasUnresolvedNotification)
        {
            logger.LogDebug(
                "Upload {UploadId} stays pending because hoster account pool {HosterAccountPool} has no usable account",
                upload.Id,
                hosterAccountPool.Name
            );

            return;
        }

        logger.LogInformation(
            "Upload {UploadId} stays pending because hoster account pool {HosterAccountPool} has no usable account",
            upload.Id,
            hosterAccountPool.Name
        );

        notificationService.Create(
            kind: NotificationKind.HosterAccountPoolWithoutUsableAccount,
            message: $"Upload {upload.Id} of release '{upload.UploadConfig.Release.Name}' could not start: account pool '{hosterAccountPool.Name}' has no usable account (all accounts are inactive, need captcha verification or have unreadable secrets, or the pool has no accounts). The upload stays pending.",
            entity: upload,
            selector: n => n.Upload
        );
    }

    private async Task ReportNoAccountHasEnoughFreeStorageAsync(
        Upload upload,
        HosterAccountPool hosterAccountPool,
        long requiredBytes,
        CancellationToken cancellationToken
    )
    {
        var requiredSize = FormatBytes(requiredBytes);

        var hasUnresolvedNotification = await repository.HasUnresolvedNotificationForUploadAsync(
            upload.Id,
            NotificationKind.HosterAccountPoolFull,
            cancellationToken
        );

        if (hasUnresolvedNotification)
        {
            logger.LogDebug(
                "Upload {UploadId} stays pending because no account of hoster account pool {HosterAccountPool} has enough free storage for {RequiredSize}",
                upload.Id,
                hosterAccountPool.Name,
                requiredSize
            );

            return;
        }

        logger.LogInformation(
            "Upload {UploadId} stays pending because no account of hoster account pool {HosterAccountPool} has enough free storage for {RequiredSize}",
            upload.Id,
            hosterAccountPool.Name,
            requiredSize
        );

        notificationService.Create(
            kind: NotificationKind.HosterAccountPoolFull,
            message: $"Upload {upload.Id} of release '{upload.UploadConfig.Release.Name}' ({requiredSize}) could not start: no account of hoster account pool '{hosterAccountPool.Name}' has enough free storage for it plus the minimum free space of {FormatBytes(hosterAccountPool.MinimumFreeSpaceBytes)}. The upload stays pending.",
            entity: upload,
            selector: n => n.Upload
        );
    }

    private static string FormatBytes(long bytes)
    {
        return bytes.Bytes().Humanize("0.0", CultureInfo.InvariantCulture);
    }
}

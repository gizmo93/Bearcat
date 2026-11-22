using Bearcat.Abstractions.ImageHoster;
using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Proxies;
using Bearcat.Domain.UseCases.ManageImageUploads.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageImageUploads.OnlineChecks;

public class ImageUploadOnlineCheckService(
    IImageUploadRepository repository,
    IImageHosterFactory imageHosterFactory,
    TimeProvider timeProvider,
    INotificationService notificationService,
    ILogger<ImageUploadOnlineCheckService> logger
)
{
    public static readonly TimeSpan MinimumCheckInterval = TimeSpan.FromHours(6);

    public const int MaximumImageUploadsToCheckPerRegistrationPerRun = 200;

    public async Task CheckOnlineStateOfLeastRecentlyCheckedImageUploadsAsync(
        CancellationToken cancellationToken
    )
    {
        var localNow = timeProvider.GetLocalNow();

        var configsByRegistrationId = await repository.GetConfigByImageHosterRegistrationIdAsync(
            cancellationToken
        );

        var imageHostersByClassName = imageHosterFactory.GetByClassName();

        foreach (var (registrationId, serializedConfig) in configsByRegistrationId)
        {
            var imageUploads = await repository.GetLeastRecentlyCheckedImageUploadsAsync(
                imageHosterRegistrationId: registrationId,
                checkedBefore: localNow - MinimumCheckInterval,
                maximumImageUploadCount: MaximumImageUploadsToCheckPerRegistrationPerRun,
                cancellationToken: cancellationToken
            );

            if (imageUploads.Count == 0)
            {
                continue;
            }

            var registration = imageUploads.First().ImageUploadConfig.ImageHosterRegistration;
            var imageHoster = imageHostersByClassName[registration.ImageHosterClassName];

            if (imageHoster is not ISupportsImageOnlineCheck imageOnlineCheck)
            {
                continue;
            }

            using var proxyScope = RegistrationProxyScope.EnterForImageHoster(registration);
            var imageHosterConfig = imageHoster.DeserializeConfig(serializedConfig);

            var newlyOfflineScreenshotUploads = new List<ImageUpload>();

            foreach (var imageUpload in imageUploads)
            {
                var screenshotWentOffline =
                    await CheckImageUploadAndReturnIfScreenshotWentOfflineAsync(
                        imageUpload: imageUpload,
                        imageOnlineCheck: imageOnlineCheck,
                        imageHosterConfig: imageHosterConfig,
                        checkedAt: localNow,
                        cancellationToken: cancellationToken
                    );

                if (screenshotWentOffline)
                {
                    newlyOfflineScreenshotUploads.Add(imageUpload);
                }
            }

            CreateImagesOfflineNotificationPerRelease(newlyOfflineScreenshotUploads, registration);

            await repository.SaveChangesAsync(cancellationToken);
        }
    }

    private void CreateImagesOfflineNotificationPerRelease(
        List<ImageUpload> newlyOfflineScreenshotUploads,
        ImageHosterRegistration registration
    )
    {
        var newlyOfflineScreenshotUploadsByRelease = newlyOfflineScreenshotUploads.GroupBy(
            imageUpload => imageUpload.ImageUploadConfig.Release!
        );

        foreach (var releaseUploads in newlyOfflineScreenshotUploadsByRelease)
        {
            var offlineCount = releaseUploads.Count();

            var offlineText =
                offlineCount == 1
                    ? "1 screenshot image is offline"
                    : $"{offlineCount} screenshot images are offline";

            notificationService.Create(
                kind: NotificationKind.ImagesOffline,
                message: $"{offlineText} on image hoster '{registration.Name}'. Regenerate the screenshots to upload them again.",
                entity: releaseUploads.Key,
                selector: notification => notification.Release
            );
        }
    }

    private async Task<bool> CheckImageUploadAndReturnIfScreenshotWentOfflineAsync(
        ImageUpload imageUpload,
        ISupportsImageOnlineCheck imageOnlineCheck,
        IImageHosterConfig imageHosterConfig,
        DateTime checkedAt,
        CancellationToken cancellationToken
    )
    {
        var imageUrls = imageUpload
            .ImageUrls.Select(url => new ImageUrl(url.ImageSize, url.Url))
            .ToList();

        try
        {
            var imageExists = await imageOnlineCheck.CheckImageExistsAsync(
                imageUrls: imageUrls,
                imageHosterConfig: imageHosterConfig,
                cancellationToken: cancellationToken
            );

            if (imageExists)
            {
                imageUpload.SetOnlineStateAndCheckedAt(OnlineState.Online, checkedAt);
                return false;
            }

            if (imageUpload.ReleaseScreenshotId is null)
            {
                imageUpload.ResetToPendingForReupload();

                logger.LogInformation(
                    "Cover image upload {ImageUploadId} is offline and is uploaded again",
                    imageUpload.Id
                );

                return false;
            }

            var previousOnlineState = imageUpload.OnlineState;
            imageUpload.SetOnlineStateAndCheckedAt(OnlineState.Offline, checkedAt);

            if (previousOnlineState == OnlineState.Offline)
            {
                return false;
            }

            logger.LogInformation(
                "Screenshot image upload {ImageUploadId} is offline",
                imageUpload.Id
            );

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Online check of image upload {ImageUploadId} failed: {ErrorMessage}",
                imageUpload.Id,
                ex.Message
            );

            imageUpload.OnlineCheckedAt = checkedAt;

            return false;
        }
    }
}

using Bearcat.Abstractions.ImageHoster;
using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.Domain.Entities;
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

            foreach (var imageUpload in imageUploads)
            {
                await CheckImageUploadAsync(
                    imageUpload: imageUpload,
                    imageOnlineCheck: imageOnlineCheck,
                    imageHosterConfig: imageHosterConfig,
                    checkedAt: localNow,
                    cancellationToken: cancellationToken
                );
            }

            await repository.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task CheckImageUploadAsync(
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
                return;
            }

            var previousOnlineState = imageUpload.OnlineState;
            imageUpload.SetOnlineStateAndCheckedAt(OnlineState.Offline, checkedAt);

            if (previousOnlineState != OnlineState.Offline)
            {
                logger.LogInformation("Image upload {ImageUploadId} is offline", imageUpload.Id);
            }
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
        }
    }
}

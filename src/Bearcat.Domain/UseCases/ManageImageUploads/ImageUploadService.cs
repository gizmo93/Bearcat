using Bearcat.Abstractions.ImageHoster;
using Bearcat.Abstractions.ImageHoster.Dto;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageImageUploads.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageImageUploads;

public class ImageUploadService(
    IImageUploadRepository repository,
    IImageHosterFactory imageHosterFactory,
    TimeProvider timeProvider,
    ILogger<ImageUploadService> logger
)
{
    public async Task ProcessAsync(CancellationToken cancellationToken = default)
    {
        await repository.CreateMissingImageUploadsAsync(
            createdAt: timeProvider.GetLocalNow(),
            cancellationToken: cancellationToken
        );

        await ProcessPendingImageUploadsAsync(cancellationToken);
    }

    private async Task ProcessPendingImageUploadsAsync(CancellationToken cancellationToken)
    {
        var pendingUploads = await repository.GetPendingImageUploadsAsync(cancellationToken);

        if (pendingUploads.Count == 0)
        {
            return;
        }

        var configsByRegistrationId = await repository.GetConfigByImageHosterRegistrationIdAsync(
            cancellationToken
        );

        var imageHostersByClassName = imageHosterFactory.GetByClassName();

        foreach (var imageUpload in pendingUploads)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await ProcessImageUploadAsync(
                imageUpload: imageUpload,
                configsByRegistrationId: configsByRegistrationId,
                imageHostersByClassName: imageHostersByClassName,
                cancellationToken: cancellationToken
            );

            await repository.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task ProcessImageUploadAsync(
        ImageUpload imageUpload,
        IReadOnlyDictionary<int, string> configsByRegistrationId,
        IReadOnlyDictionary<string, IImageHoster> imageHostersByClassName,
        CancellationToken cancellationToken
    )
    {
        var imageUploadConfig = imageUpload.ImageUploadConfig;
        var registration = imageUploadConfig.ImageHosterRegistration;

        var (coverUrl, imageName) = imageUploadConfig switch
        {
            { Release: { } release } => (release.ReleaseInfo?.CoverUrl, release.Name),
            { ReleaseCollection: { } collection } => (
                collection.Metadata?.CoverUrl,
                collection.Name
            ),
            _ => (null, string.Empty),
        };

        if (string.IsNullOrWhiteSpace(coverUrl))
        {
            imageUpload.UploadState = UploadState.Failed;
            imageUpload.ErrorMessages = ["Image upload source has no cover URL."];
            return;
        }

        imageUpload.UploadState = UploadState.Uploading;
        await repository.SaveChangesAsync(cancellationToken);

        try
        {
            var imageHoster = imageHostersByClassName[registration.ImageHosterClassName];

            var config = imageHoster.DeserializeConfig(configsByRegistrationId[registration.Id]);

            var result = await imageHoster.UploadImageAsync(
                image: new ImageToUploadDto(
                    Source: coverUrl,
                    SourceType: ImageUploadSource.Url,
                    Name: imageName
                ),
                imageHosterConfig: config,
                cancellationToken: cancellationToken
            );

            if (!result.IsSuccess)
            {
                imageUpload.UploadState = UploadState.Failed;
                imageUpload.ErrorMessages = result.ErrorMessages.ToList();
                return;
            }

            imageUpload.UploadState = UploadState.Completed;
            imageUpload.UploadedAt = timeProvider.GetLocalNow();
            imageUpload.ImageUrls = result
                .ImageUrls.Select(url => new ImageUploadUrl { ImageSize = url.Size, Url = url.Url })
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Image upload {ImageUploadId} failed", imageUpload.Id);
            imageUpload.UploadState = UploadState.Failed;
            imageUpload.ErrorMessages = [ex.InnerException?.Message ?? ex.Message];
        }
    }
}

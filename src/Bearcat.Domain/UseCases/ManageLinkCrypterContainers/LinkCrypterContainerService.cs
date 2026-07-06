using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageLinkCrypterContainers.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageLinkCrypterContainers;

public class LinkCrypterContainerService(
    ILinkCrypterContainerCreationWriteRepository repository,
    ILogger<LinkCrypterContainerService> logger,
    ILinkCrypterFactory linkCrypterFactory,
    TimeProvider timeProvider,
    INotificationService notificationService,
    ISecretProtector secretProtector,
    CollectionLinkCrypterContainerService collectionContainerService
)
{
    public async Task CreateMissingLinkCrypterContainersAsync(CancellationToken cancellationToken)
    {
        var uploadsToProcess = await repository.GetUploadsWithMissingLinkCrypterContainersAsync(
            cancellationToken
        );

        if (uploadsToProcess.Count == 0)
        {
            logger.LogInformation(
                "No uploads found with missing link crypter containers, finishing"
            );
            return;
        }

        var processedCollectionSlots = new HashSet<int>();

        foreach (var upload in uploadsToProcess)
        {
            logger.LogInformation(
                "Processing upload {UploadId} for missing link crypter containers",
                upload.Id
            );
            await TriggerCollectionContainerUpdatesAsync(
                upload,
                processedCollectionSlots,
                cancellationToken
            );
            await CreateMissingReleaseContainersAsync(upload, cancellationToken);
        }

        logger.LogInformation("Finished processing link crypter container creation for uploads");
    }

    private async Task TriggerCollectionContainerUpdatesAsync(
        Upload upload,
        HashSet<int> processedCollectionSlots,
        CancellationToken cancellationToken
    )
    {
        if (upload.UploadConfig.CollectionUploadSlotId is not { } slotId)
        {
            return;
        }

        var hasCollectionScopedCrypters = upload.UploadConfig.LinkCrypters.Any(l =>
            l
                is {
                    ContainerScope: LinkCrypterContainerScope.ReleaseCollection,
                    LinkCrypterRegistration.IsActive: true
                }
        );

        if (hasCollectionScopedCrypters && processedCollectionSlots.Add(slotId))
        {
            await collectionContainerService.UpdateContainersAsync(slotId, cancellationToken);
        }
    }

    public async Task DeleteFailedContainerAsync(
        int containerId,
        CancellationToken cancellationToken
    )
    {
        var container = await repository.GetByIdAsync(containerId, cancellationToken);

        if (container is null)
        {
            throw new InvalidOperationException(
                $"Link crypter container {containerId} was not found."
            );
        }

        if (container.State != LinkCrypterContainerState.CreationFailed)
        {
            throw new InvalidOperationException(
                "Only failed link crypter containers can be deleted."
            );
        }

        repository.Remove(container);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task CreateMissingReleaseContainersAsync(
        Upload upload,
        CancellationToken cancellationToken
    )
    {
        var missingReleaseConfigs = upload
            .UploadConfig.LinkCrypters.Where(l =>
                l.LinkCrypterRegistration.IsActive
                && l.ContainerScope == LinkCrypterContainerScope.Release
                && !upload
                    .LinkCrypterContainers.Select(c => c.UploadConfigLinkCrypterId)
                    .Contains(l.Id)
            )
            .ToList();

        foreach (var linkCrypterConfig in missingReleaseConfigs)
        {
            var previousContainer = FindPreviousContainer(upload, linkCrypterConfig);

            if (previousContainer is not null)
            {
                try
                {
                    await UpdateReleaseContainerAsync(
                        upload,
                        previousContainer,
                        linkCrypterConfig,
                        cancellationToken
                    );
                    continue;
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Failed to update link crypter container for upload {UploadId} using link crypter config Id {LinkCrypterId}. Falling back to creating a new container",
                        upload.Id,
                        linkCrypterConfig.Id
                    );
                }
            }

            await CreateReleaseContainerAsync(upload, linkCrypterConfig, cancellationToken);
        }
    }

    private static LinkCrypterContainer? FindPreviousContainer(
        Upload upload,
        UploadConfigLinkCrypter linkCrypterConfig
    )
    {
        return upload
            .UploadConfig.Uploads.Where(u => u.Id < upload.Id)
            .SelectMany(u => u.LinkCrypterContainers)
            .Where(l =>
                l.UploadConfigLinkCrypterId == linkCrypterConfig.Id
                && l.State == LinkCrypterContainerState.Created
            )
            .OrderByDescending(l => l.UploadId)
            .ThenByDescending(l => l.Id)
            .FirstOrDefault();
    }

    private async Task UpdateReleaseContainerAsync(
        Upload upload,
        LinkCrypterContainer previousContainer,
        UploadConfigLinkCrypter linkCrypterConfig,
        CancellationToken cancellationToken
    )
    {
        var crypter = linkCrypterFactory.Get(
            className: linkCrypterConfig.LinkCrypterRegistration.LinkCrypterClassName
        );
        var config = crypter.DeserializeConfig(
            serializedConfig: secretProtector.Unprotect(
                linkCrypterConfig.LinkCrypterRegistration.SerializedConfig
            )
        );

        var result = await crypter.UpdateContainerAsync(
            linkCrypterConfig: config,
            containerLink: previousContainer.ContainerUrl,
            externalReference: previousContainer.ExternalReference,
            password: linkCrypterConfig.Password,
            enableCaptcha: linkCrypterConfig.EnableCaptcha,
            enableContainerDownload: linkCrypterConfig.EnableContainerDownload,
            enableClickAndLoad: linkCrypterConfig.EnableClickAndLoad,
            links: upload
                .UploadedFiles.Select(selector: uf => uf.HosterFileLink)
                .OrderBy(keySelector: l => l)
                .ToList(),
            cancellationToken: cancellationToken
        );

        if (!result.IsSuccess)
        {
            previousContainer.Password = linkCrypterConfig.Password;
            previousContainer.EnableCaptcha = linkCrypterConfig.EnableCaptcha;
            previousContainer.EnableContainerDownload = linkCrypterConfig.EnableContainerDownload;
            previousContainer.EnableClickAndLoad = linkCrypterConfig.EnableClickAndLoad;
            previousContainer.Errors = string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? []
                : [result.ErrorMessage];
            previousContainer.State = LinkCrypterContainerState.CreationFailed;
            notificationService.CreateError(
                message: $"Failed to update link crypter container for upload {upload.Id} using link crypter config Id {linkCrypterConfig.Id} with crypter {linkCrypterConfig.LinkCrypterRegistration.Name}. Errors: {result.ErrorMessage}",
                entity: previousContainer,
                selector: n => n.LinkCrypterContainer
            );
            await repository.SaveChangesAsync(cancellationToken);

            return;
        }

        previousContainer.Upload = upload;
        previousContainer.UploadId = upload.Id;
        previousContainer.Password = linkCrypterConfig.Password;
        previousContainer.EnableCaptcha = linkCrypterConfig.EnableCaptcha;
        previousContainer.EnableContainerDownload = linkCrypterConfig.EnableContainerDownload;
        previousContainer.EnableClickAndLoad = linkCrypterConfig.EnableClickAndLoad;
        previousContainer.StatusImageId = result.StatusImageId ?? previousContainer.StatusImageId;
        previousContainer.Errors = [];
        previousContainer.State = LinkCrypterContainerState.Created;
        await repository.SaveChangesAsync(cancellationToken: cancellationToken);
    }

    private async Task CreateReleaseContainerAsync(
        Upload upload,
        UploadConfigLinkCrypter linkCrypterConfig,
        CancellationToken cancellationToken
    )
    {
        var crypter = linkCrypterFactory.Get(
            linkCrypterConfig.LinkCrypterRegistration.LinkCrypterClassName
        );
        var config = crypter.DeserializeConfig(
            secretProtector.Unprotect(linkCrypterConfig.LinkCrypterRegistration.SerializedConfig)
        );

        var fileUrls = upload.UploadedFiles.Select(f => f.HosterFileLink).OrderBy(l => l).ToList();

        var result = await crypter.CreateContainerAsync(
            linkCrypterConfig: config,
            containerName: upload.UploadConfig.Release.Name,
            password: linkCrypterConfig.Password,
            enableCaptcha: linkCrypterConfig.EnableCaptcha,
            enableContainerDownload: linkCrypterConfig.EnableContainerDownload,
            enableClickAndLoad: linkCrypterConfig.EnableClickAndLoad,
            links: fileUrls,
            cancellationToken: cancellationToken
        );

        var container = new LinkCrypterContainer
        {
            Scope = LinkCrypterContainerScope.Release,
            UploadConfigLinkCrypter = linkCrypterConfig,
            LinkCrypterRegistrationId = linkCrypterConfig.LinkCrypterRegistrationId,
            Upload = upload,
            ExternalReference = result.ExternalReference,
            StatusImageId = result.StatusImageId,
            ContainerUrl = result.ContainerLink ?? string.Empty,
            Password = linkCrypterConfig.Password,
            EnableCaptcha = linkCrypterConfig.EnableCaptcha,
            EnableContainerDownload = linkCrypterConfig.EnableContainerDownload,
            EnableClickAndLoad = linkCrypterConfig.EnableClickAndLoad,
            Errors = result.ErrorMessages.ToList(),
            CreatedAt = timeProvider.GetLocalNow(),
            State = result.IsSuccess
                ? LinkCrypterContainerState.Created
                : LinkCrypterContainerState.CreationFailed,
        };

        if (result.IsSuccess)
        {
            logger.LogInformation(
                "Successfully created link crypter container for upload {UploadId} using link crypter {LinkCrypterId}",
                upload.Id,
                linkCrypterConfig.Id
            );
        }
        else
        {
            logger.LogError(
                "Failed to create link crypter container for upload {UploadId} using link crypter {LinkCrypterId}. Errors: {Errors}",
                upload.Id,
                linkCrypterConfig.Id,
                string.Join("; ", result.ErrorMessages)
            );

            notificationService.CreateError(
                message: $"Failed to create link crypter container for upload {upload.Id} using link crypter {linkCrypterConfig.Id}. Errors: {string.Join("; ", result.ErrorMessages)}",
                entity: container,
                selector: n => n.LinkCrypterContainer
            );
        }

        repository.Add(container);
        await repository.SaveChangesAsync(cancellationToken);
    }
}

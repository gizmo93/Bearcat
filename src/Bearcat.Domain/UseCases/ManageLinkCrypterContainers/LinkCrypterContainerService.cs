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
    ISecretProtector secretProtector
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

        foreach (var upload in uploadsToProcess)
        {
            logger.LogInformation(
                "Processing upload {UploadId} for missing link crypter containers",
                upload.Id
            );
            await ProcessUploadAsync(upload, cancellationToken);
        }

        logger.LogInformation("Finished processing link crypter container creation for uploads");
    }

    private async Task ProcessUploadAsync(Upload upload, CancellationToken cancellationToken)
    {
        var missingConfigs = upload
            .UploadConfig.LinkCrypters.Where(l =>
                l.LinkCrypterRegistration.IsActive
                && (
                    (
                        l.ContainerScope == LinkCrypterContainerScope.Release
                        && !upload
                            .LinkCrypterContainers.Select(c => c.UploadConfigLinkCrypterId)
                            .Contains(l.Id)
                    )
                    || (
                        l.ContainerScope == LinkCrypterContainerScope.ReleaseCollection
                        && upload.UploadConfig.CollectionUploadSlotId is not null
                    )
                )
            )
            .ToList();

        foreach (var linkCrypterConfig in missingConfigs)
        {
            if (linkCrypterConfig.ContainerScope is LinkCrypterContainerScope.ReleaseCollection)
            {
                await ProcessCollectionUploadAsync(upload, linkCrypterConfig, cancellationToken);
                continue;
            }

            var previousContainer = upload
                .UploadConfig.Uploads.Where(u =>
                    u.Id < upload.Id
                    && u.LinkCrypterContainers.Any(l =>
                        l.UploadConfigLinkCrypterId == linkCrypterConfig.Id
                    )
                )
                .Select(u =>
                    u.LinkCrypterContainers.First(l =>
                        l.UploadConfigLinkCrypterId == linkCrypterConfig.Id
                    )
                )
                .FirstOrDefault();

            if (previousContainer is not null)
            {
                try
                {
                    await UpdateLinkCrypterContainerAsync(
                        upload: upload,
                        previousContainer: previousContainer,
                        linkCrypterConfig: linkCrypterConfig,
                        cancellationToken: cancellationToken
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

            await CreateLinkCrypterContainerAsync(
                upload: upload,
                linkCrypterConfig: linkCrypterConfig,
                cancellationToken: cancellationToken
            );
        }
    }

    private async Task ProcessCollectionUploadAsync(
        Upload upload,
        UploadConfigLinkCrypter linkCrypterConfig,
        CancellationToken cancellationToken
    )
    {
        if (upload.UploadConfig.CollectionUploadSlotId is null)
        {
            return;
        }

        var container = await repository.GetCollectionContainerAsync(
            upload.UploadConfig.CollectionUploadSlotId.Value,
            linkCrypterConfig.LinkCrypterRegistrationId,
            cancellationToken
        );

        if (container?.SourceUploads.Any(source => source.UploadId == upload.Id) is true)
        {
            return;
        }

        var collectionUploads = await repository.GetCompletedOnlineUploadsByCollectionSlotAsync(
            upload.UploadConfig.CollectionUploadSlotId.Value,
            linkCrypterConfig.LinkCrypterRegistrationId,
            cancellationToken
        );

        if (collectionUploads.Count == 0)
        {
            return;
        }

        var collectionLinkCrypterConfigs = collectionUploads
            .SelectMany(collectionUpload =>
                collectionUpload.UploadConfig.LinkCrypters.Where(config =>
                    config.ContainerScope == LinkCrypterContainerScope.ReleaseCollection
                    && config.LinkCrypterRegistrationId == linkCrypterConfig.LinkCrypterRegistrationId
                    && config.LinkCrypterRegistration.IsActive
                )
            )
            .ToList();

        if (!HaveSameContainerSettings(collectionLinkCrypterConfigs))
        {
            logger.LogWarning(
                "Skipping collection link crypter container for upload slot {CollectionUploadSlotId} and link crypter registration {LinkCrypterRegistrationId} because settings differ across upload configs",
                upload.UploadConfig.CollectionUploadSlotId.Value,
                linkCrypterConfig.LinkCrypterRegistrationId
            );
            return;
        }

        if (container is null)
        {
            await CreateCollectionLinkCrypterContainerAsync(
                collectionUploads,
                collectionLinkCrypterConfigs[0],
                cancellationToken
            );
            return;
        }

        await UpdateCollectionLinkCrypterContainerAsync(
            container,
            collectionUploads,
            collectionLinkCrypterConfigs[0],
            cancellationToken
        );
    }

    private async Task UpdateLinkCrypterContainerAsync(
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
            notificationService.CreateError(
                message: $"Failed to update link crypter container for upload {upload.Id} using link crypter config Id {linkCrypterConfig.Id} with crypter {linkCrypterConfig.LinkCrypterRegistration.Name}. Errors: {result.ErrorMessage}",
                entity: previousContainer,
                selector: n => n.LinkCrypterContainer
            );

            return;
        }

        upload.LinkCrypterContainers.Add(item: previousContainer);
        await repository.SaveChangesAsync(cancellationToken: cancellationToken);
    }

    private async Task UpdateCollectionLinkCrypterContainerAsync(
        LinkCrypterContainer container,
        IReadOnlyList<Upload> uploads,
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

        var links = GetUploadLinks(uploads);
        var result = await crypter.UpdateContainerAsync(
            linkCrypterConfig: config,
            containerLink: container.ContainerUrl,
            externalReference: container.ExternalReference,
            password: linkCrypterConfig.Password,
            enableCaptcha: linkCrypterConfig.EnableCaptcha,
            enableContainerDownload: linkCrypterConfig.EnableContainerDownload,
            enableClickAndLoad: linkCrypterConfig.EnableClickAndLoad,
            links: links,
            cancellationToken: cancellationToken
        );

        container.Password = linkCrypterConfig.Password;
        container.EnableCaptcha = linkCrypterConfig.EnableCaptcha;
        container.EnableContainerDownload = linkCrypterConfig.EnableContainerDownload;
        container.EnableClickAndLoad = linkCrypterConfig.EnableClickAndLoad;
        container.Errors = string.IsNullOrWhiteSpace(result.ErrorMessage)
            ? []
            : [result.ErrorMessage];
        container.State = result.IsSuccess
            ? LinkCrypterContainerState.Created
            : LinkCrypterContainerState.CreationFailed;

        if (!result.IsSuccess)
        {
            notificationService.CreateError(
                message: $"Failed to update collection link crypter container {container.Id} using link crypter config Id {linkCrypterConfig.Id} with crypter {linkCrypterConfig.LinkCrypterRegistration.Name}. Errors: {result.ErrorMessage}",
                entity: container,
                selector: n => n.LinkCrypterContainer
            );

            return;
        }

        SyncSourceUploads(container, uploads);
        await repository.SaveChangesAsync(cancellationToken: cancellationToken);
    }

    private async Task CreateLinkCrypterContainerAsync(
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

    private async Task CreateCollectionLinkCrypterContainerAsync(
        IReadOnlyList<Upload> uploads,
        UploadConfigLinkCrypter linkCrypterConfig,
        CancellationToken cancellationToken
    )
    {
        var firstUpload = uploads[0];
        var slot = firstUpload.UploadConfig.CollectionUploadSlot!;
        var crypter = linkCrypterFactory.Get(
            linkCrypterConfig.LinkCrypterRegistration.LinkCrypterClassName
        );
        var config = crypter.DeserializeConfig(
            secretProtector.Unprotect(linkCrypterConfig.LinkCrypterRegistration.SerializedConfig)
        );

        var result = await crypter.CreateContainerAsync(
            linkCrypterConfig: config,
            containerName: $"{slot.ReleaseCollection.Name} - {slot.Name}",
            password: linkCrypterConfig.Password,
            enableCaptcha: linkCrypterConfig.EnableCaptcha,
            enableContainerDownload: linkCrypterConfig.EnableContainerDownload,
            enableClickAndLoad: linkCrypterConfig.EnableClickAndLoad,
            links: GetUploadLinks(uploads),
            cancellationToken: cancellationToken
        );

        var container = new LinkCrypterContainer
        {
            Scope = LinkCrypterContainerScope.ReleaseCollection,
            CollectionUploadSlotId = slot.Id,
            LinkCrypterRegistrationId = linkCrypterConfig.LinkCrypterRegistrationId,
            ExternalReference = result.ExternalReference,
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

        SyncSourceUploads(container, uploads);

        if (!result.IsSuccess)
        {
            notificationService.CreateError(
                message: $"Failed to create collection link crypter container for upload slot {slot.Id} using link crypter {linkCrypterConfig.Id}. Errors: {string.Join("; ", result.ErrorMessages)}",
                entity: container,
                selector: n => n.LinkCrypterContainer
            );
        }

        repository.Add(container);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private static bool HaveSameContainerSettings(
        IReadOnlyList<UploadConfigLinkCrypter> linkCrypterConfigs
    )
    {
        if (linkCrypterConfigs.Count == 0)
        {
            return false;
        }

        var first = linkCrypterConfigs[0];

        return linkCrypterConfigs.All(config =>
            config.LinkCrypterRegistrationId == first.LinkCrypterRegistrationId
            && string.Equals(config.Password, first.Password, StringComparison.Ordinal)
            && config.EnableCaptcha == first.EnableCaptcha
            && config.EnableContainerDownload == first.EnableContainerDownload
            && config.EnableClickAndLoad == first.EnableClickAndLoad
        );
    }

    private static List<string> GetUploadLinks(IReadOnlyList<Upload> uploads)
    {
        return uploads
            .SelectMany(upload => upload.UploadedFiles.Select(file => file.HosterFileLink))
            .Distinct()
            .OrderBy(link => link)
            .ToList();
    }

    private static void SyncSourceUploads(LinkCrypterContainer container, IReadOnlyList<Upload> uploads)
    {
        var existingUploadIds = container.SourceUploads.Select(source => source.UploadId).ToHashSet();

        foreach (var upload in uploads.Where(upload => !existingUploadIds.Contains(upload.Id)))
        {
            container.SourceUploads.Add(
                new LinkCrypterContainerSourceUpload
                {
                    LinkCrypterContainer = container,
                    UploadId = upload.Id,
                }
            );
        }
    }
}

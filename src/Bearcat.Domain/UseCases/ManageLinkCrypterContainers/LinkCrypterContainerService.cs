using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.LinkCrypter.Results;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Proxies;
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
    private const int UpdateFailedRetryDelayInMinutes = 15;

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

    public async Task RetryFailedReleaseContainerUpdatesAsync(CancellationToken cancellationToken)
    {
        var containersToRetry = await repository.GetReleaseContainersWithUpdateRetryDueAsync(
            updateFailedBefore: timeProvider
                .GetLocalNow()
                .AddMinutes(-UpdateFailedRetryDelayInMinutes),
            cancellationToken: cancellationToken
        );

        foreach (var container in containersToRetry)
        {
            logger.LogInformation(
                "Retrying update of link crypter container {ContainerId} for upload {UploadId}",
                container.Id,
                container.UploadId
            );
            await UpdateReleaseContainerAsync(
                upload: container.Upload!,
                container: container,
                linkCrypterConfig: container.UploadConfigLinkCrypter!,
                cancellationToken: cancellationToken
            );
        }
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
        var container = await GetExistingContainerAsync(containerId, cancellationToken);

        if (container.State != LinkCrypterContainerState.CreationFailed)
        {
            throw new InvalidOperationException(
                "Only failed link crypter containers can be deleted."
            );
        }

        repository.Remove(container);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<LinkCrypterContainer> ForceUpdateContainerAsync(
        int containerId,
        CancellationToken cancellationToken
    )
    {
        var container = await GetExistingContainerAsync(containerId, cancellationToken);

        if (
            container.State
            is not (LinkCrypterContainerState.Created or LinkCrypterContainerState.UpdateFailed)
        )
        {
            throw new InvalidOperationException(
                "Only created link crypter containers or containers whose update failed can be updated."
            );
        }

        if (container.Scope == LinkCrypterContainerScope.ReleaseCollection)
        {
            return await collectionContainerService.ForceUpdateContainerAsync(
                collectionUploadSlotId: container.CollectionUploadSlotId!.Value,
                containerId: container.Id,
                cancellationToken: cancellationToken
            );
        }

        var releaseContainer = await repository.GetReleaseContainerWithUploadAsync(
            containerId,
            cancellationToken
        );

        await UpdateReleaseContainerAsync(
            upload: releaseContainer.Upload!,
            container: releaseContainer,
            linkCrypterConfig: releaseContainer.UploadConfigLinkCrypter!,
            cancellationToken: cancellationToken
        );

        return releaseContainer;
    }

    public async Task<LinkCrypterContainer> RecreateContainerAsync(
        int containerId,
        CancellationToken cancellationToken
    )
    {
        var container = await GetExistingContainerAsync(containerId, cancellationToken);

        if (container.State != LinkCrypterContainerState.UpdateFailed)
        {
            throw new InvalidOperationException(
                "Only link crypter containers whose update failed can be recreated."
            );
        }

        if (container.Scope == LinkCrypterContainerScope.ReleaseCollection)
        {
            return await collectionContainerService.RecreateContainerAsync(
                collectionUploadSlotId: container.CollectionUploadSlotId!.Value,
                containerId: container.Id,
                cancellationToken: cancellationToken
            );
        }

        var releaseContainer = await repository.GetReleaseContainerWithUploadAsync(
            containerId,
            cancellationToken
        );

        var upload = releaseContainer.Upload!;
        var linkCrypterConfig = releaseContainer.UploadConfigLinkCrypter!;

        var result = await CreateNewContainerAndReplaceUrlAsync(
            upload: upload,
            container: releaseContainer,
            linkCrypterConfig: linkCrypterConfig,
            cancellationToken: cancellationToken
        );

        if (!result.IsSuccess)
        {
            var errorMessage = string.Join("; ", result.ErrorMessages);
            releaseContainer.MarkAsUpdateFailed(errorMessage, timeProvider.GetLocalNow());
            logger.LogError(
                "Failed to recreate link crypter container {ContainerId} for upload {UploadId}, keeping the previous container. Errors: {Errors}",
                releaseContainer.Id,
                upload.Id,
                errorMessage
            );
        }

        await repository.SaveChangesAsync(cancellationToken);
        return releaseContainer;
    }

    private async Task<LinkCrypterContainer> GetExistingContainerAsync(
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

        return container;
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

            if (previousContainer is null)
            {
                await CreateReleaseContainerAsync(upload, linkCrypterConfig, cancellationToken);
            }
            else
            {
                await UpdateReleaseContainerAsync(
                    upload: upload,
                    container: previousContainer,
                    linkCrypterConfig: linkCrypterConfig,
                    cancellationToken: cancellationToken
                );
            }
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
                && l.State
                    is LinkCrypterContainerState.Created
                        or LinkCrypterContainerState.UpdateFailed
            )
            .OrderByDescending(l => l.UploadId)
            .ThenByDescending(l => l.Id)
            .FirstOrDefault();
    }

    private async Task UpdateReleaseContainerAsync(
        Upload upload,
        LinkCrypterContainer container,
        UploadConfigLinkCrypter linkCrypterConfig,
        CancellationToken cancellationToken
    )
    {
        container.Upload = upload;
        container.UploadId = upload.Id;

        var result = await UpdateContainerAtLinkCrypterAsync(
            upload: upload,
            container: container,
            linkCrypterConfig: linkCrypterConfig,
            cancellationToken: cancellationToken
        );

        switch (result.Outcome)
        {
            case UpdateContainerOutcome.Success:
                container.CopySettingsFrom(linkCrypterConfig);
                container.MarkAsUpdated(result.StatusImageId);
                logger.LogInformation(
                    "Successfully updated link crypter container {ContainerId} for upload {UploadId}",
                    container.Id,
                    upload.Id
                );
                break;
            case UpdateContainerOutcome.ContainerNotFound:
            {
                var createResult = await CreateNewContainerAndReplaceUrlAsync(
                    upload: upload,
                    container: container,
                    linkCrypterConfig: linkCrypterConfig,
                    cancellationToken: cancellationToken
                );

                if (!createResult.IsSuccess)
                {
                    container.CopySettingsFrom(linkCrypterConfig);
                    container.ApplyCreateContainerResult(createResult, timeProvider.GetLocalNow());
                    NotifyReleaseContainerCreationFailed(
                        upload: upload,
                        container: container,
                        linkCrypterConfig: linkCrypterConfig,
                        result: createResult
                    );
                }

                break;
            }
            case UpdateContainerOutcome.Failed:
                MarkReleaseContainerAsUpdateFailed(
                    upload: upload,
                    container: container,
                    linkCrypterConfig: linkCrypterConfig,
                    errorMessage: result.ErrorMessage
                );
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown update container outcome {result.Outcome}."
                );
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    private void MarkReleaseContainerAsUpdateFailed(
        Upload upload,
        LinkCrypterContainer container,
        UploadConfigLinkCrypter linkCrypterConfig,
        string? errorMessage
    )
    {
        var wasAlreadyUpdateFailed = container.State == LinkCrypterContainerState.UpdateFailed;

        container.MarkAsUpdateFailed(errorMessage, timeProvider.GetLocalNow());

        logger.LogError(
            "Failed to update link crypter container {ContainerId} for upload {UploadId} using link crypter config Id {LinkCrypterId}. Errors: {Errors}",
            container.Id,
            upload.Id,
            linkCrypterConfig.Id,
            errorMessage
        );

        if (wasAlreadyUpdateFailed)
        {
            return;
        }

        notificationService.Create(
            kind: NotificationKind.LinkCrypterContainerUpdateFailed,
            message: $"Failed to update link crypter container for upload {upload.Id} using link crypter config Id {linkCrypterConfig.Id} with crypter {linkCrypterConfig.LinkCrypterRegistration.Name}. The update is retried automatically. Errors: {errorMessage}",
            entity: container,
            selector: n => n.LinkCrypterContainer
        );
    }

    private async Task<CreateContainerResult> CreateNewContainerAndReplaceUrlAsync(
        Upload upload,
        LinkCrypterContainer container,
        UploadConfigLinkCrypter linkCrypterConfig,
        CancellationToken cancellationToken
    )
    {
        var previousContainerUrl = container.ContainerUrl;
        var result = await CreateContainerAtLinkCrypterAsync(
            upload: upload,
            linkCrypterConfig: linkCrypterConfig,
            cancellationToken: cancellationToken
        );

        if (!result.IsSuccess)
        {
            return result;
        }

        container.CopySettingsFrom(linkCrypterConfig);
        container.ApplyCreateContainerResult(result, timeProvider.GetLocalNow());
        upload.UploadConfig.Release.UploadsPostedAt = null;

        logger.LogWarning(
            "Link crypter container {ContainerId} for upload {UploadId} was recreated. Previous URL: {PreviousContainerUrl}, new URL: {ContainerUrl}",
            container.Id,
            upload.Id,
            previousContainerUrl,
            container.ContainerUrl
        );

        notificationService.Create(
            kind: NotificationKind.LinkCrypterContainerRecreated,
            message: $"Link crypter container for upload {upload.Id} with crypter {linkCrypterConfig.LinkCrypterRegistration.Name} was recreated. Previous URL: {previousContainerUrl}, new URL: {container.ContainerUrl}. Existing posts must be updated.",
            entity: container,
            selector: n => n.LinkCrypterContainer
        );

        return result;
    }

    private async Task CreateReleaseContainerAsync(
        Upload upload,
        UploadConfigLinkCrypter linkCrypterConfig,
        CancellationToken cancellationToken
    )
    {
        var result = await CreateContainerAtLinkCrypterAsync(
            upload: upload,
            linkCrypterConfig: linkCrypterConfig,
            cancellationToken: cancellationToken
        );

        var container = new LinkCrypterContainer
        {
            Scope = LinkCrypterContainerScope.Release,
            UploadConfigLinkCrypter = linkCrypterConfig,
            LinkCrypterRegistrationId = linkCrypterConfig.LinkCrypterRegistrationId,
            Upload = upload,
        };
        container.CopySettingsFrom(linkCrypterConfig);
        container.ApplyCreateContainerResult(result, timeProvider.GetLocalNow());

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
            NotifyReleaseContainerCreationFailed(
                upload: upload,
                container: container,
                linkCrypterConfig: linkCrypterConfig,
                result: result
            );
        }

        repository.Add(container);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private void NotifyReleaseContainerCreationFailed(
        Upload upload,
        LinkCrypterContainer container,
        UploadConfigLinkCrypter linkCrypterConfig,
        CreateContainerResult result
    )
    {
        logger.LogError(
            "Failed to create link crypter container for upload {UploadId} using link crypter {LinkCrypterId}. Errors: {Errors}",
            upload.Id,
            linkCrypterConfig.Id,
            string.Join("; ", result.ErrorMessages)
        );

        notificationService.Create(
            kind: NotificationKind.LinkCrypterContainerCreationFailed,
            message: $"Failed to create link crypter container for upload {upload.Id} using link crypter {linkCrypterConfig.Id}. Errors: {string.Join("; ", result.ErrorMessages)}",
            entity: container,
            selector: n => n.LinkCrypterContainer
        );
    }

    private async Task<UpdateContainerResult> UpdateContainerAtLinkCrypterAsync(
        Upload upload,
        LinkCrypterContainer container,
        UploadConfigLinkCrypter linkCrypterConfig,
        CancellationToken cancellationToken
    )
    {
        var crypter = GetCrypter(linkCrypterConfig);
        var config = GetCrypterConfig(crypter, linkCrypterConfig);

        using var proxyScope = LinkCrypterRegistrationProxyScope.Enter(
            linkCrypterConfig.LinkCrypterRegistration
        );

        return await crypter.UpdateContainerAsync(
            linkCrypterConfig: config,
            containerLink: container.ContainerUrl,
            externalReference: container.ExternalReference,
            password: linkCrypterConfig.Password,
            enableCaptcha: linkCrypterConfig.EnableCaptcha,
            enableContainerDownload: linkCrypterConfig.EnableContainerDownload,
            enableClickAndLoad: linkCrypterConfig.EnableClickAndLoad,
            links: GetUploadLinks(upload),
            cancellationToken: cancellationToken
        );
    }

    private async Task<CreateContainerResult> CreateContainerAtLinkCrypterAsync(
        Upload upload,
        UploadConfigLinkCrypter linkCrypterConfig,
        CancellationToken cancellationToken
    )
    {
        var crypter = GetCrypter(linkCrypterConfig);
        var config = GetCrypterConfig(crypter, linkCrypterConfig);

        using var proxyScope = LinkCrypterRegistrationProxyScope.Enter(
            linkCrypterConfig.LinkCrypterRegistration
        );

        return await crypter.CreateContainerAsync(
            linkCrypterConfig: config,
            containerName: upload.UploadConfig.Release.Name,
            password: linkCrypterConfig.Password,
            enableCaptcha: linkCrypterConfig.EnableCaptcha,
            enableContainerDownload: linkCrypterConfig.EnableContainerDownload,
            enableClickAndLoad: linkCrypterConfig.EnableClickAndLoad,
            links: GetUploadLinks(upload),
            cancellationToken: cancellationToken
        );
    }

    private ILinkCrypter GetCrypter(UploadConfigLinkCrypter linkCrypterConfig)
    {
        return linkCrypterFactory.Get(
            linkCrypterConfig.LinkCrypterRegistration.LinkCrypterClassName
        );
    }

    private ILinkCrypterConfig GetCrypterConfig(
        ILinkCrypter crypter,
        UploadConfigLinkCrypter linkCrypterConfig
    )
    {
        return crypter.DeserializeConfig(
            secretProtector.Unprotect(linkCrypterConfig.LinkCrypterRegistration.SerializedConfig)
        );
    }

    private static List<string> GetUploadLinks(Upload upload)
    {
        return upload.UploadedFiles.Select(f => f.HosterFileLink).OrderBy(l => l).ToList();
    }
}

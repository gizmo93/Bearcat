using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.LinkCrypter.Results;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.LinkCrypterContainers;
using Bearcat.Domain.Shared.Proxies;
using Bearcat.Domain.UseCases.ManageLinkCrypterContainers.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageLinkCrypterContainers;

public class CollectionLinkCrypterContainerService(
    ILinkCrypterContainerCreationWriteRepository repository,
    ILogger<CollectionLinkCrypterContainerService> logger,
    ILinkCrypterFactory linkCrypterFactory,
    TimeProvider timeProvider,
    INotificationService notificationService,
    ISecretProtector secretProtector
) : ICollectionLinkCrypterContainerUpdater
{
    private const int UpdateFailedRetryDelayInMinutes = 15;

    public async Task UpdateContainersAsync(
        int collectionUploadSlotId,
        CancellationToken cancellationToken = default
    )
    {
        var slot = await repository.GetCollectionUploadSlotAsync(
            collectionUploadSlotId,
            cancellationToken
        );
        var existingContainers = await repository.GetCollectionContainersAsync(
            collectionUploadSlotId,
            cancellationToken
        );
        var containersByRegistrationId = existingContainers.ToDictionary(c =>
            c.LinkCrypterRegistrationId
        );
        var activeRegistrations = GetActiveCollectionRegistrations(slot);

        await RemoveOrphanedContainersAsync(
            existingContainers,
            activeRegistrations,
            cancellationToken
        );

        foreach (var (registrationId, linkCrypterConfig) in activeRegistrations)
        {
            containersByRegistrationId.TryGetValue(registrationId, out var container);
            await CreateOrUpdateContainerAsync(
                slot: slot,
                existingContainer: container,
                linkCrypterConfig: linkCrypterConfig,
                ignoreUpdateRetryDelay: false,
                cancellationToken: cancellationToken
            );
        }
    }

    public async Task<LinkCrypterContainer> ForceUpdateContainerAsync(
        int collectionUploadSlotId,
        int containerId,
        CancellationToken cancellationToken
    )
    {
        var slot = await repository.GetCollectionUploadSlotAsync(
            collectionUploadSlotId,
            cancellationToken
        );
        var container = await GetCollectionContainerAsync(
            collectionUploadSlotId: collectionUploadSlotId,
            containerId: containerId,
            cancellationToken: cancellationToken
        );
        var linkCrypterConfig = GetActiveCollectionRegistrations(slot)[
            container.LinkCrypterRegistrationId
        ];

        await CreateOrUpdateContainerAsync(
            slot: slot,
            existingContainer: container,
            linkCrypterConfig: linkCrypterConfig,
            ignoreUpdateRetryDelay: true,
            cancellationToken: cancellationToken
        );

        return container;
    }

    public async Task<LinkCrypterContainer> RecreateContainerAsync(
        int collectionUploadSlotId,
        int containerId,
        CancellationToken cancellationToken
    )
    {
        var slot = await repository.GetCollectionUploadSlotAsync(
            collectionUploadSlotId,
            cancellationToken
        );

        var container = await GetCollectionContainerAsync(
            collectionUploadSlotId: collectionUploadSlotId,
            containerId: containerId,
            cancellationToken: cancellationToken
        );

        var linkCrypterConfig = GetActiveCollectionRegistrations(slot)[
            container.LinkCrypterRegistrationId
        ];

        var uploads = await GetUploadsWithValidCollectionSettingsAsync(
            slot: slot,
            existingContainer: container,
            linkCrypterConfig: linkCrypterConfig,
            cancellationToken: cancellationToken
        );

        if (uploads is null)
        {
            return container;
        }

        var result = await CreateNewContainerAndReplaceUrlAsync(
            slot: slot,
            container: container,
            uploads: uploads,
            linkCrypterConfig: linkCrypterConfig,
            cancellationToken: cancellationToken
        );

        if (!result.IsSuccess)
        {
            var errorMessage = string.Join("; ", result.ErrorMessages);
            container.MarkAsUpdateFailed(errorMessage, timeProvider.GetLocalNow());
            logger.LogError(
                "Failed to recreate collection link crypter container {ContainerId} for upload slot {SlotId}, keeping the previous container. Errors: {Errors}",
                container.Id,
                slot.Id,
                errorMessage
            );
        }

        await repository.SaveChangesAsync(cancellationToken);
        return container;
    }

    private async Task<LinkCrypterContainer> GetCollectionContainerAsync(
        int collectionUploadSlotId,
        int containerId,
        CancellationToken cancellationToken
    )
    {
        var containers = await repository.GetCollectionContainersAsync(
            collectionUploadSlotId,
            cancellationToken
        );

        return containers.Single(container => container.Id == containerId);
    }

    private async Task RemoveOrphanedContainersAsync(
        IReadOnlyList<LinkCrypterContainer> containers,
        Dictionary<int, UploadConfigLinkCrypter> activeRegistrations,
        CancellationToken cancellationToken
    )
    {
        var orphaned = containers
            .Where(c => !activeRegistrations.ContainsKey(c.LinkCrypterRegistrationId))
            .ToList();

        if (orphaned.Count == 0)
        {
            return;
        }

        foreach (var container in orphaned)
        {
            repository.Remove(container);
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    private static Dictionary<int, UploadConfigLinkCrypter> GetActiveCollectionRegistrations(
        CollectionUploadSlot slot
    )
    {
        return slot
            .UploadConfigs.SelectMany(uc => uc.LinkCrypters)
            .Where(lc =>
                lc.ContainerScope == LinkCrypterContainerScope.ReleaseCollection
                && lc.LinkCrypterRegistration.IsActive
            )
            .GroupBy(lc => lc.LinkCrypterRegistrationId)
            .ToDictionary(g => g.Key, g => g.OrderBy(item => item.Id).First());
    }

    private async Task CreateOrUpdateContainerAsync(
        CollectionUploadSlot slot,
        LinkCrypterContainer? existingContainer,
        UploadConfigLinkCrypter linkCrypterConfig,
        bool ignoreUpdateRetryDelay,
        CancellationToken cancellationToken
    )
    {
        var uploads = await GetUploadsWithValidCollectionSettingsAsync(
            slot: slot,
            existingContainer: existingContainer,
            linkCrypterConfig: linkCrypterConfig,
            cancellationToken: cancellationToken
        );

        if (uploads is null)
        {
            return;
        }

        if (existingContainer is null)
        {
            await CreateContainerAsync(slot, uploads, linkCrypterConfig, cancellationToken);
            return;
        }

        if (existingContainer.State == LinkCrypterContainerState.CreationFailed)
        {
            await CreateContainerAtLinkCrypterAndApplyResultAsync(
                slot: slot,
                container: existingContainer,
                uploads: uploads,
                linkCrypterConfig: linkCrypterConfig,
                cancellationToken: cancellationToken
            );
            await repository.SaveChangesAsync(cancellationToken);
            return;
        }

        if (!ignoreUpdateRetryDelay && IsWithinUpdateRetryDelay(existingContainer))
        {
            return;
        }

        await UpdateContainerAsync(
            slot: slot,
            container: existingContainer,
            uploads: uploads,
            linkCrypterConfig: linkCrypterConfig,
            cancellationToken: cancellationToken
        );
    }

    private async Task<IReadOnlyList<Upload>?> GetUploadsWithValidCollectionSettingsAsync(
        CollectionUploadSlot slot,
        LinkCrypterContainer? existingContainer,
        UploadConfigLinkCrypter linkCrypterConfig,
        CancellationToken cancellationToken
    )
    {
        var uploads = GetCompletedUploads(slot, linkCrypterConfig.LinkCrypterRegistrationId);

        if (uploads.Count == 0)
        {
            return null;
        }

        var allLinkCrypterConfigs = GetAllConfigsForRegistration(
            slot,
            linkCrypterConfig.LinkCrypterRegistrationId
        );

        var problem = ValidateCollectionSettings(slot, uploads, allLinkCrypterConfigs);

        if (problem is null)
        {
            return uploads;
        }

        logger.LogWarning(
            "Skipping collection container for slot {SlotId}, registration {RegistrationId}: {Reason}",
            slot.Id,
            linkCrypterConfig.LinkCrypterRegistrationId,
            problem
        );

        if (existingContainer is not null)
        {
            await MarkContainerAsFailedAsync(existingContainer, problem, cancellationToken);
        }

        return null;
    }

    private bool IsWithinUpdateRetryDelay(LinkCrypterContainer container)
    {
        return container.State == LinkCrypterContainerState.UpdateFailed
            && container.UpdateFailedAt
                > timeProvider.GetLocalNow().AddMinutes(-UpdateFailedRetryDelayInMinutes);
    }

    private async Task CreateContainerAsync(
        CollectionUploadSlot slot,
        IReadOnlyList<Upload> uploads,
        UploadConfigLinkCrypter linkCrypterConfig,
        CancellationToken cancellationToken
    )
    {
        var container = new LinkCrypterContainer
        {
            Scope = LinkCrypterContainerScope.ReleaseCollection,
            CollectionUploadSlotId = slot.Id,
            LinkCrypterRegistrationId = linkCrypterConfig.LinkCrypterRegistrationId,
        };

        await CreateContainerAtLinkCrypterAndApplyResultAsync(
            slot: slot,
            container: container,
            uploads: uploads,
            linkCrypterConfig: linkCrypterConfig,
            cancellationToken: cancellationToken
        );

        repository.Add(container);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task CreateContainerAtLinkCrypterAndApplyResultAsync(
        CollectionUploadSlot slot,
        LinkCrypterContainer container,
        IReadOnlyList<Upload> uploads,
        UploadConfigLinkCrypter linkCrypterConfig,
        CancellationToken cancellationToken
    )
    {
        var result = await CreateContainerAtLinkCrypterAsync(
            slot: slot,
            uploads: uploads,
            linkCrypterConfig: linkCrypterConfig,
            cancellationToken: cancellationToken
        );

        container.CopySettingsFrom(linkCrypterConfig);
        container.ApplyCreateContainerResult(result, timeProvider.GetLocalNow());
        SyncSourceUploads(container, uploads);

        if (!result.IsSuccess)
        {
            NotifyContainerCreationFailed(
                slot: slot,
                linkCrypterConfig: linkCrypterConfig,
                container: container,
                result: result
            );
        }
    }

    private async Task UpdateContainerAsync(
        CollectionUploadSlot slot,
        LinkCrypterContainer container,
        IReadOnlyList<Upload> uploads,
        UploadConfigLinkCrypter linkCrypterConfig,
        CancellationToken cancellationToken
    )
    {
        var result = await UpdateContainerAtLinkCrypterAsync(
            container: container,
            uploads: uploads,
            linkCrypterConfig: linkCrypterConfig,
            cancellationToken: cancellationToken
        );

        switch (result.Outcome)
        {
            case UpdateContainerOutcome.Success:
                container.CopySettingsFrom(linkCrypterConfig);
                container.MarkAsUpdated(result.StatusImageId);
                SyncSourceUploads(container, uploads);
                break;
            case UpdateContainerOutcome.ContainerNotFound:
            {
                var createResult = await CreateNewContainerAndReplaceUrlAsync(
                    slot: slot,
                    container: container,
                    uploads: uploads,
                    linkCrypterConfig: linkCrypterConfig,
                    cancellationToken: cancellationToken
                );

                if (!createResult.IsSuccess)
                {
                    container.CopySettingsFrom(linkCrypterConfig);
                    container.ApplyCreateContainerResult(createResult, timeProvider.GetLocalNow());

                    NotifyContainerCreationFailed(
                        slot: slot,
                        linkCrypterConfig: linkCrypterConfig,
                        container: container,
                        result: createResult
                    );
                }

                break;
            }
            case UpdateContainerOutcome.Failed:
                MarkContainerAsUpdateFailed(
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

    private void MarkContainerAsUpdateFailed(
        LinkCrypterContainer container,
        UploadConfigLinkCrypter linkCrypterConfig,
        string? errorMessage
    )
    {
        var wasAlreadyUpdateFailed = container.State == LinkCrypterContainerState.UpdateFailed;

        container.MarkAsUpdateFailed(errorMessage, timeProvider.GetLocalNow());

        logger.LogError(
            "Failed to update collection link crypter container {ContainerId} using link crypter config Id {LinkCrypterId}. Errors: {Errors}",
            container.Id,
            linkCrypterConfig.Id,
            errorMessage
        );

        if (wasAlreadyUpdateFailed)
        {
            return;
        }

        notificationService.Create(
            kind: NotificationKind.LinkCrypterContainerUpdateFailed,
            message: $"Failed to update collection link crypter container {container.Id} using link crypter config Id {linkCrypterConfig.Id} with crypter {linkCrypterConfig.LinkCrypterRegistration.Name}. The update is retried automatically. Errors: {errorMessage}",
            entity: container,
            selector: n => n.LinkCrypterContainer
        );
    }

    private async Task<CreateContainerResult> CreateNewContainerAndReplaceUrlAsync(
        CollectionUploadSlot slot,
        LinkCrypterContainer container,
        IReadOnlyList<Upload> uploads,
        UploadConfigLinkCrypter linkCrypterConfig,
        CancellationToken cancellationToken
    )
    {
        var previousContainerUrl = container.ContainerUrl;
        var result = await CreateContainerAtLinkCrypterAsync(
            slot: slot,
            uploads: uploads,
            linkCrypterConfig: linkCrypterConfig,
            cancellationToken: cancellationToken
        );

        if (!result.IsSuccess)
        {
            return result;
        }

        container.CopySettingsFrom(linkCrypterConfig);
        container.ApplyCreateContainerResult(result, timeProvider.GetLocalNow());
        SyncSourceUploads(container, uploads);
        slot.ReleaseCollection.UploadsPostedAt = null;

        logger.LogWarning(
            "Collection link crypter container {ContainerId} for upload slot {SlotId} was recreated. Previous URL: {PreviousContainerUrl}, new URL: {ContainerUrl}",
            container.Id,
            slot.Id,
            previousContainerUrl,
            container.ContainerUrl
        );

        notificationService.Create(
            kind: NotificationKind.LinkCrypterContainerRecreated,
            message: $"Collection link crypter container {container.Id} for upload slot {slot.Id} with crypter {linkCrypterConfig.LinkCrypterRegistration.Name} was recreated. Previous URL: {previousContainerUrl}, new URL: {container.ContainerUrl}. Existing posts must be updated.",
            entity: container,
            selector: n => n.LinkCrypterContainer
        );

        return result;
    }

    private void NotifyContainerCreationFailed(
        CollectionUploadSlot slot,
        UploadConfigLinkCrypter linkCrypterConfig,
        LinkCrypterContainer container,
        CreateContainerResult result
    )
    {
        notificationService.Create(
            kind: NotificationKind.LinkCrypterContainerCreationFailed,
            message: $"Failed to create collection link crypter container for upload slot {slot.Id} using link crypter {linkCrypterConfig.Id}. Errors: {string.Join("; ", result.ErrorMessages)}",
            entity: container,
            selector: n => n.LinkCrypterContainer
        );
    }

    private async Task<CreateContainerResult> CreateContainerAtLinkCrypterAsync(
        CollectionUploadSlot slot,
        IReadOnlyList<Upload> uploads,
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
            containerName: slot.ReleaseCollection.Name,
            password: linkCrypterConfig.Password,
            enableCaptcha: linkCrypterConfig.EnableCaptcha,
            enableContainerDownload: linkCrypterConfig.EnableContainerDownload,
            enableClickAndLoad: linkCrypterConfig.EnableClickAndLoad,
            links: GetUploadLinks(uploads),
            cancellationToken: cancellationToken
        );
    }

    private async Task<UpdateContainerResult> UpdateContainerAtLinkCrypterAsync(
        LinkCrypterContainer container,
        IReadOnlyList<Upload> uploads,
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
            links: GetUploadLinks(uploads),
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

    private async Task MarkContainerAsFailedAsync(
        LinkCrypterContainer container,
        string errorMessage,
        CancellationToken cancellationToken
    )
    {
        var previousState = container.State;

        container.Errors = [errorMessage];
        container.State = container.HasContainerUrl()
            ? LinkCrypterContainerState.UpdateFailed
            : LinkCrypterContainerState.CreationFailed;

        if (container.State != previousState)
        {
            notificationService.Create(
                kind: NotificationKind.CollectionLinkCrypterContainerInvalid,
                message: $"Collection link crypter container {container.Id} is invalid: {errorMessage}",
                entity: container,
                selector: n => n.LinkCrypterContainer
            );
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    private static string? ValidateCollectionSettings(
        CollectionUploadSlot slot,
        IReadOnlyList<Upload> uploads,
        IReadOnlyList<UploadConfigLinkCrypter> linkCrypterConfigs
    )
    {
        if (!HaveSameContainerSettings(linkCrypterConfigs))
        {
            return "Link crypter settings differ across upload configs.";
        }

        return CheckArchivePasswords(slot, uploads);
    }

    private static string? CheckArchivePasswords(
        CollectionUploadSlot slot,
        IReadOnlyList<Upload> uploads
    )
    {
        var passwords = uploads
            .Select(upload => CleanPassword(upload.UploadConfig.ArchiveConfig!.ArchivePassword))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return slot.PasswordPolicy switch
        {
            CollectionUploadSlotPasswordPolicy.Ignore => null,
            CollectionUploadSlotPasswordPolicy.MustMatchAcrossReleases when passwords.Count <= 1 =>
                null,
            CollectionUploadSlotPasswordPolicy.MustMatchAcrossReleases =>
                "Archive passwords differ across releases.",
            CollectionUploadSlotPasswordPolicy.MustEqualExpectedValue
                when passwords.Count == 1
                    && string.Equals(
                        passwords[0],
                        CleanPassword(slot.ExpectedArchivePassword),
                        StringComparison.Ordinal
                    ) => null,
            CollectionUploadSlotPasswordPolicy.MustEqualExpectedValue =>
                "Archive passwords do not match the expected value.",
            _ => throw new ArgumentOutOfRangeException(
                nameof(slot),
                slot.PasswordPolicy,
                "Unknown password policy."
            ),
        };
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

    private static List<UploadConfigLinkCrypter> GetAllConfigsForRegistration(
        CollectionUploadSlot slot,
        int linkCrypterRegistrationId
    )
    {
        return slot
            .UploadConfigs.SelectMany(uc =>
                uc.LinkCrypters.Where(lc =>
                    lc.ContainerScope == LinkCrypterContainerScope.ReleaseCollection
                    && lc.LinkCrypterRegistrationId == linkCrypterRegistrationId
                    && lc.LinkCrypterRegistration.IsActive
                )
            )
            .ToList();
    }

    private static List<Upload> GetCompletedUploads(
        CollectionUploadSlot slot,
        int linkCrypterRegistrationId
    )
    {
        return slot
            .UploadConfigs.Where(uc =>
                uc.LinkCrypters.Any(lc =>
                    lc.ContainerScope == LinkCrypterContainerScope.ReleaseCollection
                    && lc.LinkCrypterRegistrationId == linkCrypterRegistrationId
                    && lc.LinkCrypterRegistration.IsActive
                )
            )
            .SelectMany(uc => uc.Uploads)
            .Where(upload =>
                upload.UploadState == UploadState.Completed
                && upload.OnlineState == OnlineState.Online
                && upload.UploadedFiles.Count > 0
            )
            .GroupBy(upload => upload.Id)
            .Select(group => group.First())
            .ToList();
    }

    private static List<string> GetUploadLinks(IReadOnlyList<Upload> uploads)
    {
        return uploads
            .SelectMany(upload => upload.UploadedFiles.Select(file => file.HosterFileLink))
            .Distinct()
            .OrderBy(link => link)
            .ToList();
    }

    private static void SyncSourceUploads(
        LinkCrypterContainer container,
        IReadOnlyList<Upload> uploads
    )
    {
        var currentUploadIds = uploads.Select(upload => upload.Id).ToHashSet();

        var stale = container
            .SourceUploads.Where(source => !currentUploadIds.Contains(source.UploadId))
            .ToList();

        foreach (var source in stale)
        {
            container.SourceUploads.Remove(source);
        }

        var existingUploadIds = container
            .SourceUploads.Select(source => source.UploadId)
            .ToHashSet();

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

    private static string? CleanPassword(string? password)
    {
        return string.IsNullOrWhiteSpace(password) ? null : password.Trim();
    }
}

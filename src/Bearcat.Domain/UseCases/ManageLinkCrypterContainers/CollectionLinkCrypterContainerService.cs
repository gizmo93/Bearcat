using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
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
)
{
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
                slot,
                container,
                linkCrypterConfig,
                cancellationToken
            );
        }
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
        CancellationToken cancellationToken
    )
    {
        var uploads = GetCompletedUploads(slot, linkCrypterConfig.LinkCrypterRegistrationId);
        if (uploads.Count == 0)
        {
            return;
        }

        var allLinkCrypterConfigs = GetAllConfigsForRegistration(
            slot,
            linkCrypterConfig.LinkCrypterRegistrationId
        );

        var problem = ValidateCollectionSettings(slot, uploads, allLinkCrypterConfigs);
        if (problem is not null)
        {
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

            return;
        }

        if (existingContainer is null)
        {
            await CreateContainerAsync(slot, uploads, linkCrypterConfig, cancellationToken);
        }
        else
        {
            await UpdateContainerAsync(
                existingContainer,
                uploads,
                linkCrypterConfig,
                cancellationToken
            );
        }
    }

    private async Task CreateContainerAsync(
        CollectionUploadSlot slot,
        IReadOnlyList<Upload> uploads,
        UploadConfigLinkCrypter linkCrypterConfig,
        CancellationToken cancellationToken
    )
    {
        var crypter = GetCrypter(linkCrypterConfig);
        var config = GetCrypterConfig(crypter, linkCrypterConfig);

        var result = await crypter.CreateContainerAsync(
            linkCrypterConfig: config,
            containerName: slot.ReleaseCollection.Name,
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

    private async Task UpdateContainerAsync(
        LinkCrypterContainer container,
        IReadOnlyList<Upload> uploads,
        UploadConfigLinkCrypter linkCrypterConfig,
        CancellationToken cancellationToken
    )
    {
        var crypter = GetCrypter(linkCrypterConfig);
        var config = GetCrypterConfig(crypter, linkCrypterConfig);

        var result = await crypter.UpdateContainerAsync(
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
            await repository.SaveChangesAsync(cancellationToken);
            return;
        }

        SyncSourceUploads(container, uploads);
        await repository.SaveChangesAsync(cancellationToken);
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
        container.Errors = [errorMessage];
        container.State = LinkCrypterContainerState.CreationFailed;
        notificationService.CreateError(
            message: $"Collection link crypter container {container.Id} is invalid: {errorMessage}",
            entity: container,
            selector: n => n.LinkCrypterContainer
        );
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
            .Select(upload => CleanPassword(upload.UploadConfig.ArchiveConfig.ArchivePassword))
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
                && upload.UploadedFiles.Any()
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

using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.LinkCrypterContainers;
using Bearcat.Domain.UseCases.ManageLinkCrypterContainers.MultiUploadContainers;
using Bearcat.Domain.UseCases.ManageLinkCrypterContainers.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageLinkCrypterContainers;

public class CollectionLinkCrypterContainerService(
    ILinkCrypterContainerCreationWriteRepository repository,
    ILogger<CollectionLinkCrypterContainerService> logger,
    MultiUploadLinkCrypterContainerService multiUploadContainerService
) : ICollectionLinkCrypterContainerUpdater
{
    private const string ContainerDescriptionInMessages = "Collection link crypter container";

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

        await multiUploadContainerService.RemoveContainersAsync(
            existingContainers
                .Where(c => !activeRegistrations.ContainsKey(c.LinkCrypterRegistrationId))
                .ToList(),
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

        var definition = await GetDefinitionWithValidCollectionSettingsAsync(
            slot: slot,
            existingContainer: container,
            linkCrypterConfig: linkCrypterConfig,
            cancellationToken: cancellationToken
        );

        if (definition is null)
        {
            return container;
        }

        await multiUploadContainerService.RecreateContainerAsync(
            definition,
            container,
            cancellationToken
        );
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
        var definition = await GetDefinitionWithValidCollectionSettingsAsync(
            slot: slot,
            existingContainer: existingContainer,
            linkCrypterConfig: linkCrypterConfig,
            cancellationToken: cancellationToken
        );

        if (definition is null)
        {
            return;
        }

        if (existingContainer is null)
        {
            await multiUploadContainerService.AddContainerAsync(
                definition,
                new LinkCrypterContainer
                {
                    Scope = LinkCrypterContainerScope.ReleaseCollection,
                    CollectionUploadSlotId = slot.Id,
                    LinkCrypterRegistrationId = linkCrypterConfig.LinkCrypterRegistrationId,
                },
                cancellationToken
            );
            return;
        }

        await multiUploadContainerService.UpdateContainerAsync(
            definition: definition,
            container: existingContainer,
            ignoreUpdateRetryDelay: ignoreUpdateRetryDelay,
            cancellationToken: cancellationToken
        );
    }

    private async Task<MultiUploadLinkCrypterContainerDefinition?> GetDefinitionWithValidCollectionSettingsAsync(
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

        var definition = new MultiUploadLinkCrypterContainerDefinition(
            ContainerDescriptionInMessages: ContainerDescriptionInMessages,
            OwnerDescriptionInMessages: $"upload slot {slot.Id}",
            ContainerName: slot.ReleaseCollection.Name,
            InvalidNotificationKind: NotificationKind.CollectionLinkCrypterContainerInvalid,
            ResetUploadsPostedAt: () => slot.ReleaseCollection.UploadsPostedAt = null,
            LinkCrypterConfig: linkCrypterConfig,
            Uploads: uploads,
            LinksPerMirrorNumber: new Dictionary<int, IReadOnlyList<string>>
            {
                [1] = GetUploadLinks(uploads),
            }
        );

        var problem = ValidateCollectionSettings(slot, uploads, allLinkCrypterConfigs);

        if (problem is null)
        {
            return definition;
        }

        logger.LogWarning(
            "Skipping collection container for slot {SlotId}, registration {RegistrationId}: {Reason}",
            slot.Id,
            linkCrypterConfig.LinkCrypterRegistrationId,
            problem
        );

        if (existingContainer is not null)
        {
            await multiUploadContainerService.MarkContainerAsInvalidAsync(
                definition: definition,
                container: existingContainer,
                errorMessage: problem,
                cancellationToken: cancellationToken
            );
        }

        return null;
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
            && config.HasSameContainerSettingsAs(first)
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

    private static string? CleanPassword(string? password)
    {
        return string.IsNullOrWhiteSpace(password) ? null : password.Trim();
    }
}

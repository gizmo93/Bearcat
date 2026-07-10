using System.Text;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageLinkCrypterContainers;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Dto;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.ValueObjects;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections;

public class ReleaseCollectionService(
    IReleaseCollectionWriteRepository writeRepository,
    CollectionLinkCrypterContainerService collectionContainerService,
    TimeProvider timeProvider
)
{
    private const string ManualSource = "Manual";

    public async Task<int> CreateAsync(
        string name,
        string key,
        ReleaseContentType releaseContentType,
        int releaseGroupId,
        CancellationToken cancellationToken = default
    )
    {
        var releaseCollection = new ReleaseCollection
        {
            Name = CleanRequired(name, nameof(name)),
            Key = CleanRequired(key, nameof(key)),
            ReleaseContentType = releaseContentType,
            ReleaseGroupId = releaseGroupId,
            CreatedAt = timeProvider.GetLocalNow(),
        };

        writeRepository.Add(releaseCollection);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return releaseCollection.Id;
    }

    public async Task UpdateAsync(
        int releaseCollectionId,
        string name,
        CancellationToken cancellationToken = default
    )
    {
        var releaseCollection = await writeRepository.GetByIdAsync(
            releaseCollectionId,
            cancellationToken
        );
        releaseCollection.Name = CleanRequired(name, nameof(name));

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateMetadataAsync(
        int releaseCollectionId,
        EditCollectionMetadataData data,
        CancellationToken cancellationToken = default
    )
    {
        var releaseCollection = await writeRepository.GetForCoverUpdateAsync(
            releaseCollectionId,
            cancellationToken
        );
        var newCoverUrl = CleanOptional(data.CoverUrl);
        var previousCoverUrl = releaseCollection.Metadata?.CoverUrl;

        var metadata = releaseCollection.Metadata;
        if (metadata is null)
        {
            metadata = new ReleaseCollectionMetadata { MetadataDatabaseClassName = ManualSource };
            releaseCollection.Metadata = metadata;
        }

        metadata.Title = CleanOptional(data.Title) ?? releaseCollection.Name;
        metadata.CoverUrl = newCoverUrl;
        metadata.Description = CleanOptional(data.Description);
        metadata.MetadataDatabaseUrl = CleanOptional(data.MetadataDatabaseUrl);

        if (!string.Equals(previousCoverUrl, newCoverUrl, StringComparison.Ordinal))
        {
            RemoveUploadedImages(releaseCollection.ImageUploadConfigs);
        }

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    private void RemoveUploadedImages(IReadOnlyList<ImageUploadConfig> imageUploadConfigs)
    {
        var uploadedImages = imageUploadConfigs
            .SelectMany(config => config.ImageUploads)
            .Where(upload => upload.UploadState == UploadState.Completed)
            .ToList();

        foreach (var uploadedImage in uploadedImages)
        {
            writeRepository.Remove(uploadedImage);
        }
    }

    public async Task UpdateContentTypeAsync(
        int releaseCollectionId,
        ReleaseContentType releaseContentType,
        CancellationToken cancellationToken = default
    )
    {
        var releaseCollection = await writeRepository.GetByIdAsync(
            releaseCollectionId,
            cancellationToken
        );
        releaseCollection.ReleaseContentType = releaseContentType;

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    )
    {
        var releaseCollection = await writeRepository.GetByIdAsync(
            releaseCollectionId,
            cancellationToken
        );
        writeRepository.Remove(releaseCollection);

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkUploadsPostedAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    )
    {
        var releaseCollection = await writeRepository.GetByIdAsync(
            releaseCollectionId,
            cancellationToken
        );
        releaseCollection.UploadsPostedAt = timeProvider.GetLocalNow();

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CreateUploadSlotAsync(
        int releaseCollectionId,
        string name,
        int hosterRegistrationId,
        string archiveConfigName,
        bool premiumOnlyDownload,
        bool isRequired,
        CollectionUploadSlotPasswordPolicy passwordPolicy,
        string? expectedArchivePassword,
        CancellationToken cancellationToken = default
    )
    {
        await writeRepository.GetByIdAsync(releaseCollectionId, cancellationToken);

        var (cleanedName, key) = await ResolveUniqueSlotKeyAsync(
            releaseCollectionId,
            name,
            cancellationToken
        );
        var archiveConfigTargets = await GetValidatedArchiveConfigTargetsAsync(
            releaseCollectionId,
            CleanRequired(archiveConfigName, nameof(archiveConfigName)),
            cancellationToken
        );

        var uploadSlot = new CollectionUploadSlot
        {
            ReleaseCollectionId = releaseCollectionId,
            Key = key,
            Name = cleanedName,
            IsRequired = isRequired,
            PasswordPolicy = passwordPolicy,
            ExpectedArchivePassword =
                passwordPolicy is CollectionUploadSlotPasswordPolicy.MustEqualExpectedValue
                    ? CleanRequired(
                        expectedArchivePassword ?? string.Empty,
                        nameof(expectedArchivePassword)
                    )
                    : null,
        };

        foreach (var target in archiveConfigTargets.Values)
        {
            uploadSlot.UploadConfigs.Add(
                new UploadConfig
                {
                    ReleaseId = target.ReleaseId,
                    ArchiveConfigId = target.ArchiveConfigId,
                    HosterRegistrationId = hosterRegistrationId,
                    Name = cleanedName,
                    PremiumOnlyDownload = premiumOnlyDownload,
                    LinkCrypters = [],
                    Uploads = [],
                }
            );
        }

        writeRepository.Add(uploadSlot);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return uploadSlot.Id;
    }

    private async Task<(string cleanedName, string key)> ResolveUniqueSlotKeyAsync(
        int releaseCollectionId,
        string name,
        CancellationToken cancellationToken
    )
    {
        var cleanedName = CleanRequired(name, nameof(name));
        var key = CreateStableKey(cleanedName);

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException(
                "Value must contain at least one letter or digit.",
                nameof(name)
            );
        }

        if (
            await writeRepository.UploadSlotKeyExistsAsync(
                releaseCollectionId,
                key,
                cancellationToken
            )
        )
        {
            throw new InvalidOperationException(
                "A collection upload slot with this key already exists."
            );
        }

        return (cleanedName, key);
    }

    private async Task<
        Dictionary<int, CollectionReleaseArchiveConfigTarget>
    > GetValidatedArchiveConfigTargetsAsync(
        int releaseCollectionId,
        string archiveConfigName,
        CancellationToken cancellationToken
    )
    {
        var releaseCount = await writeRepository.GetReleaseCountAsync(
            releaseCollectionId,
            cancellationToken
        );
        var archiveConfigTargets = await writeRepository.GetArchiveConfigTargetsAsync(
            releaseCollectionId,
            archiveConfigName,
            cancellationToken
        );

        var targetsByReleaseId = archiveConfigTargets
            .GroupBy(target => target.ReleaseId)
            .ToDictionary(group => group.Key, group => group.ToList());

        if (
            releaseCount == 0
            || targetsByReleaseId.Count != releaseCount
            || targetsByReleaseId.Values.Any(group => group.Count != 1)
        )
        {
            throw new InvalidOperationException(
                "The selected archive configuration must exist on every release in the collection."
            );
        }

        return targetsByReleaseId.ToDictionary(kv => kv.Key, kv => kv.Value.Single());
    }

    public async Task DeleteUploadSlotAsync(
        int collectionUploadSlotId,
        CancellationToken cancellationToken = default
    )
    {
        var uploadSlot = await writeRepository.GetUploadSlotForDeleteAsync(
            collectionUploadSlotId,
            cancellationToken
        );

        foreach (var uploadConfig in uploadSlot.UploadConfigs.ToList())
        {
            writeRepository.Remove(uploadConfig);
        }

        writeRepository.Remove(uploadSlot);
        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task AddReleaseAsync(
        int releaseCollectionId,
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var releaseCollection = await writeRepository.GetByIdWithSlotsAsync(
            releaseCollectionId,
            cancellationToken
        );

        var release = await writeRepository.GetReleaseByIdAsync(releaseId, cancellationToken);

        if (release.ReleaseGroupId != releaseCollection.ReleaseGroupId)
        {
            throw new InvalidOperationException(
                "The release must belong to the same release group as the collection."
            );
        }

        if (release.ReleaseCollectionId == releaseCollectionId)
        {
            return;
        }

        release.ReleaseCollectionId = releaseCollectionId;

        if (releaseCollection.UploadSlots.Count > 0)
        {
            var archiveConfigNames = releaseCollection
                .UploadSlots.Select(slot =>
                    slot.UploadConfigs.Select(uc => uc.ArchiveConfig.Name).FirstOrDefault()
                )
                .Where(name => name is not null)
                .Distinct()
                .ToList();

            var archiveConfigTargets = await writeRepository.GetArchiveConfigTargetsForReleaseAsync(
                releaseId: releaseId,
                archiveConfigNames: archiveConfigNames!,
                cancellationToken: cancellationToken
            );

            var targetsByName = archiveConfigTargets.ToDictionary(
                target => target.ArchiveConfigName!,
                StringComparer.Ordinal
            );

            foreach (var slot in releaseCollection.UploadSlots)
            {
                var referenceUploadConfig = slot.UploadConfigs.FirstOrDefault();
                if (referenceUploadConfig is null)
                {
                    continue;
                }

                var archiveConfigName = referenceUploadConfig.ArchiveConfig.Name;
                if (!targetsByName.TryGetValue(archiveConfigName, out var target))
                {
                    continue;
                }

                var uploadConfig = new UploadConfig
                {
                    ReleaseId = releaseId,
                    ArchiveConfigId = target.ArchiveConfigId,
                    HosterRegistrationId = referenceUploadConfig.HosterRegistrationId,
                    Name = referenceUploadConfig.Name,
                    PremiumOnlyDownload = referenceUploadConfig.PremiumOnlyDownload,
                    LinkCrypters = [],
                    Uploads = [],
                };

                CollectionLinkCrypterSync.ApplyToNewUploadConfig(
                    uploadConfig,
                    CollectionLinkCrypterSync.GetSettingsFromSlot(slot)
                );

                slot.UploadConfigs.Add(uploadConfig);
            }
        }

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveReleaseAsync(
        int releaseCollectionId,
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var release = await writeRepository.GetReleaseWithSlotUploadConfigsAsync(
            releaseId,
            cancellationToken
        );

        if (release.ReleaseCollectionId != releaseCollectionId)
        {
            throw new InvalidOperationException("The release does not belong to this collection.");
        }

        release.ReleaseCollectionId = null;

        var slotUploadConfigs = release
            .UploadConfigs.Where(uc => uc.CollectionUploadSlotId is not null)
            .ToList();

        foreach (var uploadConfig in slotUploadConfigs)
        {
            writeRepository.Remove(uploadConfig);
        }

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateSharedLinkCryptersAsync(
        int collectionUploadSlotId,
        IReadOnlyCollection<CollectionUploadSlotLinkCrypterSettings> linkCrypterSettings,
        CancellationToken cancellationToken = default
    )
    {
        var settingsByRegistrationId = CollectionLinkCrypterSync.NormalizeSettings(
            linkCrypterSettings
        );

        var uploadSlot = await writeRepository.GetUploadSlotForSharedLinkCrypterUpdateAsync(
            collectionUploadSlotId,
            cancellationToken
        );

        foreach (var uploadConfig in uploadSlot.UploadConfigs)
        {
            CollectionLinkCrypterSync.ApplyToExistingUploadConfig(
                writeRepository,
                uploadConfig,
                settingsByRegistrationId
            );
        }

        await writeRepository.SaveChangesAsync(cancellationToken);

        await collectionContainerService.UpdateContainersAsync(
            collectionUploadSlotId,
            cancellationToken
        );
    }

    private static string CleanRequired(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameterName)
            : value.Trim();
    }

    private static string? CleanOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string CreateStableKey(string value)
    {
        var keyBuilder = new StringBuilder(value.Length);
        var lastWasSeparator = true;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                keyBuilder.Append(character);
                lastWasSeparator = false;
                continue;
            }

            if (lastWasSeparator)
            {
                continue;
            }

            keyBuilder.Append('-');
            lastWasSeparator = true;
        }

        return keyBuilder.ToString().Trim('-');
    }
}

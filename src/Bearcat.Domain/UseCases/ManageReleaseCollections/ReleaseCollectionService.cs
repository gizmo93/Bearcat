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
    public async Task<int> CreateAsync(
        string name,
        string key,
        int releaseGroupId,
        CancellationToken cancellationToken = default
    )
    {
        var releaseCollection = new ReleaseCollection
        {
            Name = CleanRequired(name, nameof(name)),
            Key = CleanRequired(key, nameof(key)),
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

        var releaseCount = await writeRepository.GetReleaseCountAsync(
            releaseCollectionId,
            cancellationToken
        );
        var archiveConfigTargets = await writeRepository.GetArchiveConfigTargetsAsync(
            releaseCollectionId,
            CleanRequired(archiveConfigName, nameof(archiveConfigName)),
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

        foreach (var target in targetsByReleaseId.Values.Select(group => group.Single()))
        {
            uploadSlot.UploadConfigs.Add(
                new UploadConfig
                {
                    ReleaseId = target.ReleaseId,
                    ArchiveConfigId = target.ArchiveConfigId,
                    HosterRegistrationId = hosterRegistrationId,
                    Name = cleanedName,
                    PremiumOnlyDownload = premiumOnlyDownload,
                    LinksDistributedTo = [],
                    LinkCrypters = [],
                    Uploads = [],
                }
            );
        }

        writeRepository.Add(uploadSlot);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return uploadSlot.Id;
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

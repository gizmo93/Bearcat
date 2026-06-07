using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using System.Text;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Dto;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.ValueObjects;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections;

public class ReleaseCollectionService(
    IReleaseCollectionWriteRepository writeRepository,
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
            throw new ArgumentException("Value must contain at least one letter or digit.", nameof(name));
        }

        if (
            await writeRepository.UploadSlotKeyExistsAsync(
                releaseCollectionId,
                key,
                cancellationToken
            )
        )
        {
            throw new InvalidOperationException("A collection upload slot with this key already exists.");
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
                    ? CleanRequired(expectedArchivePassword ?? string.Empty, nameof(expectedArchivePassword))
                    : null,
        };

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
        var settingsByRegistrationId = linkCrypterSettings
            .GroupBy(settings => settings.LinkCrypterRegistrationId)
            .ToDictionary(group => group.Key, group => group.Last());
        var uploadSlot = await writeRepository.GetUploadSlotForSharedLinkCrypterUpdateAsync(
            collectionUploadSlotId,
            cancellationToken
        );

        foreach (var uploadConfig in uploadSlot.UploadConfigs)
        {
            SyncCollectionScopedLinkCrypters(uploadConfig, settingsByRegistrationId);
        }

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    private static string CleanRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return value.Trim();
    }

    private void SyncCollectionScopedLinkCrypters(
        UploadConfig uploadConfig,
        IReadOnlyDictionary<int, CollectionUploadSlotLinkCrypterSettings> settingsByRegistrationId
    )
    {
        var sharedLinkCrypters = uploadConfig
            .LinkCrypters.Where(linkCrypter =>
                linkCrypter.ContainerScope == LinkCrypterContainerScope.ReleaseCollection
            )
            .ToList();

        foreach (
            var linkCrypter in sharedLinkCrypters.Where(linkCrypter =>
                !settingsByRegistrationId.ContainsKey(linkCrypter.LinkCrypterRegistrationId)
            )
        )
        {
            writeRepository.Remove(linkCrypter);
        }

        var existingLinkCryptersByRegistrationId = sharedLinkCrypters.ToDictionary(
            linkCrypter => linkCrypter.LinkCrypterRegistrationId
        );

        foreach (var settings in settingsByRegistrationId.Values)
        {
            if (
                !existingLinkCryptersByRegistrationId.TryGetValue(
                    settings.LinkCrypterRegistrationId,
                    out var linkCrypter
                )
            )
            {
                linkCrypter = new UploadConfigLinkCrypter
                {
                    LinkCrypterRegistrationId = settings.LinkCrypterRegistrationId,
                    ContainerScope = LinkCrypterContainerScope.ReleaseCollection,
                    LinkCrypterContainers = [],
                };
                uploadConfig.LinkCrypters.Add(linkCrypter);
            }

            linkCrypter.Password = CleanOptional(settings.Password);
            linkCrypter.EnableCaptcha = settings.EnableCaptcha;
            linkCrypter.EnableContainerDownload = settings.EnableContainerDownload;
            linkCrypter.EnableClickAndLoad = settings.EnableClickAndLoad;
        }
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

            if (!lastWasSeparator)
            {
                keyBuilder.Append('-');
                lastWasSeparator = true;
            }
        }

        return keyBuilder.ToString().Trim('-');
    }
}

using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
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

    public async Task UpdateSharedLinkCryptersAsync(
        int collectionUploadSlotId,
        IReadOnlyCollection<int> linkCrypterRegistrationIds,
        CancellationToken cancellationToken = default
    )
    {
        var selectedRegistrationIds = linkCrypterRegistrationIds.Distinct().ToList();
        var uploadSlot = await writeRepository.GetUploadSlotForSharedLinkCrypterUpdateAsync(
            collectionUploadSlotId,
            cancellationToken
        );
        var existingSettingsByRegistrationId = uploadSlot
            .UploadConfigs.SelectMany(uploadConfig => uploadConfig.LinkCrypters)
            .Where(linkCrypter =>
                linkCrypter.ContainerScope == LinkCrypterContainerScope.ReleaseCollection
                && selectedRegistrationIds.Contains(linkCrypter.LinkCrypterRegistrationId)
            )
            .GroupBy(linkCrypter => linkCrypter.LinkCrypterRegistrationId)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var linkCrypters in uploadSlot.UploadConfigs.Select(uploadConfig => uploadConfig.LinkCrypters))
        {
            var sharedLinkCrypters = linkCrypters
                .Where(linkCrypter =>
                    linkCrypter.ContainerScope == LinkCrypterContainerScope.ReleaseCollection
                )
                .ToList();

            foreach (
                var linkCrypter in sharedLinkCrypters.Where(linkCrypter =>
                    !selectedRegistrationIds.Contains(linkCrypter.LinkCrypterRegistrationId)
                )
            )
            {
                writeRepository.Remove(linkCrypter);
            }

            var existingRegistrationIds = sharedLinkCrypters
                .Select(linkCrypter => linkCrypter.LinkCrypterRegistrationId)
                .ToHashSet();

            foreach (
                var registrationId in selectedRegistrationIds.Where(registrationId =>
                    !existingRegistrationIds.Contains(registrationId)
                )
            )
            {
                var settings = existingSettingsByRegistrationId.GetValueOrDefault(registrationId);
                linkCrypters.Add(
                    new UploadConfigLinkCrypter
                    {
                        LinkCrypterRegistrationId = registrationId,
                        ContainerScope = LinkCrypterContainerScope.ReleaseCollection,
                        Password = settings?.Password,
                        EnableCaptcha = settings?.EnableCaptcha ?? true,
                        EnableContainerDownload = settings?.EnableContainerDownload ?? true,
                        EnableClickAndLoad = settings?.EnableClickAndLoad ?? true,
                    }
                );
            }
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
}

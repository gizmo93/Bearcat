using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.ValueObjects;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections;

public class ReleaseCollectionAssignmentService(
    IReleaseCollectionWriteRepository writeRepository,
    TimeProvider timeProvider
)
{
    private readonly Dictionary<
        ReleaseCollectionCacheKey,
        ReleaseCollection
    > releaseCollectionsByKey = [];

    public async Task AssignFromTemplateAsync(
        Release release,
        ReleaseTemplate releaseTemplate,
        CancellationToken cancellationToken = default
    )
    {
        var detectionResult = ReleaseCollectionDetectionService.Detect(
            release.Name,
            releaseTemplate
        );

        if (detectionResult is null)
        {
            return;
        }

        var cacheKey = new ReleaseCollectionCacheKey(
            releaseTemplate.ReleaseGroupId,
            detectionResult.Key
        );

        if (!releaseCollectionsByKey.TryGetValue(cacheKey, out var releaseCollection))
        {
            releaseCollection = await writeRepository.GetByReleaseGroupAndKeyAsync(
                releaseTemplate.ReleaseGroupId,
                detectionResult.Key,
                cancellationToken
            );

            if (releaseCollection is null)
            {
                releaseCollection = new ReleaseCollection
                {
                    ReleaseGroupId = releaseTemplate.ReleaseGroupId,
                    Key = detectionResult.Key,
                    Name = detectionResult.Name,
                    CreatedAt = timeProvider.GetLocalNow(),
                };
                writeRepository.Add(releaseCollection);
            }

            releaseCollectionsByKey[cacheKey] = releaseCollection;
        }

        release.ReleaseCollection = releaseCollection;

        var uploadConfigTemplates = releaseTemplate
            .UploadConfigTemplates.OrderBy(template => template.Id)
            .ToList();

        var uploadConfigPairs = uploadConfigTemplates
            .Zip(
                release.UploadConfigs,
                (uploadConfigTemplate, uploadConfig) => (uploadConfigTemplate, uploadConfig)
            )
            .ToList();

        foreach (var (uploadConfigTemplate, uploadConfig) in uploadConfigPairs)
        {
            if (string.IsNullOrWhiteSpace(uploadConfigTemplate.CollectionUploadSlotKey))
            {
                continue;
            }

            var slotKey = uploadConfigTemplate.CollectionUploadSlotKey.Trim();
            var slot = releaseCollection.UploadSlots.FirstOrDefault(existingSlot =>
                string.Equals(existingSlot.Key, slotKey, StringComparison.Ordinal)
            );

            if (slot is null)
            {
                slot = new CollectionUploadSlot
                {
                    Key = slotKey,
                    Name = string.IsNullOrWhiteSpace(uploadConfigTemplate.CollectionUploadSlotName)
                        ? slotKey
                        : uploadConfigTemplate.CollectionUploadSlotName.Trim(),
                    IsRequired = uploadConfigTemplate.CollectionUploadSlotIsRequired,
                    PasswordPolicy = uploadConfigTemplate.CollectionUploadSlotPasswordPolicy,
                    ExpectedArchivePassword = string.IsNullOrWhiteSpace(
                        uploadConfigTemplate.CollectionUploadSlotExpectedArchivePassword
                    )
                        ? null
                        : uploadConfigTemplate.CollectionUploadSlotExpectedArchivePassword.Trim(),
                };
                releaseCollection.UploadSlots.Add(slot);
            }
            else
            {
                SyncCollectionScopedLinkCrypters(uploadConfig, slot);
            }

            uploadConfig.CollectionUploadSlot = slot;
        }
    }

    private static void SyncCollectionScopedLinkCrypters(
        UploadConfig uploadConfig,
        CollectionUploadSlot slot
    )
    {
        var slotSettingsByRegistrationId = slot
            .UploadConfigs.SelectMany(existingUploadConfig => existingUploadConfig.LinkCrypters)
            .Where(linkCrypter =>
                linkCrypter.ContainerScope == LinkCrypterContainerScope.ReleaseCollection
            )
            .GroupBy(linkCrypter => linkCrypter.LinkCrypterRegistrationId)
            .ToDictionary(group => group.Key, group => group.First());

        var currentLinkCryptersByRegistrationId = uploadConfig
            .LinkCrypters.Where(linkCrypter =>
                linkCrypter.ContainerScope == LinkCrypterContainerScope.ReleaseCollection
            )
            .ToDictionary(linkCrypter => linkCrypter.LinkCrypterRegistrationId);

        uploadConfig.LinkCrypters.RemoveAll(linkCrypter =>
            linkCrypter.ContainerScope == LinkCrypterContainerScope.ReleaseCollection
            && !slotSettingsByRegistrationId.ContainsKey(linkCrypter.LinkCrypterRegistrationId)
        );

        foreach (var settings in slotSettingsByRegistrationId.Values)
        {
            if (
                !currentLinkCryptersByRegistrationId.TryGetValue(
                    settings.LinkCrypterRegistrationId,
                    out var linkCrypter
                )
            )
            {
                uploadConfig.LinkCrypters.Add(
                    new UploadConfigLinkCrypter
                    {
                        LinkCrypterRegistrationId = settings.LinkCrypterRegistrationId,
                        ContainerScope = LinkCrypterContainerScope.ReleaseCollection,
                        Password = settings.Password,
                        EnableCaptcha = settings.EnableCaptcha,
                        EnableContainerDownload = settings.EnableContainerDownload,
                        EnableClickAndLoad = settings.EnableClickAndLoad,
                        LinkCrypterContainers = [],
                    }
                );

                continue;
            }

            linkCrypter.Password = settings.Password;
            linkCrypter.EnableCaptcha = settings.EnableCaptcha;
            linkCrypter.EnableContainerDownload = settings.EnableContainerDownload;
            linkCrypter.EnableClickAndLoad = settings.EnableClickAndLoad;
        }
    }

    private sealed record ReleaseCollectionCacheKey(int ReleaseGroupId, string Key);
}

using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.UseCases.ManageReleases;
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
        IReadOnlyList<ReleaseUploadConfigMatch> uploadConfigMatches,
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
                    ReleaseContentType = releaseTemplate.ReleaseContentType,
                    Key = detectionResult.Key,
                    Name = detectionResult.Name,
                    PrimaryLanguageCode = release.PrimaryLanguageCode,
                    CreatedAt = timeProvider.GetLocalNow(),
                };
                writeRepository.Add(releaseCollection);
            }

            releaseCollectionsByKey[cacheKey] = releaseCollection;
        }

        if (releaseCollection.PrimaryLanguageCode is null)
        {
            releaseCollection.PrimaryLanguageCode = release.PrimaryLanguageCode;
        }

        release.ReleaseCollection = releaseCollection;

        MaterializeImageUploadConfigs(releaseCollection, releaseTemplate);

        foreach (var match in uploadConfigMatches)
        {
            var uploadConfigTemplate = match.UploadConfigTemplate;
            var uploadConfig = match.UploadConfig;

            if (string.IsNullOrWhiteSpace(uploadConfigTemplate.CollectionUploadSlotKey))
            {
                continue;
            }

            var slotKey = uploadConfigTemplate.CollectionUploadSlotKey.Trim();
            var slot = releaseCollection.UploadSlots.FirstOrDefault(existingSlot =>
                string.Equals(existingSlot.Key, slotKey, StringComparison.Ordinal)
            );

            foreach (var linkCrypter in uploadConfig.LinkCrypters)
            {
                linkCrypter.ContainerScope = LinkCrypterContainerScope.ReleaseCollection;
            }

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
                CollectionLinkCrypterSync.ApplyToNewUploadConfig(
                    uploadConfig,
                    CollectionLinkCrypterSync.GetSettingsFromSlot(slot)
                );
            }

            uploadConfig.CollectionUploadSlot = slot;
            if (!slot.UploadConfigs.Contains(uploadConfig))
            {
                slot.UploadConfigs.Add(uploadConfig);
            }
        }
    }

    private static void MaterializeImageUploadConfigs(
        ReleaseCollection releaseCollection,
        ReleaseTemplate releaseTemplate
    )
    {
        foreach (var template in releaseTemplate.CollectionImageUploadConfigTemplates)
        {
            var alreadyConfigured = releaseCollection.ImageUploadConfigs.Any(config =>
                config.ImageHosterRegistrationId == template.ImageHosterRegistrationId
            );

            if (alreadyConfigured)
            {
                continue;
            }

            releaseCollection.ImageUploadConfigs.Add(
                new ImageUploadConfig
                {
                    Name = string.IsNullOrWhiteSpace(template.Name)
                        ? template.ImageHosterRegistration.Name
                        : template.Name.Trim(),
                    ImageHosterRegistrationId = template.ImageHosterRegistrationId,
                    ImageUploads = [],
                }
            );
        }
    }

    private sealed record ReleaseCollectionCacheKey(int ReleaseGroupId, string Key);
}

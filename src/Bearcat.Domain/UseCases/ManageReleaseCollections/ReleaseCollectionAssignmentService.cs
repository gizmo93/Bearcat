using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.UseCases.ManageReleases;
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
                    Key = detectionResult.Key,
                    Name = detectionResult.Name,
                    CreatedAt = timeProvider.GetLocalNow(),
                };
                writeRepository.Add(releaseCollection);
            }

            releaseCollectionsByKey[cacheKey] = releaseCollection;
        }

        release.ReleaseCollection = releaseCollection;

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

    private sealed record ReleaseCollectionCacheKey(int ReleaseGroupId, string Key);
}

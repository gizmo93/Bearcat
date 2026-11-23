using System.Text;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.CollectionAssignment;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.ValueObjects;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections;

public class ReleaseCollectionAssignmentService(
    IReleaseCollectionWriteRepository writeRepository,
    TimeProvider timeProvider
) : IReleaseCollectionAssigner
{
    private readonly Dictionary<string, ReleaseCollection> releaseCollectionsByKey = [];

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

        if (!releaseCollectionsByKey.TryGetValue(detectionResult.Key, out var releaseCollection))
        {
            releaseCollection =
                await writeRepository.GetByKeyAsync(detectionResult.Key, cancellationToken)
                ?? await GetByNameOrCreateAsync(
                    release: release,
                    releaseTemplate: releaseTemplate,
                    detectionResult: detectionResult,
                    cancellationToken: cancellationToken
                );

            releaseCollectionsByKey[detectionResult.Key] = releaseCollection;
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

    public void ClearCachedReleaseCollections()
    {
        releaseCollectionsByKey.Clear();
    }

    private async Task<ReleaseCollection> GetByNameOrCreateAsync(
        Release release,
        ReleaseTemplate releaseTemplate,
        ReleaseCollectionDetectionResult detectionResult,
        CancellationToken cancellationToken
    )
    {
        var existingReleaseCollection =
            GetReleaseCollectionOfCurrentBatchByName(detectionResult.Name)
            ?? await writeRepository.GetByNameAsync(
                name: detectionResult.Name,
                excludedReleaseCollectionId: null,
                cancellationToken: cancellationToken
            );

        if (existingReleaseCollection is not null)
        {
            return existingReleaseCollection;
        }

        var releaseCollection = new ReleaseCollection
        {
            ReleaseGroupId = releaseTemplate.ReleaseGroupId,
            ReleaseContentType = releaseTemplate.ReleaseContentType,
            Key = detectionResult.Key,
            Name = detectionResult.Name,
            PrimaryLanguageCode = release.PrimaryLanguageCode,
            CreatedAt = timeProvider.GetLocalNow(),
        };

        writeRepository.Add(releaseCollection);

        return releaseCollection;
    }

    private ReleaseCollection? GetReleaseCollectionOfCurrentBatchByName(string name)
    {
        return releaseCollectionsByKey.Values.FirstOrDefault(releaseCollection =>
            string.Equals(
                releaseCollection.Name.Normalize(NormalizationForm.FormC),
                name.Normalize(NormalizationForm.FormC),
                StringComparison.OrdinalIgnoreCase
            )
        );
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
                    UploadCover = true,
                    UploadScreenshots = false,
                    UploadScreenshotGrid = false,
                    ImageUploads = [],
                }
            );
        }
    }
}

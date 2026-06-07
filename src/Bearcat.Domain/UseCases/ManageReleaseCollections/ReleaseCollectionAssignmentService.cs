using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections;

public class ReleaseCollectionAssignmentService(
    IReleaseCollectionWriteRepository writeRepository,
    TimeProvider timeProvider
)
{
    public async Task AssignFromTemplateAsync(
        Release release,
        ReleaseTemplate releaseTemplate,
        CancellationToken cancellationToken = default
    )
    {
        var detectionResult = ReleaseCollectionDetectionService.Detect(release.Name, releaseTemplate);

        if (detectionResult is null)
        {
            return;
        }

        var releaseCollection = await writeRepository.GetByReleaseGroupAndKeyAsync(
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

        release.ReleaseCollection = releaseCollection;

        var uploadConfigTemplates = releaseTemplate
            .UploadConfigTemplates.OrderBy(template => template.Id)
            .ToList();

        var uploadConfigPairs = uploadConfigTemplates
            .Zip(
                release.UploadConfigs,
                (template, uploadConfig) => new
                {
                    Template = template,
                    UploadConfig = uploadConfig,
                }
            )
            .ToList();

        foreach (var pair in uploadConfigPairs)
        {
            if (string.IsNullOrWhiteSpace(pair.Template.CollectionUploadSlotKey))
            {
                continue;
            }

            var slotKey = pair.Template.CollectionUploadSlotKey.Trim();
            var slot = releaseCollection.UploadSlots.FirstOrDefault(existingSlot =>
                string.Equals(existingSlot.Key, slotKey, StringComparison.Ordinal)
            );

            if (slot is null)
            {
                slot = new CollectionUploadSlot
                {
                    Key = slotKey,
                    Name = string.IsNullOrWhiteSpace(pair.Template.CollectionUploadSlotName)
                        ? slotKey
                        : pair.Template.CollectionUploadSlotName.Trim(),
                    IsRequired = pair.Template.CollectionUploadSlotIsRequired,
                    PasswordPolicy = pair.Template.CollectionUploadSlotPasswordPolicy,
                    ExpectedArchivePassword = string.IsNullOrWhiteSpace(
                        pair.Template.CollectionUploadSlotExpectedArchivePassword
                    )
                        ? null
                        : pair.Template.CollectionUploadSlotExpectedArchivePassword.Trim(),
                };
                releaseCollection.UploadSlots.Add(slot);
            }

            pair.UploadConfig.CollectionUploadSlot = slot;
        }
    }
}

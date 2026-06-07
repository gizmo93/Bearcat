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
    }
}

using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Shared.ProgressSteps;

namespace Bearcat.Website.Pages.ManageReleaseCollections.ProgressSteps;

public static class ReleaseCollectionProgressStepService
{
    public static IReadOnlyList<ReleaseCollectionProgressStep> BuildReleaseCollectionProgressSteps(
        ReleaseCollectionDetailReadModel releaseCollection,
        IReadOnlyList<CollectionImageUploadReadModel> imageUploads,
        int postedLocationCount
    )
    {
        return
        [
            BuildInfoStep(releaseCollection.Metadata),
            BuildReleasesStep(releaseCollection.Releases),
            BuildLinkContainersStep(releaseCollection.UploadSlots),
            BuildImagesStep(imageUploads),
            BuildPostedStep(releaseCollection, postedLocationCount),
        ];
    }

    private static ReleaseCollectionProgressStep BuildInfoStep(
        ReleaseCollectionMetadataReadModel? metadata
    )
    {
        return metadata is null
            ? new ReleaseCollectionProgressStep(
                Kind: ReleaseCollectionProgressStepKind.Info,
                State: ProgressStepState.Pending
            )
            : new ReleaseCollectionProgressStep(
                Kind: ReleaseCollectionProgressStepKind.Info,
                State: ProgressStepState.Done
            )
            {
                MetadataDatabaseName = metadata.MetadataDatabaseName,
            };
    }

    private static ReleaseCollectionProgressStep BuildReleasesStep(
        IReadOnlyList<ReleaseCollectionReleaseReadModel> releases
    )
    {
        var releasesWithUploadConfigs = releases
            .Where(release => release.ActiveUploadConfigsCount > 0)
            .ToList();

        if (releasesWithUploadConfigs.Count == 0)
        {
            return new ReleaseCollectionProgressStep(
                Kind: ReleaseCollectionProgressStepKind.Releases,
                State: ProgressStepState.Pending
            );
        }

        var onlineReleaseCount = releasesWithUploadConfigs.Count(release =>
            release.OnlineState is OnlineState.Online
        );

        return new ReleaseCollectionProgressStep(
            Kind: ReleaseCollectionProgressStepKind.Releases,
            State: onlineReleaseCount < releasesWithUploadConfigs.Count
                ? ProgressStepState.Attention
                : ProgressStepState.Done
        )
        {
            Count = onlineReleaseCount,
            TotalCount = releasesWithUploadConfigs.Count,
        };
    }

    private static ReleaseCollectionProgressStep BuildLinkContainersStep(
        IReadOnlyList<CollectionUploadSlotReadModel> uploadSlots
    )
    {
        var containers = uploadSlots.SelectMany(slot => slot.Containers).ToList();

        if (containers.Count == 0)
        {
            var hasSharedLinkCrypters = uploadSlots.Any(slot => slot.SharedLinkCrypters.Count > 0);

            return new ReleaseCollectionProgressStep(
                Kind: ReleaseCollectionProgressStepKind.LinkContainers,
                State: hasSharedLinkCrypters
                    ? ProgressStepState.Pending
                    : ProgressStepState.NotApplicable
            );
        }

        var failedContainerCount = containers.Count(container =>
            container.State is LinkCrypterContainerState.CreationFailed
        );

        return failedContainerCount > 0
            ? new ReleaseCollectionProgressStep(
                Kind: ReleaseCollectionProgressStepKind.LinkContainers,
                State: ProgressStepState.Attention
            )
            {
                Count = failedContainerCount,
                TotalCount = containers.Count,
            }
            : new ReleaseCollectionProgressStep(
                Kind: ReleaseCollectionProgressStepKind.LinkContainers,
                State: ProgressStepState.Done
            )
            {
                Count = containers.Count,
                TotalCount = containers.Count,
            };
    }

    private static ReleaseCollectionProgressStep BuildImagesStep(
        IReadOnlyList<CollectionImageUploadReadModel> imageUploads
    )
    {
        if (imageUploads.Count == 0)
        {
            return new ReleaseCollectionProgressStep(
                Kind: ReleaseCollectionProgressStepKind.Images,
                State: ProgressStepState.NotApplicable
            );
        }

        var failedImageUploadCount = imageUploads.Count(imageUpload =>
            imageUpload.UploadState is UploadState.Failed
        );

        if (failedImageUploadCount > 0)
        {
            return new ReleaseCollectionProgressStep(
                Kind: ReleaseCollectionProgressStepKind.Images,
                State: ProgressStepState.Attention
            )
            {
                Count = failedImageUploadCount,
                TotalCount = imageUploads.Count,
            };
        }

        var isAnyImageUploadRunning = imageUploads.Any(imageUpload =>
            imageUpload.UploadState
                is UploadState.WaitingForArchive
                    or UploadState.Pending
                    or UploadState.Uploading
                    or UploadState.CancellationRequested
        );

        if (isAnyImageUploadRunning)
        {
            return new ReleaseCollectionProgressStep(
                Kind: ReleaseCollectionProgressStepKind.Images,
                State: ProgressStepState.InProgress
            );
        }

        var completedImageUploadCount = imageUploads.Count(imageUpload =>
            imageUpload.UploadState is UploadState.Completed
        );

        return completedImageUploadCount > 0
            ? new ReleaseCollectionProgressStep(
                Kind: ReleaseCollectionProgressStepKind.Images,
                State: ProgressStepState.Done
            )
            {
                Count = completedImageUploadCount,
                TotalCount = imageUploads.Count,
            }
            : new ReleaseCollectionProgressStep(
                Kind: ReleaseCollectionProgressStepKind.Images,
                State: ProgressStepState.Pending
            );
    }

    private static ReleaseCollectionProgressStep BuildPostedStep(
        ReleaseCollectionDetailReadModel releaseCollection,
        int postedLocationCount
    )
    {
        return postedLocationCount > 0 || releaseCollection.UploadsPostedAt is not null
            ? new ReleaseCollectionProgressStep(
                Kind: ReleaseCollectionProgressStepKind.Posted,
                State: ProgressStepState.Done
            )
            {
                Count = postedLocationCount,
            }
            : new ReleaseCollectionProgressStep(
                Kind: ReleaseCollectionProgressStepKind.Posted,
                State: ProgressStepState.Pending
            );
    }
}

using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.ManageReleases.ProgressSteps;

public static class ReleaseProgressStepCalculator
{
    public static IReadOnlyList<ReleaseProgressStep> Calculate(
        ReleaseReadModel release,
        ReleaseInfoReadModel? releaseInfo,
        ReleaseMetadataReadModel? releaseMetadata,
        IReadOnlyList<ArchiveConfigReadModel> archiveConfigs,
        IReadOnlyList<ReleaseOverviewUploadReadModel> overviewUploads,
        int postedLocationCount
    ) =>
        [
            CalculateInfoStep(releaseInfo, releaseMetadata),
            CalculateArchivedStep(archiveConfigs),
            CalculateUploadedStep(release, overviewUploads),
            CalculateLinkContainersStep(overviewUploads),
            CalculatePostedStep(release, postedLocationCount),
        ];

    private static ReleaseProgressStep CalculateInfoStep(
        ReleaseInfoReadModel? releaseInfo,
        ReleaseMetadataReadModel? releaseMetadata
    )
    {
        List<string> databaseNames = [];

        if (releaseMetadata is not null)
        {
            databaseNames.Add(
                DatabaseDisplayName.GetFromClassName(releaseMetadata.MetadataDatabaseClassName)
            );
        }

        if (releaseInfo is not null)
        {
            databaseNames.Add(
                DatabaseDisplayName.GetFromClassName(releaseInfo.NfoDatabaseClassName)
            );
        }

        return databaseNames.Count == 0
            ? new ReleaseProgressStep(
                ReleaseProgressStepKind.Info,
                ReleaseProgressStepState.Pending
            )
            : new ReleaseProgressStep(ReleaseProgressStepKind.Info, ReleaseProgressStepState.Done)
            {
                DatabaseNames = databaseNames,
            };
    }

    private static ReleaseProgressStep CalculateArchivedStep(
        IReadOnlyList<ArchiveConfigReadModel> archiveConfigs
    )
    {
        var latestArchiveStates = archiveConfigs
            .Where(archiveConfig => archiveConfig.ArchiveSummaries.Count > 0)
            .Select(archiveConfig =>
                archiveConfig.ArchiveSummaries.MaxBy(archive => archive.ArchiveId)!.ArchiveState
            )
            .ToList();

        if (latestArchiveStates.Contains(ArchiveState.CreationFailed))
        {
            return CreateArchivedAttentionStep(ArchiveState.CreationFailed);
        }

        if (latestArchiveStates.Contains(ArchiveState.MissingFiles))
        {
            return CreateArchivedAttentionStep(ArchiveState.MissingFiles);
        }

        if (
            latestArchiveStates.Any(state =>
                state is ArchiveState.Creating or ArchiveState.Restoring
            )
        )
        {
            return new ReleaseProgressStep(
                ReleaseProgressStepKind.Archived,
                ReleaseProgressStepState.InProgress
            );
        }

        var finishedArchiveCount = latestArchiveStates.Count(state =>
            state is ArchiveState.Created or ArchiveState.Deleted
        );

        return finishedArchiveCount == 0
            ? new ReleaseProgressStep(
                ReleaseProgressStepKind.Archived,
                ReleaseProgressStepState.Pending
            )
            : new ReleaseProgressStep(
                ReleaseProgressStepKind.Archived,
                ReleaseProgressStepState.Done
            )
            {
                Count = finishedArchiveCount,
            };
    }

    private static ReleaseProgressStep CreateArchivedAttentionStep(ArchiveState problemState) =>
        new(ReleaseProgressStepKind.Archived, ReleaseProgressStepState.Attention)
        {
            ArchiveProblemState = problemState,
        };

    private static ReleaseProgressStep CalculateUploadedStep(
        ReleaseReadModel release,
        IReadOnlyList<ReleaseOverviewUploadReadModel> overviewUploads
    )
    {
        if (release.ActiveUploadConfigsCount == 0)
        {
            return new ReleaseProgressStep(
                ReleaseProgressStepKind.Uploaded,
                ReleaseProgressStepState.Pending
            );
        }

        var isAnyUploadRunning = overviewUploads.Any(upload =>
            upload.UploadState
                is UploadState.WaitingForArchive
                    or UploadState.Pending
                    or UploadState.Uploading
                    or UploadState.CancellationRequested
        );

        var state = ReleaseProgressStepState.Done;

        if (isAnyUploadRunning)
        {
            state = ReleaseProgressStepState.InProgress;
        }
        else if (release.OnlineUploadConfigsCount < release.ActiveUploadConfigsCount)
        {
            state = ReleaseProgressStepState.Attention;
        }

        return new ReleaseProgressStep(ReleaseProgressStepKind.Uploaded, state)
        {
            Count = release.OnlineUploadConfigsCount,
            TotalCount = release.ActiveUploadConfigsCount,
        };
    }

    private static ReleaseProgressStep CalculateLinkContainersStep(
        IReadOnlyList<ReleaseOverviewUploadReadModel> overviewUploads
    )
    {
        var containers = overviewUploads
            .SelectMany(upload => upload.LinkCrypterLinks)
            .DistinctBy(container => container.LinkCrypterContainerId)
            .ToList();

        if (containers.Count == 0)
        {
            return new ReleaseProgressStep(
                ReleaseProgressStepKind.LinkContainers,
                ReleaseProgressStepState.NotApplicable
            );
        }

        var failedContainerCount = containers.Count(container =>
            container.State is LinkCrypterContainerState.CreationFailed
        );

        return failedContainerCount > 0
            ? new ReleaseProgressStep(
                ReleaseProgressStepKind.LinkContainers,
                ReleaseProgressStepState.Attention
            )
            {
                Count = failedContainerCount,
                TotalCount = containers.Count,
            }
            : new ReleaseProgressStep(
                ReleaseProgressStepKind.LinkContainers,
                ReleaseProgressStepState.Done
            )
            {
                Count = containers.Count,
                TotalCount = containers.Count,
            };
    }

    private static ReleaseProgressStep CalculatePostedStep(
        ReleaseReadModel release,
        int postedLocationCount
    ) =>
        postedLocationCount > 0 || release.UploadsPostedAt is not null
            ? new ReleaseProgressStep(ReleaseProgressStepKind.Posted, ReleaseProgressStepState.Done)
            {
                Count = postedLocationCount,
            }
            : new ReleaseProgressStep(
                ReleaseProgressStepKind.Posted,
                ReleaseProgressStepState.Pending
            );
}

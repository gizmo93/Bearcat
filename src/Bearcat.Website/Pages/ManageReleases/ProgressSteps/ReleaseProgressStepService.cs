using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.ManageReleases.ProgressSteps;

public static class ReleaseProgressStepService
{
    public static IReadOnlyList<ReleaseProgressStep> BuildReleaseProcessSteps(
        ReleaseReadModel release,
        ReleaseInfoReadModel? releaseInfo,
        ReleaseMetadataReadModel? releaseMetadata,
        IReadOnlyList<ArchiveConfigReadModel> archiveConfigs,
        IReadOnlyList<ReleaseOverviewUploadReadModel> overviewUploads,
        int postedLocationCount
    )
    {
        return
        [
            BuildInfoStep(releaseInfo, releaseMetadata),
            BuildArchivedStep(archiveConfigs),
            BuildUploadedStep(release, overviewUploads),
            BuildLinkContainersStep(overviewUploads),
            BuildPostedStep(release, postedLocationCount),
        ];
    }

    private static ReleaseProgressStep BuildInfoStep(
        ReleaseInfoReadModel? releaseInfo,
        ReleaseMetadataReadModel? releaseMetadata
    )
    {
        var databaseNames = new List<string>();

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
                Kind: ReleaseProgressStepKind.Info,
                State: ReleaseProgressStepState.Pending
            )
            : new ReleaseProgressStep(
                Kind: ReleaseProgressStepKind.Info,
                State: ReleaseProgressStepState.Done
            )
            {
                DatabaseNames = databaseNames,
            };
    }

    private static ReleaseProgressStep BuildArchivedStep(
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
                Kind: ReleaseProgressStepKind.Archived,
                State: ReleaseProgressStepState.InProgress
            );
        }

        var finishedArchiveCount = latestArchiveStates.Count(state =>
            state is ArchiveState.Created or ArchiveState.Deleted
        );

        return finishedArchiveCount == 0
            ? new ReleaseProgressStep(
                Kind: ReleaseProgressStepKind.Archived,
                State: ReleaseProgressStepState.Pending
            )
            : new ReleaseProgressStep(
                Kind: ReleaseProgressStepKind.Archived,
                State: ReleaseProgressStepState.Done
            )
            {
                Count = finishedArchiveCount,
            };
    }

    private static ReleaseProgressStep CreateArchivedAttentionStep(ArchiveState problemState)
    {
        return new ReleaseProgressStep(
            Kind: ReleaseProgressStepKind.Archived,
            State: ReleaseProgressStepState.Attention
        )
        {
            ArchiveProblemState = problemState,
        };
    }

    private static ReleaseProgressStep BuildUploadedStep(
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

        return new ReleaseProgressStep(Kind: ReleaseProgressStepKind.Uploaded, State: state)
        {
            Count = release.OnlineUploadConfigsCount,
            TotalCount = release.ActiveUploadConfigsCount,
        };
    }

    private static ReleaseProgressStep BuildLinkContainersStep(
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
                Kind: ReleaseProgressStepKind.LinkContainers,
                State: ReleaseProgressStepState.NotApplicable
            );
        }

        var failedContainerCount = containers.Count(container =>
            container.State is LinkCrypterContainerState.CreationFailed
        );

        return failedContainerCount > 0
            ? new ReleaseProgressStep(
                Kind: ReleaseProgressStepKind.LinkContainers,
                State: ReleaseProgressStepState.Attention
            )
            {
                Count = failedContainerCount,
                TotalCount = containers.Count,
            }
            : new ReleaseProgressStep(
                Kind: ReleaseProgressStepKind.LinkContainers,
                State: ReleaseProgressStepState.Done
            )
            {
                Count = containers.Count,
                TotalCount = containers.Count,
            };
    }

    private static ReleaseProgressStep BuildPostedStep(
        ReleaseReadModel release,
        int postedLocationCount
    )
    {
        return postedLocationCount > 0 || release.UploadsPostedAt is not null
            ? new ReleaseProgressStep(
                Kind: ReleaseProgressStepKind.Posted,
                State: ReleaseProgressStepState.Done
            )
            {
                Count = postedLocationCount,
            }
            : new ReleaseProgressStep(
                Kind: ReleaseProgressStepKind.Posted,
                State: ReleaseProgressStepState.Pending
            );
    }
}

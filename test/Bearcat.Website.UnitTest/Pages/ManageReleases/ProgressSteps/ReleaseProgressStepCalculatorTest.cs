using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Pages.ManageReleases.ProgressSteps;
using Shouldly;

namespace Bearcat.Website.UnitTest.Pages.ManageReleases.ProgressSteps;

public class ReleaseProgressStepCalculatorTest
{
    [Test]
    public void Calculate_ReturnsStepsInFixedOrder()
    {
        // Act
        var steps = Calculate();

        // Assert
        steps
            .Select(step => step.Kind)
            .ShouldBe([
                ReleaseProgressStepKind.Info,
                ReleaseProgressStepKind.Archived,
                ReleaseProgressStepKind.Uploaded,
                ReleaseProgressStepKind.LinkContainers,
                ReleaseProgressStepKind.Posted,
            ]);
    }

    [Test]
    public void Calculate_NothingDone_ReturnsPendingAndNotApplicableSteps()
    {
        // Act
        var steps = Calculate();

        // Assert
        steps
            .Select(step => step.State)
            .ShouldBe([
                ReleaseProgressStepState.Pending,
                ReleaseProgressStepState.Pending,
                ReleaseProgressStepState.Pending,
                ReleaseProgressStepState.NotApplicable,
                ReleaseProgressStepState.Pending,
            ]);
    }

    [Test]
    public void Calculate_MetadataAndReleaseInfoExist_ReturnsDoneInfoStepWithDatabaseNames()
    {
        // Act
        var step = GetStep(
            Calculate(
                releaseInfo: CreateReleaseInfo("XrelNfoDatabase"),
                releaseMetadata: CreateReleaseMetadata("TmdbMetadataDatabase")
            ),
            ReleaseProgressStepKind.Info
        );

        // Assert
        step.State.ShouldBe(ReleaseProgressStepState.Done);
        step.DatabaseNames.ShouldBe(["Tmdb", "xREL"]);
    }

    [Test]
    public void Calculate_OnlyReleaseInfoExists_ReturnsDoneInfoStepWithReleaseInfoDatabaseName()
    {
        // Act
        var step = GetStep(
            Calculate(releaseInfo: CreateReleaseInfo("SrrDbNfoDatabase")),
            ReleaseProgressStepKind.Info
        );

        // Assert
        step.State.ShouldBe(ReleaseProgressStepState.Done);
        step.DatabaseNames.ShouldBe(["SrrDb"]);
    }

    [Test]
    public void Calculate_ArchiveConfigsWithoutArchives_ReturnsPendingArchivedStep()
    {
        // Act
        var step = GetStep(
            Calculate(archiveConfigs: [CreateArchiveConfig(), CreateArchiveConfig()]),
            ReleaseProgressStepKind.Archived
        );

        // Assert
        step.State.ShouldBe(ReleaseProgressStepState.Pending);
    }

    [Test]
    public void Calculate_LatestArchivesCreatedOrDeleted_ReturnsDoneArchivedStepWithCount()
    {
        // Act
        var step = GetStep(
            Calculate(
                archiveConfigs:
                [
                    CreateArchiveConfig(CreateArchive(1, ArchiveState.Created)),
                    CreateArchiveConfig(CreateArchive(2, ArchiveState.Deleted)),
                ]
            ),
            ReleaseProgressStepKind.Archived
        );

        // Assert
        step.State.ShouldBe(ReleaseProgressStepState.Done);
        step.Count.ShouldBe(2);
    }

    [TestCase(ArchiveState.Creating)]
    [TestCase(ArchiveState.Restoring)]
    public void Calculate_LatestArchiveCreatingOrRestoring_ReturnsInProgressArchivedStep(
        ArchiveState archiveState
    )
    {
        // Act
        var step = GetStep(
            Calculate(
                archiveConfigs:
                [
                    CreateArchiveConfig(CreateArchive(1, ArchiveState.Created)),
                    CreateArchiveConfig(CreateArchive(2, archiveState)),
                ]
            ),
            ReleaseProgressStepKind.Archived
        );

        // Assert
        step.State.ShouldBe(ReleaseProgressStepState.InProgress);
    }

    [TestCase(ArchiveState.CreationFailed)]
    [TestCase(ArchiveState.MissingFiles)]
    public void Calculate_LatestArchiveHasProblem_ReturnsAttentionArchivedStepWithProblemState(
        ArchiveState archiveState
    )
    {
        // Act
        var step = GetStep(
            Calculate(
                archiveConfigs:
                [
                    CreateArchiveConfig(CreateArchive(1, ArchiveState.Creating)),
                    CreateArchiveConfig(CreateArchive(2, archiveState)),
                ]
            ),
            ReleaseProgressStepKind.Archived
        );

        // Assert
        step.State.ShouldBe(ReleaseProgressStepState.Attention);
        step.ArchiveProblemState.ShouldBe(archiveState);
    }

    [Test]
    public void Calculate_CreationFailedAndMissingFiles_ReturnsCreationFailedAsProblemState()
    {
        // Act
        var step = GetStep(
            Calculate(
                archiveConfigs:
                [
                    CreateArchiveConfig(CreateArchive(1, ArchiveState.MissingFiles)),
                    CreateArchiveConfig(CreateArchive(2, ArchiveState.CreationFailed)),
                ]
            ),
            ReleaseProgressStepKind.Archived
        );

        // Assert
        step.ArchiveProblemState.ShouldBe(ArchiveState.CreationFailed);
    }

    [Test]
    public void Calculate_OlderArchiveFailedButLatestCreated_ReturnsDoneArchivedStep()
    {
        // Act
        var step = GetStep(
            Calculate(
                archiveConfigs:
                [
                    CreateArchiveConfig(
                        CreateArchive(5, ArchiveState.CreationFailed),
                        CreateArchive(8, ArchiveState.Created)
                    ),
                ]
            ),
            ReleaseProgressStepKind.Archived
        );

        // Assert
        step.State.ShouldBe(ReleaseProgressStepState.Done);
        step.Count.ShouldBe(1);
    }

    [Test]
    public void Calculate_NoActiveUploadConfigs_ReturnsPendingUploadedStep()
    {
        // Act
        var step = GetStep(
            Calculate(release: CreateRelease(activeUploadConfigsCount: 0)),
            ReleaseProgressStepKind.Uploaded
        );

        // Assert
        step.State.ShouldBe(ReleaseProgressStepState.Pending);
    }

    [TestCase(UploadState.WaitingForArchive)]
    [TestCase(UploadState.Pending)]
    [TestCase(UploadState.Uploading)]
    [TestCase(UploadState.CancellationRequested)]
    public void Calculate_UploadRunning_ReturnsInProgressUploadedStep(UploadState uploadState)
    {
        // Act
        var step = GetStep(
            Calculate(
                release: CreateRelease(activeUploadConfigsCount: 2, onlineUploadConfigsCount: 1),
                overviewUploads:
                [
                    CreateOverviewUpload(UploadState.Completed),
                    CreateOverviewUpload(uploadState),
                ]
            ),
            ReleaseProgressStepKind.Uploaded
        );

        // Assert
        step.State.ShouldBe(ReleaseProgressStepState.InProgress);
    }

    [Test]
    public void Calculate_NotAllUploadConfigsOnline_ReturnsAttentionUploadedStepWithCounts()
    {
        // Act
        var step = GetStep(
            Calculate(
                release: CreateRelease(activeUploadConfigsCount: 7, onlineUploadConfigsCount: 6),
                overviewUploads:
                [
                    CreateOverviewUpload(UploadState.Completed),
                    CreateOverviewUpload(UploadState.Failed),
                ]
            ),
            ReleaseProgressStepKind.Uploaded
        );

        // Assert
        step.State.ShouldBe(ReleaseProgressStepState.Attention);
        step.Count.ShouldBe(6);
        step.TotalCount.ShouldBe(7);
    }

    [Test]
    public void Calculate_AllUploadConfigsOnline_ReturnsDoneUploadedStepWithCounts()
    {
        // Act
        var step = GetStep(
            Calculate(
                release: CreateRelease(activeUploadConfigsCount: 7, onlineUploadConfigsCount: 7),
                overviewUploads: [CreateOverviewUpload(UploadState.Completed)]
            ),
            ReleaseProgressStepKind.Uploaded
        );

        // Assert
        step.State.ShouldBe(ReleaseProgressStepState.Done);
        step.Count.ShouldBe(7);
        step.TotalCount.ShouldBe(7);
    }

    [Test]
    public void Calculate_AllContainersCreated_ReturnsDoneLinkContainersStepWithDistinctCount()
    {
        // Arrange
        var releaseContainer = CreateContainer(1, LinkCrypterContainerState.Created);

        // Act
        var step = GetStep(
            Calculate(
                overviewUploads:
                [
                    CreateOverviewUpload(
                        UploadState.Completed,
                        releaseContainer,
                        CreateContainer(2, LinkCrypterContainerState.Created)
                    ),
                    CreateOverviewUpload(UploadState.Completed, releaseContainer),
                ]
            ),
            ReleaseProgressStepKind.LinkContainers
        );

        // Assert
        step.State.ShouldBe(ReleaseProgressStepState.Done);
        step.Count.ShouldBe(2);
    }

    [Test]
    public void Calculate_ContainerCreationFailed_ReturnsAttentionLinkContainersStepWithFailedCount()
    {
        // Act
        var step = GetStep(
            Calculate(
                overviewUploads:
                [
                    CreateOverviewUpload(
                        UploadState.Completed,
                        CreateContainer(1, LinkCrypterContainerState.Created),
                        CreateContainer(2, LinkCrypterContainerState.CreationFailed)
                    ),
                ]
            ),
            ReleaseProgressStepKind.LinkContainers
        );

        // Assert
        step.State.ShouldBe(ReleaseProgressStepState.Attention);
        step.Count.ShouldBe(1);
        step.TotalCount.ShouldBe(2);
    }

    [Test]
    public void Calculate_PostedLocationsExist_ReturnsDonePostedStepWithCount()
    {
        // Act
        var step = GetStep(Calculate(postedLocationCount: 2), ReleaseProgressStepKind.Posted);

        // Assert
        step.State.ShouldBe(ReleaseProgressStepState.Done);
        step.Count.ShouldBe(2);
    }

    [Test]
    public void Calculate_UploadsPostedAtSetWithoutPostedLocations_ReturnsDonePostedStep()
    {
        // Act
        var step = GetStep(
            Calculate(
                release: CreateRelease(
                    uploadsPostedAt: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
                )
            ),
            ReleaseProgressStepKind.Posted
        );

        // Assert
        step.State.ShouldBe(ReleaseProgressStepState.Done);
        step.Count.ShouldBe(0);
    }

    private static IReadOnlyList<ReleaseProgressStep> Calculate(
        ReleaseReadModel? release = null,
        ReleaseInfoReadModel? releaseInfo = null,
        ReleaseMetadataReadModel? releaseMetadata = null,
        IReadOnlyList<ArchiveConfigReadModel>? archiveConfigs = null,
        IReadOnlyList<ReleaseOverviewUploadReadModel>? overviewUploads = null,
        int postedLocationCount = 0
    ) =>
        ReleaseProgressStepCalculator.Calculate(
            release ?? CreateRelease(),
            releaseInfo,
            releaseMetadata,
            archiveConfigs ?? [],
            overviewUploads ?? [],
            postedLocationCount
        );

    private static ReleaseProgressStep GetStep(
        IReadOnlyList<ReleaseProgressStep> steps,
        ReleaseProgressStepKind kind
    ) => steps.Single(step => step.Kind == kind);

    private static ReleaseReadModel CreateRelease(
        int activeUploadConfigsCount = 0,
        int onlineUploadConfigsCount = 0,
        DateTime? uploadsPostedAt = null
    ) =>
        new(
            1,
            "Some.Release-GROUP",
            ReleaseType.Managed,
            ReleaseContentType.Movie,
            "de",
            1,
            "Group",
            "/releases/Some.Release-GROUP",
            activeUploadConfigsCount,
            onlineUploadConfigsCount,
            false,
            uploadsPostedAt
        );

    private static ReleaseInfoReadModel CreateReleaseInfo(string nfoDatabaseClassName) =>
        new(nfoDatabaseClassName, "Some.Release-GROUP", null, null, null, null, null, []);

    private static ReleaseMetadataReadModel CreateReleaseMetadata(
        string metadataDatabaseClassName
    ) => new(metadataDatabaseClassName, "Some Release", null, null, null, null);

    private static ArchiveConfigReadModel CreateArchiveConfig(
        params ArchiveConfigReadModel.ArchiveSummary[] archives
    ) =>
        new(
            1,
            "/archives",
            "RarArchiver",
            "RAR",
            null,
            null,
            100,
            false,
            false,
            ".rar",
            "RAR",
            archives,
            []
        );

    private static ArchiveConfigReadModel.ArchiveSummary CreateArchive(
        int archiveId,
        ArchiveState archiveState
    ) => new(archiveId, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), archiveState, 3, []);

    private static ReleaseOverviewUploadReadModel CreateOverviewUpload(
        UploadState uploadState,
        params ReleaseOverviewLinkCrypterLinkReadModel[] containers
    ) =>
        new(
            1,
            "Upload config",
            "Hoster",
            1,
            new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            null,
            uploadState,
            null,
            null,
            0,
            [],
            null,
            containers
        );

    private static ReleaseOverviewLinkCrypterLinkReadModel CreateContainer(
        int containerId,
        LinkCrypterContainerState state
    ) =>
        new(
            containerId,
            "Link crypter",
            "SomeLinkCrypter",
            $"https://crypter.test/{containerId}",
            LinkCrypterContainerScope.Release,
            state,
            new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            []
        );
}

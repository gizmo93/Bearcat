using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Pages.ManageReleaseCollections.ProgressSteps;
using Bearcat.Website.Shared.ProgressSteps;
using Shouldly;

namespace Bearcat.Website.UnitTest.Pages.ManageReleaseCollections.ProgressSteps;

public class ReleaseCollectionProgressStepServiceTest
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
                ReleaseCollectionProgressStepKind.Info,
                ReleaseCollectionProgressStepKind.Releases,
                ReleaseCollectionProgressStepKind.LinkContainers,
                ReleaseCollectionProgressStepKind.Images,
                ReleaseCollectionProgressStepKind.Posted,
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
                ProgressStepState.Pending,
                ProgressStepState.Pending,
                ProgressStepState.NotApplicable,
                ProgressStepState.NotApplicable,
                ProgressStepState.Pending,
            ]);
    }

    [Test]
    public void Calculate_MetadataExists_ReturnsDoneInfoStepWithMetadataDatabaseName()
    {
        // Act
        var step = GetStep(
            Calculate(releaseCollection: CreateReleaseCollection(metadata: CreateMetadata())),
            ReleaseCollectionProgressStepKind.Info
        );

        // Assert
        step.State.ShouldBe(ProgressStepState.Done);
        step.MetadataDatabaseName.ShouldBe("TheTVDB");
    }

    [Test]
    public void Calculate_ReleasesWithoutUploadConfigs_ReturnsPendingReleasesStep()
    {
        // Act
        var step = GetStep(
            Calculate(
                releaseCollection: CreateReleaseCollection(
                    releases: [CreateRelease(activeUploadConfigsCount: 0)]
                )
            ),
            ReleaseCollectionProgressStepKind.Releases
        );

        // Assert
        step.State.ShouldBe(ProgressStepState.Pending);
    }

    [Test]
    public void Calculate_NotAllReleasesWithUploadConfigsOnline_ReturnsAttentionReleasesStepWithCounts()
    {
        // Act
        var step = GetStep(
            Calculate(
                releaseCollection: CreateReleaseCollection(
                    releases:
                    [
                        CreateRelease(activeUploadConfigsCount: 2, onlineUploadConfigsCount: 2),
                        CreateRelease(activeUploadConfigsCount: 2, onlineUploadConfigsCount: 1),
                        CreateRelease(activeUploadConfigsCount: 1, onlineUploadConfigsCount: 0),
                        CreateRelease(activeUploadConfigsCount: 0),
                    ]
                )
            ),
            ReleaseCollectionProgressStepKind.Releases
        );

        // Assert
        step.State.ShouldBe(ProgressStepState.Attention);
        step.Count.ShouldBe(1);
        step.TotalCount.ShouldBe(3);
    }

    [Test]
    public void Calculate_AllReleasesWithUploadConfigsOnline_ReturnsDoneReleasesStepWithCounts()
    {
        // Act
        var step = GetStep(
            Calculate(
                releaseCollection: CreateReleaseCollection(
                    releases:
                    [
                        CreateRelease(activeUploadConfigsCount: 2, onlineUploadConfigsCount: 2),
                        CreateRelease(activeUploadConfigsCount: 1, onlineUploadConfigsCount: 1),
                        CreateRelease(activeUploadConfigsCount: 0),
                    ]
                )
            ),
            ReleaseCollectionProgressStepKind.Releases
        );

        // Assert
        step.State.ShouldBe(ProgressStepState.Done);
        step.Count.ShouldBe(2);
        step.TotalCount.ShouldBe(2);
    }

    [Test]
    public void Calculate_NoContainersButSharedLinkCrypters_ReturnsPendingLinkContainersStep()
    {
        // Act
        var step = GetStep(
            Calculate(
                releaseCollection: CreateReleaseCollection(
                    uploadSlots:
                    [
                        CreateUploadSlot(sharedLinkCrypters: []),
                        CreateUploadSlot(sharedLinkCrypters: [CreateSharedLinkCrypter()]),
                    ]
                )
            ),
            ReleaseCollectionProgressStepKind.LinkContainers
        );

        // Assert
        step.State.ShouldBe(ProgressStepState.Pending);
    }

    [Test]
    public void Calculate_NoContainersAndNoSharedLinkCrypters_ReturnsNotApplicableLinkContainersStep()
    {
        // Act
        var step = GetStep(
            Calculate(
                releaseCollection: CreateReleaseCollection(
                    uploadSlots: [CreateUploadSlot(sharedLinkCrypters: [])]
                )
            ),
            ReleaseCollectionProgressStepKind.LinkContainers
        );

        // Assert
        step.State.ShouldBe(ProgressStepState.NotApplicable);
    }

    [Test]
    public void Calculate_AllContainersCreated_ReturnsDoneLinkContainersStepWithCountOfAllSlots()
    {
        // Act
        var step = GetStep(
            Calculate(
                releaseCollection: CreateReleaseCollection(
                    uploadSlots:
                    [
                        CreateUploadSlot(
                            containers:
                            [
                                CreateContainer(1, LinkCrypterContainerState.Created),
                                CreateContainer(2, LinkCrypterContainerState.Created),
                            ]
                        ),
                        CreateUploadSlot(
                            containers: [CreateContainer(3, LinkCrypterContainerState.Created)]
                        ),
                    ]
                )
            ),
            ReleaseCollectionProgressStepKind.LinkContainers
        );

        // Assert
        step.State.ShouldBe(ProgressStepState.Done);
        step.Count.ShouldBe(3);
    }

    [Test]
    public void Calculate_ContainerCreationFailed_ReturnsAttentionLinkContainersStepWithFailedCount()
    {
        // Act
        var step = GetStep(
            Calculate(
                releaseCollection: CreateReleaseCollection(
                    uploadSlots:
                    [
                        CreateUploadSlot(
                            containers: [CreateContainer(1, LinkCrypterContainerState.Created)]
                        ),
                        CreateUploadSlot(
                            containers:
                            [
                                CreateContainer(2, LinkCrypterContainerState.CreationFailed),
                            ]
                        ),
                    ]
                )
            ),
            ReleaseCollectionProgressStepKind.LinkContainers
        );

        // Assert
        step.State.ShouldBe(ProgressStepState.Attention);
        step.Count.ShouldBe(1);
        step.TotalCount.ShouldBe(2);
    }

    [Test]
    public void Calculate_ImageUploadFailed_ReturnsAttentionImagesStepWithFailedCount()
    {
        // Act
        var step = GetStep(
            Calculate(
                imageUploads:
                [
                    CreateImageUpload(UploadState.Uploading),
                    CreateImageUpload(UploadState.Failed),
                    CreateImageUpload(UploadState.Completed),
                ]
            ),
            ReleaseCollectionProgressStepKind.Images
        );

        // Assert
        step.State.ShouldBe(ProgressStepState.Attention);
        step.Count.ShouldBe(1);
        step.TotalCount.ShouldBe(3);
    }

    [TestCase(UploadState.WaitingForArchive)]
    [TestCase(UploadState.Pending)]
    [TestCase(UploadState.Uploading)]
    [TestCase(UploadState.CancellationRequested)]
    public void Calculate_ImageUploadRunning_ReturnsInProgressImagesStep(UploadState uploadState)
    {
        // Act
        var step = GetStep(
            Calculate(
                imageUploads:
                [
                    CreateImageUpload(UploadState.Completed),
                    CreateImageUpload(uploadState),
                ]
            ),
            ReleaseCollectionProgressStepKind.Images
        );

        // Assert
        step.State.ShouldBe(ProgressStepState.InProgress);
    }

    [Test]
    public void Calculate_ImageUploadsCompleted_ReturnsDoneImagesStepWithCompletedCount()
    {
        // Act
        var step = GetStep(
            Calculate(
                imageUploads:
                [
                    CreateImageUpload(UploadState.Completed),
                    CreateImageUpload(UploadState.Completed),
                    CreateImageUpload(null),
                ]
            ),
            ReleaseCollectionProgressStepKind.Images
        );

        // Assert
        step.State.ShouldBe(ProgressStepState.Done);
        step.Count.ShouldBe(2);
    }

    [Test]
    public void Calculate_ImageUploadConfigsWithoutCompletedUploads_ReturnsPendingImagesStep()
    {
        // Act
        var step = GetStep(
            Calculate(
                imageUploads: [CreateImageUpload(null), CreateImageUpload(UploadState.Canceled)]
            ),
            ReleaseCollectionProgressStepKind.Images
        );

        // Assert
        step.State.ShouldBe(ProgressStepState.Pending);
    }

    [Test]
    public void Calculate_PostedLocationsExist_ReturnsDonePostedStepWithCount()
    {
        // Act
        var step = GetStep(
            Calculate(postedLocationCount: 2),
            ReleaseCollectionProgressStepKind.Posted
        );

        // Assert
        step.State.ShouldBe(ProgressStepState.Done);
        step.Count.ShouldBe(2);
    }

    [Test]
    public void Calculate_UploadsPostedAtSetWithoutPostedLocations_ReturnsDonePostedStep()
    {
        // Act
        var step = GetStep(
            Calculate(
                releaseCollection: CreateReleaseCollection(
                    uploadsPostedAt: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
                )
            ),
            ReleaseCollectionProgressStepKind.Posted
        );

        // Assert
        step.State.ShouldBe(ProgressStepState.Done);
        step.Count.ShouldBe(0);
    }

    private static IReadOnlyList<ReleaseCollectionProgressStep> Calculate(
        ReleaseCollectionDetailReadModel? releaseCollection = null,
        IReadOnlyList<CollectionImageUploadReadModel>? imageUploads = null,
        int postedLocationCount = 0
    ) =>
        ReleaseCollectionProgressStepService.BuildReleaseCollectionProgressSteps(
            releaseCollection ?? CreateReleaseCollection(),
            imageUploads ?? [],
            postedLocationCount
        );

    private static ReleaseCollectionProgressStep GetStep(
        IReadOnlyList<ReleaseCollectionProgressStep> steps,
        ReleaseCollectionProgressStepKind kind
    ) => steps.Single(step => step.Kind == kind);

    private static ReleaseCollectionDetailReadModel CreateReleaseCollection(
        IReadOnlyList<CollectionUploadSlotReadModel>? uploadSlots = null,
        IReadOnlyList<ReleaseCollectionReleaseReadModel>? releases = null,
        ReleaseCollectionMetadataReadModel? metadata = null,
        DateTime? uploadsPostedAt = null
    ) =>
        new(
            1,
            "Some Series S01",
            "some.series.s01",
            ReleaseContentType.TvShowEpisode,
            1,
            "Group",
            "de",
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            uploadsPostedAt,
            uploadSlots ?? [],
            releases ?? [],
            metadata
        );

    private static ReleaseCollectionMetadataReadModel CreateMetadata() =>
        new("TheTVDB", "Some Series", null, null, null);

    private static ReleaseCollectionReleaseReadModel CreateRelease(
        int activeUploadConfigsCount = 0,
        int onlineUploadConfigsCount = 0
    ) =>
        new(
            1,
            "Some.Series.S01E01-GROUP",
            ReleaseType.Managed,
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            activeUploadConfigsCount,
            onlineUploadConfigsCount,
            null,
            []
        );

    private static CollectionUploadSlotReadModel CreateUploadSlot(
        IReadOnlyList<CollectionUploadSlotLinkCrypterReadModel>? sharedLinkCrypters = null,
        IReadOnlyList<CollectionUploadSlotContainerReadModel>? containers = null
    ) =>
        new(
            1,
            "rapidgator",
            "Rapidgator",
            false,
            CollectionUploadSlotPasswordPolicy.Ignore,
            null,
            1,
            1,
            sharedLinkCrypters ?? [],
            containers ?? []
        );

    private static CollectionUploadSlotLinkCrypterReadModel CreateSharedLinkCrypter() =>
        new(1, "Filecrypt", true, null, false, false, false, 1);

    private static CollectionUploadSlotContainerReadModel CreateContainer(
        int linkCrypterContainerId,
        LinkCrypterContainerState state
    ) =>
        new(
            linkCrypterContainerId,
            "Filecrypt",
            "https://filecrypt.example/container",
            state,
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            1,
            []
        );

    private static CollectionImageUploadReadModel CreateImageUpload(UploadState? uploadState) =>
        new(1, "Cover", 1, "ImgBox", null, null, null, uploadState, [], []);
}

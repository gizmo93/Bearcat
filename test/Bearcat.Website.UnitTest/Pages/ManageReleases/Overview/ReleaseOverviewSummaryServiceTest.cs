using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Pages.ManageReleases.Overview;
using Shouldly;

namespace Bearcat.Website.UnitTest.Pages.ManageReleases.Overview;

public class ReleaseOverviewSummaryServiceTest
{
    [Test]
    public void BuildReleaseOverviewSummary_NoUploads_ReturnsEmptySummary()
    {
        // Act
        var summary = ReleaseOverviewSummaryService.BuildReleaseOverviewSummary([]);

        // Assert
        summary.ShouldBe(
            new ReleaseOverviewSummary(
                0,
                0,
                0,
                0,
                null,
                new ArchivePasswordSummary(ArchivePasswordSummaryKind.None, null)
            )
        );
    }

    [Test]
    public void BuildReleaseOverviewSummary_MixedOnlineStates_CountsOnlyOnlineUploads()
    {
        // Arrange
        IReadOnlyList<ReleaseOverviewUploadReadModel> uploads =
        [
            CreateUpload(1, uploadId: 10, onlineState: OnlineState.Online),
            CreateUpload(2, uploadId: 11, onlineState: OnlineState.PartiallyOnline),
            CreateUpload(3, uploadId: 12, onlineState: OnlineState.Offline),
            CreateUpload(4, uploadId: null, onlineState: OnlineState.Online),
        ];

        // Act
        var summary = ReleaseOverviewSummaryService.BuildReleaseOverviewSummary(uploads);

        // Assert
        summary.OnlineHosterCount.ShouldBe(1);
        summary.HosterCount.ShouldBe(4);
    }

    [Test]
    public void BuildReleaseOverviewSummary_SharedCollectionContainer_CountsContainersOnce()
    {
        // Arrange
        var sharedContainer = CreateContainer(1, "Filecrypt", "https://a");
        IReadOnlyList<ReleaseOverviewUploadReadModel> uploads =
        [
            CreateUpload(
                1,
                uploadId: 10,
                containers:
                [
                    sharedContainer,
                    CreateContainer(
                        2,
                        "HideCx",
                        "https://b",
                        LinkCrypterContainerState.CreationFailed
                    ),
                ]
            ),
            CreateUpload(2, uploadId: 11, containers: [sharedContainer]),
        ];

        // Act
        var summary = ReleaseOverviewSummaryService.BuildReleaseOverviewSummary(uploads);

        // Assert
        summary.CreatedLinkContainerCount.ShouldBe(1);
        summary.LinkContainerCount.ShouldBe(2);
    }

    [Test]
    public void BuildReleaseOverviewSummary_UploadsWithAndWithoutUploadedAt_ReturnsNewestTimestamp()
    {
        // Arrange
        IReadOnlyList<ReleaseOverviewUploadReadModel> uploads =
        [
            CreateUpload(
                1,
                uploadId: 10,
                createdAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local),
                uploadedAt: new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Local)
            ),
            CreateUpload(
                2,
                uploadId: 11,
                createdAt: new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Local)
            ),
            CreateUpload(3, uploadId: null),
        ];

        // Act
        var summary = ReleaseOverviewSummaryService.BuildReleaseOverviewSummary(uploads);

        // Assert
        summary.LatestUploadAt.ShouldBe(new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Local));
    }

    [Test]
    public void BuildReleaseOverviewSummary_NoUploadHasPassword_ReturnsNoneArchivePassword()
    {
        // Arrange
        IReadOnlyList<ReleaseOverviewUploadReadModel> uploads =
        [
            CreateUpload(1, uploadId: 10),
            CreateUpload(2, uploadId: 11, archivePassword: " "),
        ];

        // Act
        var summary = ReleaseOverviewSummaryService.BuildReleaseOverviewSummary(uploads);

        // Assert
        summary.ArchivePassword.ShouldBe(
            new ArchivePasswordSummary(ArchivePasswordSummaryKind.None, null)
        );
    }

    [Test]
    public void BuildReleaseOverviewSummary_AllUploadsShareOnePassword_ReturnsSamePassword()
    {
        // Arrange
        IReadOnlyList<ReleaseOverviewUploadReadModel> uploads =
        [
            CreateUpload(1, uploadId: 10, archivePassword: "secret"),
            CreateUpload(2, uploadId: 11, archivePassword: "secret"),
            CreateUpload(3, uploadId: null),
        ];

        // Act
        var summary = ReleaseOverviewSummaryService.BuildReleaseOverviewSummary(uploads);

        // Assert
        summary.ArchivePassword.ShouldBe(
            new ArchivePasswordSummary(ArchivePasswordSummaryKind.SamePassword, "secret")
        );
    }

    [TestCase("other")]
    [TestCase(null)]
    public void BuildReleaseOverviewSummary_UploadsWithDifferentPasswords_ReturnsVariesPerHoster(
        string? otherPassword
    )
    {
        // Arrange
        IReadOnlyList<ReleaseOverviewUploadReadModel> uploads =
        [
            CreateUpload(1, uploadId: 10, archivePassword: "secret"),
            CreateUpload(2, uploadId: 11, archivePassword: otherPassword),
        ];

        // Act
        var summary = ReleaseOverviewSummaryService.BuildReleaseOverviewSummary(uploads);

        // Assert
        summary.ArchivePassword.ShouldBe(
            new ArchivePasswordSummary(ArchivePasswordSummaryKind.VariesPerHoster, null)
        );
    }

    [Test]
    public void GroupCreatedContainerUrlsByLinkCrypter_ReturnsCreatedUrlsPerCrypterInRowOrder()
    {
        // Arrange
        var sharedContainer = CreateContainer(3, "Filecrypt", "https://filecrypt/shared");
        IReadOnlyList<ReleaseOverviewUploadReadModel> uploads =
        [
            CreateUpload(
                1,
                uploadId: 10,
                containers:
                [
                    CreateContainer(1, "Filecrypt", "https://filecrypt/1"),
                    CreateContainer(2, "HideCx", "https://hidecx/1"),
                    sharedContainer,
                ]
            ),
            CreateUpload(
                2,
                uploadId: 11,
                containers:
                [
                    CreateContainer(4, "HideCx", "https://hidecx/2"),
                    CreateContainer(5, "Filecrypt", "https://filecrypt/2"),
                    CreateContainer(
                        6,
                        "Filecrypt",
                        "https://filecrypt/failed",
                        LinkCrypterContainerState.CreationFailed
                    ),
                    sharedContainer,
                ]
            ),
        ];

        // Act
        var groups = ReleaseOverviewSummaryService.GroupCreatedContainerUrlsByLinkCrypter(uploads);

        // Assert
        groups.Count.ShouldBe(2);
        groups[0].LinkCrypterRegistrationName.ShouldBe("Filecrypt");
        groups[0]
            .ContainerUrls.ShouldBe([
                "https://filecrypt/1",
                "https://filecrypt/shared",
                "https://filecrypt/2",
            ]);
        groups[1].LinkCrypterRegistrationName.ShouldBe("HideCx");
        groups[1].ContainerUrls.ShouldBe(["https://hidecx/1", "https://hidecx/2"]);
    }

    [Test]
    public void GroupCreatedContainerUrlsByLinkCrypter_OnlyFailedContainers_ReturnsEmptyUrlList()
    {
        // Arrange
        IReadOnlyList<ReleaseOverviewUploadReadModel> uploads =
        [
            CreateUpload(
                1,
                uploadId: 10,
                containers:
                [
                    CreateContainer(
                        1,
                        "Filecrypt",
                        string.Empty,
                        LinkCrypterContainerState.CreationFailed
                    ),
                ]
            ),
        ];

        // Act
        var groups = ReleaseOverviewSummaryService.GroupCreatedContainerUrlsByLinkCrypter(uploads);

        // Assert
        groups.Count.ShouldBe(1);
        groups[0].ContainerUrls.ShouldBeEmpty();
    }

    private static ReleaseOverviewUploadReadModel CreateUpload(
        int uploadConfigId,
        int? uploadId,
        OnlineState? onlineState = null,
        DateTime? createdAt = null,
        DateTime? uploadedAt = null,
        string? archivePassword = null,
        IReadOnlyList<ReleaseOverviewLinkCrypterLinkReadModel>? containers = null
    )
    {
        return new ReleaseOverviewUploadReadModel(
            UploadConfigId: uploadConfigId,
            UploadConfigName: $"Config {uploadConfigId}",
            HosterRegistrationName: "Hoster",
            UploadId: uploadId,
            CreatedAt: createdAt,
            UploadedAt: uploadedAt,
            UploadState: uploadId is null ? null : UploadState.Completed,
            OnlineState: onlineState,
            NotFullyOnlineSince: null,
            LinkCount: 0,
            ErrorMessages: [],
            ArchivePassword: archivePassword,
            LinkCrypterLinks: containers ?? []
        );
    }

    private static ReleaseOverviewLinkCrypterLinkReadModel CreateContainer(
        int linkCrypterContainerId,
        string linkCrypterRegistrationName,
        string containerUrl,
        LinkCrypterContainerState state = LinkCrypterContainerState.Created
    )
    {
        return new ReleaseOverviewLinkCrypterLinkReadModel(
            LinkCrypterContainerId: linkCrypterContainerId,
            LinkCrypterRegistrationName: linkCrypterRegistrationName,
            LinkCrypterClassName: "LinkCrypterClass",
            ContainerUrl: containerUrl,
            Scope: LinkCrypterContainerScope.Release,
            State: state,
            CreatedAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local),
            Errors: []
        );
    }
}

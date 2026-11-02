using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.LinkCrypter.Results;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageLinkCrypterContainers;
using Bearcat.Domain.UseCases.ManageLinkCrypterContainers.MultiUploadContainers;
using Bearcat.Domain.UseCases.ManageLinkCrypterContainers.ReleaseAllHosters;
using Bearcat.Domain.UseCases.ManageNotifications;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageLinkCrypterContainers.ReleaseAllHosters;

public class ReleaseAllHostersLinkCrypterContainerServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string LinkCrypterClassName = "TestCrypter";
    private const string SerializedConfig = "{\"apiKey\":\"secret\"}";
    private const string ReleaseName = "Bearcat.Release.AllHosters";

    private static readonly string[] ExpectedLinksOfFirstReupload = ["https://first.test/new"];
    private static readonly string[] ExpectedLinksOfSecondUpload = ["https://second.test/a"];

    private Mock<ILinkCrypter> linkCrypterMock = null!;
    private Mock<ILinkCrypterConfig> linkCrypterConfigMock = null!;
    private ReleaseAllHostersLinkCrypterContainerService service = null!;

    [SetUp]
    public void Setup()
    {
        linkCrypterConfigMock = new Mock<ILinkCrypterConfig>(MockBehavior.Strict);
        linkCrypterMock = new Mock<ILinkCrypter>(MockBehavior.Strict);
        linkCrypterMock
            .Setup(c => c.DeserializeConfig(SerializedConfig))
            .Returns(linkCrypterConfigMock.Object);

        var linkCrypterFactoryMock = new Mock<ILinkCrypterFactory>(MockBehavior.Strict);
        linkCrypterFactoryMock
            .Setup(f => f.Get(LinkCrypterClassName))
            .Returns(linkCrypterMock.Object);

        var repository = new LinkCrypterContainerCreationWriteRepository(DbContext);
        var notificationService = new NotificationService(
            repository: new NotificationRepository(DbContext),
            timeProvider: CreateTimeProvider(),
            configurationProvider: CreateNotificationConfigurationProvider()
        );

        service = new ReleaseAllHostersLinkCrypterContainerService(
            repository,
            Mock.Of<ILogger<ReleaseAllHostersLinkCrypterContainerService>>(),
            CreateTimeProvider(),
            new MultiUploadLinkCrypterContainerService(
                repository,
                new LinkCrypterCallService(
                    linkCrypterFactoryMock.Object,
                    NoOpSecretProtector.Instance
                ),
                CreateTimeProvider(),
                notificationService,
                Mock.Of<ILogger<MultiUploadLinkCrypterContainerService>>()
            )
        );
    }

    [Test]
    public async Task UpdateOutdatedContainersAsync_TwoMemberUploadConfigs_CreatesOneContainerWithOneMirrorPerUploadConfigInIdOrder()
    {
        // Arrange
        var seed = await AddReleaseWithTwoMemberUploadConfigsAsync();
        IReadOnlyDictionary<int, IReadOnlyList<string>>? sentLinksPerMirrorNumber = null;
        linkCrypterMock
            .Setup(c =>
                c.CreateContainerAsync(
                    linkCrypterConfigMock.Object,
                    ReleaseName,
                    "container-secret",
                    It.IsAny<IReadOnlyDictionary<int, IReadOnlyList<string>>>(),
                    true,
                    true,
                    true,
                    CancellationToken.None
                )
            )
            .Callback<
                ILinkCrypterConfig,
                string,
                string?,
                IReadOnlyDictionary<int, IReadOnlyList<string>>,
                bool,
                bool,
                bool,
                CancellationToken
            >(
                (_, _, _, linksPerMirrorNumber, _, _, _, _) =>
                    sentLinksPerMirrorNumber = linksPerMirrorNumber
            )
            .ReturnsAsync(
                new CreateContainerResult(true, "https://crypter.test/all", "all-hosters", [])
            );

        // Act
        await service.UpdateOutdatedContainersAsync(CancellationToken.None);

        // Assert
        sentLinksPerMirrorNumber.ShouldNotBeNull();
        sentLinksPerMirrorNumber.Count.ShouldBe(2);
        sentLinksPerMirrorNumber[1].ShouldBe(["https://first.test/a", "https://first.test/b"]);
        sentLinksPerMirrorNumber[2].ShouldBe(["https://second.test/a"]);

        DbContext.ChangeTracker.Clear();
        var container = await DbContext
            .LinkCrypterContainers.Include(c => c.SourceUploads)
            .SingleAsync();
        container.Scope.ShouldBe(LinkCrypterContainerScope.ReleaseAllHosters);
        container.ReleaseId.ShouldBe(seed.ReleaseId);
        container.LinkCrypterRegistrationId.ShouldBe(seed.LinkCrypterRegistrationId);
        container.UploadId.ShouldBeNull();
        container.ContainerUrl.ShouldBe("https://crypter.test/all");
        container.State.ShouldBe(LinkCrypterContainerState.Created);
        container
            .SourceUploads.Select(source => source.UploadId)
            .Order()
            .ShouldBe([seed.FirstUploadId, seed.SecondUploadId]);
    }

    [Test]
    public async Task UpdateOutdatedContainersAsync_CompletedReuploadOfOneMember_UpdatesExistingContainerWithReplacedMirror()
    {
        // Arrange
        var seed = await AddReleaseWithTwoMemberUploadConfigsAsync();
        var containerId = await AddExistingContainerAsync(
            seed,
            [seed.FirstUploadId, seed.SecondUploadId]
        );
        var reuploadId = await AddCompletedUploadAsync(
            seed.FirstUploadConfigId,
            "https://first.test/new"
        );
        linkCrypterMock
            .Setup(c =>
                c.UpdateContainerAsync(
                    linkCrypterConfigMock.Object,
                    "https://crypter.test/existing",
                    "existing-reference",
                    "container-secret",
                    It.Is<IReadOnlyDictionary<int, IReadOnlyList<string>>>(linksPerMirrorNumber =>
                        linksPerMirrorNumber.Count == 2
                        && linksPerMirrorNumber[1].SequenceEqual(ExpectedLinksOfFirstReupload)
                        && linksPerMirrorNumber[2].SequenceEqual(ExpectedLinksOfSecondUpload)
                    ),
                    true,
                    true,
                    true,
                    CancellationToken.None
                )
            )
            .ReturnsAsync(new UpdateContainerResult(UpdateContainerOutcome.Success, null));

        // Act
        await service.UpdateOutdatedContainersAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var container = await DbContext
            .LinkCrypterContainers.Include(c => c.SourceUploads)
            .SingleAsync();
        container.Id.ShouldBe(containerId);
        container.ContainerUrl.ShouldBe("https://crypter.test/existing");
        container.State.ShouldBe(LinkCrypterContainerState.Created);
        container
            .SourceUploads.Select(source => source.UploadId)
            .Order()
            .ShouldBe(new[] { seed.SecondUploadId, reuploadId }.Order());
    }

    [Test]
    public async Task UpdateOutdatedContainersAsync_MemberSettingsDiffer_AddsInvalidContainerWithoutCallingCrypter()
    {
        // Arrange
        var seed = await AddReleaseWithTwoMemberUploadConfigsAsync();
        var secondLinkCrypter = await DbContext.UploadConfigLinkCrypters.SingleAsync(linkCrypter =>
            linkCrypter.UploadConfigId == seed.SecondUploadConfigId
        );
        secondLinkCrypter.Password = "other-secret";
        await DbContext.SaveChangesAsync();

        // Act
        await service.UpdateOutdatedContainersAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var container = await DbContext.LinkCrypterContainers.SingleAsync();
        container.State.ShouldBe(LinkCrypterContainerState.CreationFailed);
        container.ContainerUrl.ShouldBeEmpty();
        container.Errors.ShouldBe(["Link crypter settings differ across upload configs."]);
        var notification = await DbContext.Notifications.SingleAsync();
        notification.NotificationKind.ShouldBe(
            NotificationKind.ReleaseAllHostersLinkCrypterContainerInvalid
        );
        notification.LinkCrypterContainerId.ShouldBe(container.Id);
        linkCrypterMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task UpdateOutdatedContainersAsync_ContainerNotFoundAtCrypter_RecreatesContainerAndResetsUploadsPostedAt()
    {
        // Arrange
        var seed = await AddReleaseWithTwoMemberUploadConfigsAsync();
        var containerId = await AddExistingContainerAsync(seed, [seed.FirstUploadId]);
        var release = await DbContext.Releases.SingleAsync(r => r.Id == seed.ReleaseId);
        release.UploadsPostedAt = DateTime.UtcNow;
        await DbContext.SaveChangesAsync();
        linkCrypterMock
            .Setup(c =>
                c.UpdateContainerAsync(
                    linkCrypterConfigMock.Object,
                    "https://crypter.test/existing",
                    "existing-reference",
                    "container-secret",
                    It.IsAny<IReadOnlyDictionary<int, IReadOnlyList<string>>>(),
                    true,
                    true,
                    true,
                    CancellationToken.None
                )
            )
            .ReturnsAsync(
                new UpdateContainerResult(UpdateContainerOutcome.ContainerNotFound, "gone")
            );
        linkCrypterMock
            .Setup(c =>
                c.CreateContainerAsync(
                    linkCrypterConfigMock.Object,
                    ReleaseName,
                    "container-secret",
                    It.Is<IReadOnlyDictionary<int, IReadOnlyList<string>>>(linksPerMirrorNumber =>
                        linksPerMirrorNumber.Count == 2
                    ),
                    true,
                    true,
                    true,
                    CancellationToken.None
                )
            )
            .ReturnsAsync(
                new CreateContainerResult(true, "https://crypter.test/recreated", "new-ref", [])
            );

        // Act
        await service.UpdateOutdatedContainersAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var container = await DbContext.LinkCrypterContainers.SingleAsync();
        container.Id.ShouldBe(containerId);
        container.ContainerUrl.ShouldBe("https://crypter.test/recreated");
        container.ExternalReference.ShouldBe("new-ref");
        container.State.ShouldBe(LinkCrypterContainerState.Created);
        (
            await DbContext.Releases.SingleAsync(r => r.Id == seed.ReleaseId)
        ).UploadsPostedAt.ShouldBeNull();
        var notification = await DbContext.Notifications.SingleAsync();
        notification.NotificationKind.ShouldBe(NotificationKind.LinkCrypterContainerRecreated);
        notification.Message.ShouldContain("https://crypter.test/recreated");
    }

    private async Task<ReleaseSeed> AddReleaseWithTwoMemberUploadConfigsAsync()
    {
        var release = new Release
        {
            Name = ReleaseName,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/tmp/release",
            ReleaseGroup = new ReleaseGroup
            {
                Name = "Managed releases",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
        };
        var linkCrypterRegistration = new LinkCrypterRegistration
        {
            Name = "Crypter",
            LinkCrypterClassName = LinkCrypterClassName,
            SerializedConfig = SerializedConfig,
            IsActive = true,
        };
        var firstUploadConfig = CreateMemberUploadConfig(
            release,
            linkCrypterRegistration,
            "First hoster"
        );
        var secondUploadConfig = CreateMemberUploadConfig(
            release,
            linkCrypterRegistration,
            "Second hoster"
        );
        DbContext.UploadConfigs.AddRange(firstUploadConfig, secondUploadConfig);
        await DbContext.SaveChangesAsync();

        var firstUploadId = await AddCompletedUploadAsync(
            firstUploadConfig.Id,
            "https://first.test/b",
            "https://first.test/a"
        );
        var secondUploadId = await AddCompletedUploadAsync(
            secondUploadConfig.Id,
            "https://second.test/a"
        );

        return new ReleaseSeed(
            ReleaseId: release.Id,
            LinkCrypterRegistrationId: linkCrypterRegistration.Id,
            FirstUploadConfigId: firstUploadConfig.Id,
            SecondUploadConfigId: secondUploadConfig.Id,
            FirstUploadId: firstUploadId,
            SecondUploadId: secondUploadId
        );
    }

    private static UploadConfig CreateMemberUploadConfig(
        Release release,
        LinkCrypterRegistration linkCrypterRegistration,
        string name
    )
    {
        return new UploadConfig
        {
            UploadSource = UploadSource.ArchiveFiles,
            Release = release,
            ArchiveConfig = new ArchiveConfig
            {
                Release = release,
                Name = name,
                ArchiveFilesBasePath = "/tmp/archive",
                ArchiverName = "zip",
                ArchiveFileSizeMb = 512,
            },
            HosterRegistration = new HosterRegistration
            {
                Name = name,
                SerializedConfig = "{}",
                HosterClassName = "TestHoster",
                IsActive = true,
            },
            Name = name,
            LinkCrypters =
            [
                new UploadConfigLinkCrypter
                {
                    LinkCrypterRegistration = linkCrypterRegistration,
                    ContainerScope = LinkCrypterContainerScope.ReleaseAllHosters,
                    Password = "container-secret",
                },
            ],
        };
    }

    private async Task<int> AddCompletedUploadAsync(int uploadConfigId, params string[] links)
    {
        var uploadConfig = await DbContext.UploadConfigs.SingleAsync(uc => uc.Id == uploadConfigId);
        var upload = new Upload
        {
            UploadConfigId = uploadConfigId,
            HosterRegistrationId = uploadConfig.HosterRegistrationId,
            CreatedAt = DateTime.UtcNow,
            UploadedAt = DateTime.UtcNow,
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Online,
            UploadedFiles = links
                .Select(link => new UploadedFile
                {
                    HosterFileLink = link,
                    VideoFileRelativePath = link,
                    ErrorMessages = [],
                    OnlineState = OnlineState.Online,
                    CreatedAt = DateTime.UtcNow,
                    CheckedAt = DateTime.UtcNow,
                })
                .ToList(),
            ErrorMessages = [],
        };

        DbContext.Uploads.Add(upload);
        await DbContext.SaveChangesAsync();
        return upload.Id;
    }

    private async Task<int> AddExistingContainerAsync(
        ReleaseSeed seed,
        IReadOnlyList<int> uploadIds
    )
    {
        var container = new LinkCrypterContainer
        {
            Scope = LinkCrypterContainerScope.ReleaseAllHosters,
            ReleaseId = seed.ReleaseId,
            LinkCrypterRegistrationId = seed.LinkCrypterRegistrationId,
            ContainerUrl = "https://crypter.test/existing",
            ExternalReference = "existing-reference",
            Password = "container-secret",
            State = LinkCrypterContainerState.Created,
            Errors = [],
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            SourceUploads = uploadIds
                .Select(uploadId => new LinkCrypterContainerSourceUpload { UploadId = uploadId })
                .ToList(),
        };

        DbContext.LinkCrypterContainers.Add(container);
        await DbContext.SaveChangesAsync();
        return container.Id;
    }

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }

    private sealed record ReleaseSeed(
        int ReleaseId,
        int LinkCrypterRegistrationId,
        int FirstUploadConfigId,
        int SecondUploadConfigId,
        int FirstUploadId,
        int SecondUploadId
    );
}

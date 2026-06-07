using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.LinkCrypter.Results;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageLinkCrypterContainers;
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

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageLinkCrypterContainers;

public class LinkCrypterContainerServiceTest : BearcatIntegrationTest
{
    private const string LinkCrypterClassName = "TestCrypter";
    private const string SerializedConfig = "{\"apiKey\":\"secret\"}";

    private BearcatDbContext dbContext = null!;
    private Mock<ILinkCrypter> linkCrypterMock = null!;
    private Mock<ILinkCrypterConfig> linkCrypterConfigMock = null!;
    private Mock<ILinkCrypterFactory> linkCrypterFactoryMock = null!;
    private LinkCrypterContainerService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        linkCrypterConfigMock = new Mock<ILinkCrypterConfig>(MockBehavior.Strict);
        linkCrypterMock = new Mock<ILinkCrypter>(MockBehavior.Strict);
        linkCrypterMock
            .Setup(c => c.DeserializeConfig(SerializedConfig))
            .Returns(linkCrypterConfigMock.Object);

        linkCrypterFactoryMock = new Mock<ILinkCrypterFactory>(MockBehavior.Strict);
        linkCrypterFactoryMock
            .Setup(f => f.Get(LinkCrypterClassName))
            .Returns(linkCrypterMock.Object);

        service = new LinkCrypterContainerService(
            new LinkCrypterContainerCreationWriteRepository(dbContext),
            Mock.Of<ILogger<LinkCrypterContainerService>>(),
            linkCrypterFactoryMock.Object,
            CreateTimeProvider(),
            new NotificationService(new NotificationRepository(dbContext), CreateTimeProvider()),
            NoOpSecretProtector.Instance
        );
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task CreateMissingLinkCrypterContainersAsync_UploadHasMissingContainer_CreatesContainer()
    {
        // Arrange
        var seed = await AddUploadWithMissingContainerAsync();
        linkCrypterMock
            .Setup(c =>
                c.CreateContainerAsync(
                    linkCrypterConfigMock.Object,
                    "Bearcat.Release.001",
                    "container-secret",
                    It.Is<IReadOnlyList<string>>(links =>
                        links.SequenceEqual(
                            new[] { "https://hoster.test/a", "https://hoster.test/b" }
                        )
                    ),
                    true,
                    true,
                    true,
                    CancellationToken.None
                )
            )
            .ReturnsAsync(
                new CreateContainerResult(true, "https://crypter.test/container", "abc", [])
            );

        // Act
        await service.CreateMissingLinkCrypterContainersAsync(CancellationToken.None);

        // Assert
        var result = await dbContext.LinkCrypterContainers.SingleAsync();

        result.ShouldNotBeNull();
        result.UploadId.ShouldBe(seed.UploadId);
        result.UploadConfigLinkCrypterId.ShouldBe(seed.UploadConfigLinkCrypterId);
        result.ContainerUrl.ShouldBe("https://crypter.test/container");
        result.ExternalReference.ShouldBe("abc");
        result.Password.ShouldBe("container-secret");
        result.EnableCaptcha.ShouldBeTrue();
        result.EnableContainerDownload.ShouldBeTrue();
        result.EnableClickAndLoad.ShouldBeTrue();
        result.State.ShouldBe(LinkCrypterContainerState.Created);
        result.Errors.ShouldBeEmpty();
        linkCrypterFactoryMock.Verify(f => f.Get(LinkCrypterClassName), Times.Once);
        linkCrypterMock.Verify(c => c.DeserializeConfig(SerializedConfig), Times.Once);
    }

    [Test]
    public async Task CreateMissingLinkCrypterContainersAsync_CreateContainerFails_PersistsFailedContainerAndNotification()
    {
        // Arrange
        var seed = await AddUploadWithMissingContainerAsync();
        linkCrypterMock
            .Setup(c =>
                c.CreateContainerAsync(
                    linkCrypterConfigMock.Object,
                    It.IsAny<string>(),
                    "container-secret",
                    It.IsAny<IReadOnlyList<string>>(),
                    true,
                    true,
                    true,
                    CancellationToken.None
                )
            )
            .ReturnsAsync(
                new CreateContainerResult(false, null, null, ["Could not create container"])
            );

        // Act
        await service.CreateMissingLinkCrypterContainersAsync(CancellationToken.None);

        // Assert
        var result = await dbContext
            .LinkCrypterContainers.Include(c => c.Notifications)
            .SingleAsync();

        result.ShouldNotBeNull();
        result.UploadId.ShouldBe(seed.UploadId);
        result.State.ShouldBe(LinkCrypterContainerState.CreationFailed);
        result.ContainerUrl.ShouldBeEmpty();
        result.Errors.ShouldBe(["Could not create container"]);
        result.Notifications.Single().NotificationType.ShouldBe(NotificationType.Error);
        result
            .Notifications.Single()
            .Message.ShouldContain("Failed to create link crypter container");
        linkCrypterFactoryMock.Verify(f => f.Get(LinkCrypterClassName), Times.Once);
        linkCrypterMock.Verify(c => c.DeserializeConfig(SerializedConfig), Times.Once);
    }

    [Test]
    public async Task CreateMissingLinkCrypterContainersAsync_ActiveContainerAlreadyExists_DoesNotCreateContainer()
    {
        // Arrange
        var seed = await AddUploadWithMissingContainerAsync();
        dbContext.LinkCrypterContainers.Add(
            new LinkCrypterContainer
            {
                Scope = LinkCrypterContainerScope.Release,
                UploadId = seed.UploadId,
                UploadConfigLinkCrypterId = seed.UploadConfigLinkCrypterId,
                LinkCrypterRegistrationId = seed.LinkCrypterRegistrationId,
                ContainerUrl = "https://crypter.test/existing",
                ExternalReference = "existing",
                Password = "container-secret",
                State = LinkCrypterContainerState.Created,
                Errors = [],
                CreatedAt = DateTime.UtcNow,
            }
        );
        await dbContext.SaveChangesAsync();

        // Act
        await service.CreateMissingLinkCrypterContainersAsync(CancellationToken.None);

        // Assert
        var result = await dbContext.LinkCrypterContainers.ToListAsync();

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        linkCrypterFactoryMock.Verify(f => f.Get(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task CreateMissingLinkCrypterContainersAsync_PreviousContainerExists_UpdatesPreviousContainer()
    {
        // Arrange
        var seed = await AddUploadWithPreviousContainerAsync();
        linkCrypterMock
            .Setup(c =>
                c.UpdateContainerAsync(
                    linkCrypterConfigMock.Object,
                    "https://crypter.test/existing",
                    "external-1",
                    "container-secret",
                    It.Is<IReadOnlyList<string>>(links =>
                        links.SequenceEqual(
                            new[] { "https://hoster.test/new-a", "https://hoster.test/new-b" }
                        )
                    ),
                    true,
                    true,
                    true,
                    CancellationToken.None
                )
            )
            .ReturnsAsync(new UpdateContainerResult(true, null));

        // Act
        await service.CreateMissingLinkCrypterContainersAsync(CancellationToken.None);

        // Assert
        var result = await dbContext.LinkCrypterContainers.SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(seed.PreviousContainerId);
        result.UploadId.ShouldBe(seed.NewUploadId);
        result.UploadConfigLinkCrypterId.ShouldBe(seed.UploadConfigLinkCrypterId);
        result.ContainerUrl.ShouldBe("https://crypter.test/existing");
        linkCrypterFactoryMock.Verify(f => f.Get(LinkCrypterClassName), Times.Once);
        linkCrypterMock.Verify(c => c.DeserializeConfig(SerializedConfig), Times.Once);
    }

    [Test]
    public async Task CreateMissingLinkCrypterContainersAsync_CollectionScopedLinkCrypter_CreatesOneSharedContainer()
    {
        // Arrange
        var seed = await AddCollectionUploadsWithMissingContainerAsync();
        linkCrypterMock
            .Setup(c =>
                c.CreateContainerAsync(
                    linkCrypterConfigMock.Object,
                    "Hostage S01 - Forum A",
                    "container-secret",
                    It.Is<IReadOnlyList<string>>(links =>
                        links.SequenceEqual(
                            new[]
                            {
                                "https://hoster.test/e01-a",
                                "https://hoster.test/e01-b",
                                "https://hoster.test/e02-a",
                            }
                        )
                    ),
                    true,
                    true,
                    true,
                    CancellationToken.None
                )
            )
            .ReturnsAsync(
                new CreateContainerResult(
                    true,
                    "https://crypter.test/collection",
                    "collection-1",
                    []
                )
            );

        // Act
        await service.CreateMissingLinkCrypterContainersAsync(CancellationToken.None);

        // Assert
        var result = await dbContext
            .LinkCrypterContainers.Include(container => container.SourceUploads)
            .SingleAsync();

        result.Scope.ShouldBe(LinkCrypterContainerScope.ReleaseCollection);
        result.UploadId.ShouldBeNull();
        result.UploadConfigLinkCrypterId.ShouldBeNull();
        result.CollectionUploadSlotId.ShouldBe(seed.CollectionUploadSlotId);
        result.LinkCrypterRegistrationId.ShouldBe(seed.LinkCrypterRegistrationId);
        result.ContainerUrl.ShouldBe("https://crypter.test/collection");
        result.ExternalReference.ShouldBe("collection-1");
        result.State.ShouldBe(LinkCrypterContainerState.Created);
        result.SourceUploads.Select(source => source.UploadId).Order().ShouldBe(seed.UploadIds);
        linkCrypterFactoryMock.Verify(f => f.Get(LinkCrypterClassName), Times.Once);
        linkCrypterMock.Verify(c => c.DeserializeConfig(SerializedConfig), Times.Once);
    }

    private async Task<MissingContainerSeed> AddUploadWithMissingContainerAsync()
    {
        var uploadConfig = await AddUploadConfigWithLinkCrypterAsync(isActive: true);
        var upload = new Upload
        {
            UploadConfigId = uploadConfig.Id,
            CreatedAt = DateTime.UtcNow,
            UploadedAt = DateTime.UtcNow,
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Online,
            UploadedFiles =
            [
                CreateUploadedFile("https://hoster.test/b"),
                CreateUploadedFile("https://hoster.test/a"),
            ],
            ErrorMessages = [],
        };

        dbContext.Uploads.Add(upload);
        await dbContext.SaveChangesAsync();

        var linkCrypterConfig = uploadConfig.LinkCrypters.Single();
        return new MissingContainerSeed(
            upload.Id,
            linkCrypterConfig.Id,
            linkCrypterConfig.LinkCrypterRegistrationId
        );
    }

    private async Task<PreviousContainerSeed> AddUploadWithPreviousContainerAsync()
    {
        var uploadConfig = await AddUploadConfigWithLinkCrypterAsync(isActive: true);
        var uploadConfigLinkCrypterId = uploadConfig.LinkCrypters.Single().Id;
        var previousUpload = new Upload
        {
            UploadConfigId = uploadConfig.Id,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            UploadedAt = DateTime.UtcNow.AddHours(-1),
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Online,
            UploadedFiles = [CreateUploadedFile("https://hoster.test/old-a")],
            ErrorMessages = [],
        };
        var previousContainer = new LinkCrypterContainer
        {
            Scope = LinkCrypterContainerScope.Release,
            Upload = previousUpload,
            UploadConfigLinkCrypterId = uploadConfigLinkCrypterId,
            LinkCrypterRegistrationId = uploadConfig
                .LinkCrypters.Single()
                .LinkCrypterRegistrationId,
            ContainerUrl = "https://crypter.test/existing",
            ExternalReference = "external-1",
            Password = "container-secret",
            State = LinkCrypterContainerState.Created,
            Errors = [],
            CreatedAt = DateTime.UtcNow.AddHours(-1),
        };
        var newUpload = new Upload
        {
            UploadConfigId = uploadConfig.Id,
            CreatedAt = DateTime.UtcNow,
            UploadedAt = DateTime.UtcNow,
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Online,
            UploadedFiles =
            [
                CreateUploadedFile("https://hoster.test/new-b"),
                CreateUploadedFile("https://hoster.test/new-a"),
            ],
            ErrorMessages = [],
        };

        dbContext.Uploads.Add(previousUpload);
        dbContext.LinkCrypterContainers.Add(previousContainer);
        dbContext.Uploads.Add(newUpload);
        await dbContext.SaveChangesAsync();

        return new PreviousContainerSeed(
            newUpload.Id,
            previousContainer.Id,
            uploadConfigLinkCrypterId
        );
    }

    private async Task<CollectionContainerSeed> AddCollectionUploadsWithMissingContainerAsync()
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = "Managed releases",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };
        var releaseCollection = new ReleaseCollection
        {
            ReleaseGroup = releaseGroup,
            Key = "hostage-s01",
            Name = "Hostage S01",
            CreatedAt = DateTime.UtcNow,
        };
        var collectionSlot = new CollectionUploadSlot
        {
            ReleaseCollection = releaseCollection,
            Key = "forum-a",
            Name = "Forum A",
            IsRequired = true,
            PasswordPolicy = CollectionUploadSlotPasswordPolicy.MustMatchAcrossReleases,
            ExpectedArchivePassword = "secret",
        };
        var hosterRegistration = new HosterRegistration
        {
            Name = "Hoster",
            SerializedConfig = "{}",
            HosterClassName = "TestHoster",
            IsActive = true,
        };
        var linkCrypterRegistration = new LinkCrypterRegistration
        {
            Name = "Crypter",
            LinkCrypterClassName = LinkCrypterClassName,
            SerializedConfig = SerializedConfig,
            IsActive = true,
        };

        var firstUploadConfig = CreateCollectionUploadConfig(
            releaseGroup,
            releaseCollection,
            collectionSlot,
            hosterRegistration,
            linkCrypterRegistration,
            "Hostage.S01E01.German.AC3.DL.1080p.Web.x265-FuN",
            "Episode 1 upload"
        );
        var secondUploadConfig = CreateCollectionUploadConfig(
            releaseGroup,
            releaseCollection,
            collectionSlot,
            hosterRegistration,
            linkCrypterRegistration,
            "Hostage.S01E02.German.AC3.DL.1080p.Web.x265-FuN",
            "Episode 2 upload"
        );

        var firstUpload = new Upload
        {
            UploadConfig = firstUploadConfig,
            CreatedAt = DateTime.UtcNow,
            UploadedAt = DateTime.UtcNow,
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Online,
            UploadedFiles =
            [
                CreateUploadedFile("https://hoster.test/e01-b"),
                CreateUploadedFile("https://hoster.test/e01-a"),
            ],
            ErrorMessages = [],
        };
        var secondUpload = new Upload
        {
            UploadConfig = secondUploadConfig,
            CreatedAt = DateTime.UtcNow,
            UploadedAt = DateTime.UtcNow,
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Online,
            UploadedFiles = [CreateUploadedFile("https://hoster.test/e02-a")],
            ErrorMessages = [],
        };

        dbContext.Uploads.AddRange(firstUpload, secondUpload);
        await dbContext.SaveChangesAsync();

        return new CollectionContainerSeed(
            collectionSlot.Id,
            linkCrypterRegistration.Id,
            [firstUpload.Id, secondUpload.Id]
        );
    }

    private static UploadConfig CreateCollectionUploadConfig(
        ReleaseGroup releaseGroup,
        ReleaseCollection releaseCollection,
        CollectionUploadSlot collectionSlot,
        HosterRegistration hosterRegistration,
        LinkCrypterRegistration linkCrypterRegistration,
        string releaseName,
        string uploadConfigName
    )
    {
        var release = new Release
        {
            Name = releaseName,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = $"/tmp/{releaseName}",
            ReleaseGroup = releaseGroup,
            ReleaseCollection = releaseCollection,
        };
        var archiveConfig = new ArchiveConfig
        {
            Release = release,
            Name = "Main archive",
            ArchiveFilesBasePath = "/tmp/archive",
            ArchiverName = "zip",
            ArchiveNamePrefix = releaseName,
            ArchivePassword = "secret",
            ArchiveFileSizeMb = 512,
        };

        return new UploadConfig
        {
            Release = release,
            ArchiveConfig = archiveConfig,
            CollectionUploadSlot = collectionSlot,
            HosterRegistration = hosterRegistration,
            Name = uploadConfigName,
            LinksDistributedTo = [],
            LinkCrypters =
            [
                new UploadConfigLinkCrypter
                {
                    LinkCrypterRegistration = linkCrypterRegistration,
                    ContainerScope = LinkCrypterContainerScope.ReleaseCollection,
                    Password = "container-secret",
                },
            ],
        };
    }

    private async Task<UploadConfig> AddUploadConfigWithLinkCrypterAsync(bool isActive)
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = "Managed releases",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };
        var release = new Release
        {
            Name = "Bearcat.Release.001",
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/tmp/release",
            ReleaseGroup = releaseGroup,
        };
        var archiveConfig = new ArchiveConfig
        {
            Release = release,
            Name = "Main archive",
            ArchiveFilesBasePath = "/tmp/archive",
            ArchiverName = "zip",
            ArchiveNamePrefix = "bearcat-release",
            ArchivePassword = "secret",
            ArchiveFileSizeMb = 512,
        };
        var hosterRegistration = new HosterRegistration
        {
            Name = "Hoster",
            SerializedConfig = "{}",
            HosterClassName = "TestHoster",
            IsActive = true,
        };
        var linkCrypterRegistration = new LinkCrypterRegistration
        {
            Name = "Crypter",
            LinkCrypterClassName = LinkCrypterClassName,
            SerializedConfig = SerializedConfig,
            IsActive = isActive,
        };
        var uploadConfig = new UploadConfig
        {
            Release = release,
            ArchiveConfig = archiveConfig,
            HosterRegistration = hosterRegistration,
            Name = "Default upload",
            LinksDistributedTo = [],
            LinkCrypters =
            [
                new UploadConfigLinkCrypter
                {
                    LinkCrypterRegistration = linkCrypterRegistration,
                    Password = "container-secret",
                },
            ],
        };

        dbContext.UploadConfigs.Add(uploadConfig);
        await dbContext.SaveChangesAsync();

        return uploadConfig;
    }

    private static UploadedFile CreateUploadedFile(string link)
    {
        return new UploadedFile
        {
            ArchiveFile = new ArchiveFile
            {
                Archive = new Archive
                {
                    ArchiveConfig = new ArchiveConfig
                    {
                        Release = new Release
                        {
                            Name = $"Release {Guid.NewGuid():N}",
                            ReleaseType = ReleaseType.Managed,
                            ReleaseFolderPath = "/tmp/release",
                            ReleaseGroup = new ReleaseGroup
                            {
                                Name = $"Group {Guid.NewGuid():N}",
                                EnableAutomaticReuploads = false,
                                NumberOfHoursUntilReupload = 24,
                            },
                        },
                        Name = "Archive",
                        ArchiveFilesBasePath = "/tmp/archive",
                        ArchiverName = "zip",
                        ArchiveFileSizeMb = 512,
                    },
                    ArchiveFolderPath = "/tmp/archive",
                    ArchiveState = ArchiveState.Created,
                    CreatedAt = DateTime.UtcNow,
                    ErrorMessages = [],
                },
                FullFileName = $"{link}.rar",
            },
            HosterFileLink = link,
            ErrorMessages = [],
            OnlineState = OnlineState.Online,
            CreatedAt = DateTime.UtcNow,
            CheckedAt = DateTime.UtcNow,
        };
    }

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }

    private sealed record MissingContainerSeed(
        int UploadId,
        int UploadConfigLinkCrypterId,
        int LinkCrypterRegistrationId
    );

    private sealed record CollectionContainerSeed(
        int CollectionUploadSlotId,
        int LinkCrypterRegistrationId,
        IReadOnlyList<int> UploadIds
    );

    private sealed record PreviousContainerSeed(
        int NewUploadId,
        int PreviousContainerId,
        int UploadConfigLinkCrypterId
    );
}

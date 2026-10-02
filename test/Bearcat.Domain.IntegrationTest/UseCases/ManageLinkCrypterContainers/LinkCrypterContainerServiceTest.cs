using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.LinkCrypter.Results;
using Bearcat.Abstractions.Proxies;
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

public class LinkCrypterContainerServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string LinkCrypterClassName = "TestCrypter";
    private const string SerializedConfig = "{\"apiKey\":\"secret\"}";

    private Mock<ILinkCrypter> linkCrypterMock = null!;
    private Mock<ILinkCrypterConfig> linkCrypterConfigMock = null!;
    private Mock<ILinkCrypterFactory> linkCrypterFactoryMock = null!;
    private LinkCrypterContainerService service = null!;
    private CollectionLinkCrypterContainerService collectionContainerService = null!;

    [SetUp]
    public void Setup()
    {
        linkCrypterConfigMock = new Mock<ILinkCrypterConfig>(MockBehavior.Strict);
        linkCrypterMock = new Mock<ILinkCrypter>(MockBehavior.Strict);
        linkCrypterMock
            .Setup(c => c.DeserializeConfig(SerializedConfig))
            .Returns(linkCrypterConfigMock.Object);

        linkCrypterFactoryMock = new Mock<ILinkCrypterFactory>(MockBehavior.Strict);
        linkCrypterFactoryMock
            .Setup(f => f.Get(LinkCrypterClassName))
            .Returns(linkCrypterMock.Object);

        var repository = new LinkCrypterContainerCreationWriteRepository(DbContext);
        var notificationService = new NotificationService(
            repository: new NotificationRepository(DbContext),
            timeProvider: CreateTimeProvider(),
            configurationProvider: CreateNotificationConfigurationProvider()
        );

        collectionContainerService = new CollectionLinkCrypterContainerService(
            repository,
            Mock.Of<ILogger<CollectionLinkCrypterContainerService>>(),
            linkCrypterFactoryMock.Object,
            CreateTimeProvider(),
            notificationService,
            NoOpSecretProtector.Instance
        );

        service = new LinkCrypterContainerService(
            repository,
            Mock.Of<ILogger<LinkCrypterContainerService>>(),
            linkCrypterFactoryMock.Object,
            CreateTimeProvider(),
            notificationService,
            NoOpSecretProtector.Instance,
            collectionContainerService
        );
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
        var result = await DbContext.LinkCrypterContainers.SingleAsync();

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
        var result = await DbContext
            .LinkCrypterContainers.Include(c => c.Notifications)
            .SingleAsync();

        result.ShouldNotBeNull();
        result.UploadId.ShouldBe(seed.UploadId);
        result.State.ShouldBe(LinkCrypterContainerState.CreationFailed);
        result.ContainerUrl.ShouldBeEmpty();
        result.Errors.ShouldBe(["Could not create container"]);
        result.Notifications.Single().NotificationSeverity.ShouldBe(NotificationSeverity.Error);
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
        DbContext.LinkCrypterContainers.Add(
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
        await DbContext.SaveChangesAsync();

        // Act
        await service.CreateMissingLinkCrypterContainersAsync(CancellationToken.None);

        // Assert
        var result = await DbContext.LinkCrypterContainers.ToListAsync();

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
        var result = await DbContext.LinkCrypterContainers.SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(seed.PreviousContainerId);
        result.UploadId.ShouldBe(seed.NewUploadId);
        result.UploadConfigLinkCrypterId.ShouldBe(seed.UploadConfigLinkCrypterId);
        result.ContainerUrl.ShouldBe("https://crypter.test/existing");
        linkCrypterFactoryMock.Verify(f => f.Get(LinkCrypterClassName), Times.Once);
        linkCrypterMock.Verify(c => c.DeserializeConfig(SerializedConfig), Times.Once);
    }

    [Test]
    public async Task CreateMissingLinkCrypterContainersAsync_MultiplePreviousContainers_UpdatesMostRecentContainer()
    {
        // Arrange
        var seed = await AddUploadWithMultiplePreviousContainersAsync();
        linkCrypterMock
            .Setup(c =>
                c.UpdateContainerAsync(
                    linkCrypterConfigMock.Object,
                    "https://crypter.test/recent",
                    "external-recent",
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
        var containers = await DbContext.LinkCrypterContainers.ToListAsync();

        var reused = containers.Single(c => c.UploadId == seed.NewUploadId);
        reused.Id.ShouldBe(seed.RecentContainerId);
        reused.ContainerUrl.ShouldBe("https://crypter.test/recent");
        linkCrypterFactoryMock.Verify(f => f.Get(LinkCrypterClassName), Times.Once);
        linkCrypterMock.Verify(c => c.DeserializeConfig(SerializedConfig), Times.Once);
    }

    [Test]
    public async Task CreateMissingLinkCrypterContainersAsync_MostRecentPreviousContainerFailed_UpdatesLastCreatedContainer()
    {
        // Arrange
        var seed = await AddUploadWithFailedAndCreatedPreviousContainersAsync();
        linkCrypterMock
            .Setup(c =>
                c.UpdateContainerAsync(
                    linkCrypterConfigMock.Object,
                    "https://crypter.test/created",
                    "external-created",
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
        var reused = await DbContext.LinkCrypterContainers.SingleAsync(c =>
            c.UploadId == seed.NewUploadId
        );
        reused.Id.ShouldBe(seed.CreatedContainerId);
        reused.ContainerUrl.ShouldBe("https://crypter.test/created");
        linkCrypterFactoryMock.Verify(f => f.Get(LinkCrypterClassName), Times.Once);
        linkCrypterMock.Verify(c => c.DeserializeConfig(SerializedConfig), Times.Once);
    }

    [Test]
    public async Task DeleteFailedContainerAsync_ContainerFailed_RemovesContainer()
    {
        // Arrange
        var containerId = await AddContainerAsync(LinkCrypterContainerState.CreationFailed);

        // Act
        await service.DeleteFailedContainerAsync(containerId, CancellationToken.None);

        // Assert
        (await DbContext.LinkCrypterContainers.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task DeleteFailedContainerAsync_FailedCollectionContainer_RemovesContainerAndSourceUploads()
    {
        // Arrange
        var seed = await AddCollectionUploadsWithMissingContainerAsync();
        var container = new LinkCrypterContainer
        {
            Scope = LinkCrypterContainerScope.ReleaseCollection,
            CollectionUploadSlotId = seed.CollectionUploadSlotId,
            LinkCrypterRegistrationId = seed.LinkCrypterRegistrationId,
            ContainerUrl = string.Empty,
            Password = "container-secret",
            State = LinkCrypterContainerState.CreationFailed,
            Errors = ["Could not create container"],
            CreatedAt = DateTime.UtcNow,
            SourceUploads = seed
                .UploadIds.Select(uploadId => new LinkCrypterContainerSourceUpload
                {
                    UploadId = uploadId,
                })
                .ToList(),
        };
        DbContext.LinkCrypterContainers.Add(container);
        await DbContext.SaveChangesAsync();
        var containerId = container.Id;
        DbContext.ChangeTracker.Clear();

        // Act
        await service.DeleteFailedContainerAsync(containerId, CancellationToken.None);

        // Assert
        (await DbContext.LinkCrypterContainers.AnyAsync()).ShouldBeFalse();
        (await DbContext.LinkCrypterContainerSourceUploads.AnyAsync()).ShouldBeFalse();
        var remainingUploadIds = await DbContext.Uploads.Select(upload => upload.Id).ToListAsync();
        remainingUploadIds.Order().ShouldBe(seed.UploadIds.Order());
    }

    [Test]
    public async Task DeleteFailedContainerAsync_ContainerCreated_ThrowsAndKeepsContainer()
    {
        // Arrange
        var containerId = await AddContainerAsync(LinkCrypterContainerState.Created);

        // Act
        var act = async () =>
            await service.DeleteFailedContainerAsync(containerId, CancellationToken.None);

        // Assert
        await act.ShouldThrowAsync<InvalidOperationException>();
        (await DbContext.LinkCrypterContainers.CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task CreateMissingLinkCrypterContainersAsync_OnlyFailedPreviousContainer_CreatesNewContainer()
    {
        // Arrange
        var seed = await AddUploadWithOnlyFailedPreviousContainerAsync();
        linkCrypterMock
            .Setup(c =>
                c.CreateContainerAsync(
                    linkCrypterConfigMock.Object,
                    "Bearcat.Release.001",
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
            .ReturnsAsync(
                new CreateContainerResult(true, "https://crypter.test/new", "ref-new", [])
            );

        // Act
        await service.CreateMissingLinkCrypterContainersAsync(CancellationToken.None);

        // Assert
        var created = await DbContext.LinkCrypterContainers.SingleAsync(c =>
            c.UploadId == seed.NewUploadId
        );
        created.Id.ShouldNotBe(seed.FailedContainerId);
        created.ContainerUrl.ShouldBe("https://crypter.test/new");
        created.State.ShouldBe(LinkCrypterContainerState.Created);
        linkCrypterMock.Verify(
            c =>
                c.UpdateContainerAsync(
                    It.IsAny<ILinkCrypterConfig>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<string>>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
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
                    "Hostage S01",
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
        var result = await DbContext
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

    [Test]
    public async Task UpdateCollectionContainersAsync_SharedSettingsChange_UpdatesExistingContainer()
    {
        var seed = await AddCollectionUploadsWithMissingContainerAsync();
        var existingContainerId = await AddExistingCollectionContainerAsync(
            seed,
            [seed.UploadIds[0]]
        );
        var linkCrypters = await DbContext
            .UploadConfigLinkCrypters.Where(linkCrypter =>
                linkCrypter.UploadConfig.CollectionUploadSlotId == seed.CollectionUploadSlotId
            )
            .ToListAsync();

        foreach (var linkCrypter in linkCrypters)
        {
            linkCrypter.Password = "changed-secret";
            linkCrypter.EnableCaptcha = false;
            linkCrypter.EnableContainerDownload = false;
            linkCrypter.EnableClickAndLoad = false;
        }

        await DbContext.SaveChangesAsync();

        linkCrypterMock
            .Setup(c =>
                c.UpdateContainerAsync(
                    linkCrypterConfigMock.Object,
                    "https://crypter.test/existing-collection",
                    "collection-existing",
                    "changed-secret",
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
                    false,
                    false,
                    false,
                    CancellationToken.None
                )
            )
            .ReturnsAsync(new UpdateContainerResult(true, null));

        await collectionContainerService.UpdateContainersAsync(
            seed.CollectionUploadSlotId,
            CancellationToken.None
        );

        DbContext.ChangeTracker.Clear();
        var result = await DbContext
            .LinkCrypterContainers.Include(container => container.SourceUploads)
            .SingleAsync(container => container.Id == existingContainerId);

        result.Password.ShouldBe("changed-secret");
        result.EnableCaptcha.ShouldBeFalse();
        result.EnableContainerDownload.ShouldBeFalse();
        result.EnableClickAndLoad.ShouldBeFalse();
        result.State.ShouldBe(LinkCrypterContainerState.Created);
        result.Errors.ShouldBeEmpty();
        result.SourceUploads.Select(source => source.UploadId).Order().ShouldBe(seed.UploadIds);
    }

    [Test]
    public async Task UpdateCollectionContainersAsync_PasswordMismatch_MarksExistingContainerAsFailed()
    {
        var seed = await AddCollectionUploadsWithMissingContainerAsync();
        var existingContainerId = await AddExistingCollectionContainerAsync(seed, seed.UploadIds);
        var uploadConfigs = await DbContext
            .UploadConfigs.Include(uploadConfig => uploadConfig.ArchiveConfig)
            .Where(uploadConfig =>
                uploadConfig.CollectionUploadSlotId == seed.CollectionUploadSlotId
            )
            .OrderBy(uploadConfig => uploadConfig.Id)
            .ToListAsync();

        uploadConfigs[1].ArchiveConfig.ArchivePassword = "different";
        await DbContext.SaveChangesAsync();

        await collectionContainerService.UpdateContainersAsync(
            seed.CollectionUploadSlotId,
            CancellationToken.None
        );

        DbContext.ChangeTracker.Clear();
        var result = await DbContext.LinkCrypterContainers.SingleAsync(container =>
            container.Id == existingContainerId
        );

        result.State.ShouldBe(LinkCrypterContainerState.CreationFailed);
        result.Errors.ShouldBe(["Archive passwords differ across releases."]);
        (await DbContext.Notifications.CountAsync()).ShouldBe(1);
        linkCrypterFactoryMock.Verify(f => f.Get(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task UpdateCollectionContainersAsync_NoCompletedUploads_DoesNotCreateContainer()
    {
        // Arrange
        var seed = await AddCollectionUploadsWithMissingContainerAsync();

        var uploads = await DbContext.Uploads.ToListAsync();
        foreach (var upload in uploads)
        {
            upload.OnlineState = OnlineState.Offline;
        }
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        await collectionContainerService.UpdateContainersAsync(
            seed.CollectionUploadSlotId,
            CancellationToken.None
        );

        // Assert
        (await DbContext.LinkCrypterContainers.AnyAsync()).ShouldBeFalse();
        linkCrypterFactoryMock.Verify(f => f.Get(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task UpdateCollectionContainersAsync_StaleSourceUploads_RemovesStaleEntry()
    {
        // Arrange
        var seed = await AddCollectionUploadsWithMissingContainerAsync();
        var existingContainerId = await AddExistingCollectionContainerAsync(seed, seed.UploadIds);

        var firstUpload = await DbContext.Uploads.FindAsync(seed.UploadIds[0]);
        firstUpload!.OnlineState = OnlineState.Offline;
        await DbContext.SaveChangesAsync();

        linkCrypterMock
            .Setup(c =>
                c.UpdateContainerAsync(
                    linkCrypterConfigMock.Object,
                    "https://crypter.test/existing-collection",
                    "collection-existing",
                    "container-secret",
                    It.Is<IReadOnlyList<string>>(links =>
                        links.SequenceEqual(new[] { "https://hoster.test/e02-a" })
                    ),
                    true,
                    true,
                    true,
                    CancellationToken.None
                )
            )
            .ReturnsAsync(new UpdateContainerResult(true, null));

        // Act
        await collectionContainerService.UpdateContainersAsync(
            seed.CollectionUploadSlotId,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var container = await DbContext
            .LinkCrypterContainers.Include(c => c.SourceUploads)
            .SingleAsync(c => c.Id == existingContainerId);

        container.SourceUploads.Count.ShouldBe(1);
        container.SourceUploads.Single().UploadId.ShouldBe(seed.UploadIds[1]);
    }

    [Test]
    public async Task UpdateCollectionContainersAsync_MustEqualExpectedValue_PasswordMatches_CreatesContainer()
    {
        // Arrange
        var seed = await AddCollectionUploadsWithPasswordPolicyAsync(
            CollectionUploadSlotPasswordPolicy.MustEqualExpectedValue,
            expectedPassword: "secret"
        );
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
            .ReturnsAsync(new CreateContainerResult(true, "https://crypter.test/new", "ref-1", []));

        // Act
        await collectionContainerService.UpdateContainersAsync(
            seed.CollectionUploadSlotId,
            CancellationToken.None
        );

        // Assert
        var container = await DbContext.LinkCrypterContainers.SingleAsync();
        container.State.ShouldBe(LinkCrypterContainerState.Created);
        linkCrypterFactoryMock.Verify(f => f.Get(LinkCrypterClassName), Times.Once);
    }

    [Test]
    public async Task UpdateCollectionContainersAsync_MustEqualExpectedValue_PasswordMismatch_MarksContainerAsFailed()
    {
        // Arrange
        var seed = await AddCollectionUploadsWithPasswordPolicyAsync(
            CollectionUploadSlotPasswordPolicy.MustEqualExpectedValue,
            expectedPassword: "expected"
        );
        var existingContainerId = await AddExistingCollectionContainerAsync(seed, seed.UploadIds);

        // Act
        await collectionContainerService.UpdateContainersAsync(
            seed.CollectionUploadSlotId,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var container = await DbContext.LinkCrypterContainers.SingleAsync(c =>
            c.Id == existingContainerId
        );

        container.State.ShouldBe(LinkCrypterContainerState.CreationFailed);
        container.Errors.ShouldBe(["Archive passwords do not match the expected value."]);
        linkCrypterFactoryMock.Verify(f => f.Get(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task UpdateCollectionContainersAsync_SettingsDifferAcrossUploadConfigs_MarksContainerAsFailed()
    {
        // Arrange
        var seed = await AddCollectionUploadsWithMissingContainerAsync();
        var existingContainerId = await AddExistingCollectionContainerAsync(seed, seed.UploadIds);

        var linkCrypters = await DbContext
            .UploadConfigLinkCrypters.Where(lc =>
                lc.UploadConfig.CollectionUploadSlotId == seed.CollectionUploadSlotId
            )
            .OrderBy(lc => lc.Id)
            .ToListAsync();

        linkCrypters[0].Password = "password-a";
        linkCrypters[1].Password = "password-b";
        await DbContext.SaveChangesAsync();

        // Act
        await collectionContainerService.UpdateContainersAsync(
            seed.CollectionUploadSlotId,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var container = await DbContext.LinkCrypterContainers.SingleAsync(c =>
            c.Id == existingContainerId
        );

        container.State.ShouldBe(LinkCrypterContainerState.CreationFailed);
        container.Errors.ShouldBe(["Link crypter settings differ across upload configs."]);
        linkCrypterFactoryMock.Verify(f => f.Get(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task UpdateCollectionContainersAsync_SharedLinkCrypterWasRemoved_DeletesExistingContainer()
    {
        var seed = await AddCollectionUploadsWithMissingContainerAsync();
        await AddExistingCollectionContainerAsync(seed, seed.UploadIds);

        var linkCrypters = await DbContext
            .UploadConfigLinkCrypters.Where(linkCrypter =>
                linkCrypter.UploadConfig.CollectionUploadSlotId == seed.CollectionUploadSlotId
            )
            .ToListAsync();
        DbContext.UploadConfigLinkCrypters.RemoveRange(linkCrypters);
        await DbContext.SaveChangesAsync();

        await collectionContainerService.UpdateContainersAsync(
            seed.CollectionUploadSlotId,
            CancellationToken.None
        );

        (await DbContext.LinkCrypterContainers.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task CreateMissingLinkCrypterContainersAsync_LinkCrypterRegistrationUsesSpecificProxyServer_CreatesContainerInsideLinkCrypterProxyScope()
    {
        // Arrange
        await AddUploadWithMissingContainerAsync();
        var proxyServerId = await UseProxySelectionForLinkCrypterRegistrationAsync(
            ProxySelection.SpecificProxyServer
        );
        ProxyCategoryScopeState? stateDuringCreation = null;
        SetupCreateContainerCapturingProxyScope(state => stateDuringCreation = state);

        // Act
        await service.CreateMissingLinkCrypterContainersAsync(CancellationToken.None);

        // Assert
        stateDuringCreation.ShouldBe(
            new ProxyCategoryScopeState(
                ProxyCategory.LinkCrypters,
                ProxySelection.SpecificProxyServer,
                proxyServerId
            )
        );
        ProxyCategoryScope.Current.ShouldBeNull();
    }

    [Test]
    public async Task CreateMissingLinkCrypterContainersAsync_PreviousContainerAndLinkCrypterRegistrationUsesNoProxy_UpdatesContainerInsideLinkCrypterProxyScope()
    {
        // Arrange
        await AddUploadWithPreviousContainerAsync();
        await UseProxySelectionForLinkCrypterRegistrationAsync(ProxySelection.NoProxy);
        ProxyCategoryScopeState? stateDuringUpdate = null;
        SetupUpdateContainerCapturingProxyScope(state => stateDuringUpdate = state);

        // Act
        await service.CreateMissingLinkCrypterContainersAsync(CancellationToken.None);

        // Assert
        stateDuringUpdate.ShouldBe(
            new ProxyCategoryScopeState(ProxyCategory.LinkCrypters, ProxySelection.NoProxy, null)
        );
        ProxyCategoryScope.Current.ShouldBeNull();
    }

    [Test]
    public async Task CreateMissingLinkCrypterContainersAsync_CollectionScopedLinkCrypterRegistrationUsesSpecificProxyServer_CreatesSharedContainerInsideLinkCrypterProxyScope()
    {
        // Arrange
        await AddCollectionUploadsWithMissingContainerAsync();
        var proxyServerId = await UseProxySelectionForLinkCrypterRegistrationAsync(
            ProxySelection.SpecificProxyServer
        );
        ProxyCategoryScopeState? stateDuringCreation = null;
        SetupCreateContainerCapturingProxyScope(state => stateDuringCreation = state);

        // Act
        await service.CreateMissingLinkCrypterContainersAsync(CancellationToken.None);

        // Assert
        stateDuringCreation.ShouldBe(
            new ProxyCategoryScopeState(
                ProxyCategory.LinkCrypters,
                ProxySelection.SpecificProxyServer,
                proxyServerId
            )
        );
        ProxyCategoryScope.Current.ShouldBeNull();
    }

    [Test]
    public async Task UpdateCollectionContainersAsync_LinkCrypterRegistrationUsesNoProxy_UpdatesExistingContainerInsideLinkCrypterProxyScope()
    {
        // Arrange
        var seed = await AddCollectionUploadsWithMissingContainerAsync();
        await AddExistingCollectionContainerAsync(seed, seed.UploadIds);
        await UseProxySelectionForLinkCrypterRegistrationAsync(ProxySelection.NoProxy);
        ProxyCategoryScopeState? stateDuringUpdate = null;
        SetupUpdateContainerCapturingProxyScope(state => stateDuringUpdate = state);

        // Act
        await collectionContainerService.UpdateContainersAsync(
            seed.CollectionUploadSlotId,
            CancellationToken.None
        );

        // Assert
        stateDuringUpdate.ShouldBe(
            new ProxyCategoryScopeState(ProxyCategory.LinkCrypters, ProxySelection.NoProxy, null)
        );
        ProxyCategoryScope.Current.ShouldBeNull();
    }

    private async Task<int?> UseProxySelectionForLinkCrypterRegistrationAsync(
        ProxySelection proxySelection
    )
    {
        int? proxyServerId = null;

        if (proxySelection == ProxySelection.SpecificProxyServer)
        {
            var proxyServer = new ProxyServer
            {
                Name = "Link crypter proxy",
                ProxyType = ProxyType.Http,
                Host = "proxy.example.com",
                Port = 8080,
            };
            DbContext.ProxyServers.Add(proxyServer);
            await DbContext.SaveChangesAsync();
            proxyServerId = proxyServer.Id;
        }

        var registration = await DbContext.LinkCrypterRegistrations.SingleAsync();
        registration.ProxySelection = proxySelection;
        registration.ProxyServerId = proxyServerId;
        await DbContext.SaveChangesAsync();

        return proxyServerId;
    }

    private void SetupCreateContainerCapturingProxyScope(
        Action<ProxyCategoryScopeState?> captureState
    )
    {
        linkCrypterMock
            .Setup(c =>
                c.CreateContainerAsync(
                    linkCrypterConfigMock.Object,
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<IReadOnlyList<string>>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => captureState(ProxyCategoryScope.Current))
            .ReturnsAsync(
                new CreateContainerResult(true, "https://crypter.test/container", "abc", [])
            );
    }

    private void SetupUpdateContainerCapturingProxyScope(
        Action<ProxyCategoryScopeState?> captureState
    )
    {
        linkCrypterMock
            .Setup(c =>
                c.UpdateContainerAsync(
                    linkCrypterConfigMock.Object,
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<IReadOnlyList<string>>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => captureState(ProxyCategoryScope.Current))
            .ReturnsAsync(new UpdateContainerResult(true, null));
    }

    private async Task<CollectionContainerSeed> AddCollectionUploadsWithPasswordPolicyAsync(
        CollectionUploadSlotPasswordPolicy passwordPolicy,
        string expectedPassword
    )
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
            Key = "hostage-s01-pw",
            Name = "Hostage S01",
            CreatedAt = DateTime.UtcNow,
        };
        var collectionSlot = new CollectionUploadSlot
        {
            ReleaseCollection = releaseCollection,
            Key = "forum-pw",
            Name = "Forum PW",
            IsRequired = true,
            PasswordPolicy = passwordPolicy,
            ExpectedArchivePassword = expectedPassword,
        };
        var hosterRegistration = new HosterRegistration
        {
            Name = "Hoster PW",
            SerializedConfig = "{}",
            HosterClassName = "TestHoster",
            IsActive = true,
        };
        var linkCrypterRegistration = new LinkCrypterRegistration
        {
            Name = "Crypter PW",
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
            "Hostage.S01E01.German",
            "Episode 1 pw upload"
        );
        var secondUploadConfig = CreateCollectionUploadConfig(
            releaseGroup,
            releaseCollection,
            collectionSlot,
            hosterRegistration,
            linkCrypterRegistration,
            "Hostage.S01E02.German",
            "Episode 2 pw upload"
        );
        var firstUpload = new Upload
        {
            UploadConfig = firstUploadConfig,
            CreatedAt = DateTime.UtcNow,
            UploadedAt = DateTime.UtcNow,
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Online,
            UploadedFiles = [CreateUploadedFile("https://hoster.test/pw-e01-a")],
            ErrorMessages = [],
        };
        var secondUpload = new Upload
        {
            UploadConfig = secondUploadConfig,
            CreatedAt = DateTime.UtcNow,
            UploadedAt = DateTime.UtcNow,
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Online,
            UploadedFiles = [CreateUploadedFile("https://hoster.test/pw-e02-a")],
            ErrorMessages = [],
        };

        DbContext.Uploads.AddRange(firstUpload, secondUpload);
        await DbContext.SaveChangesAsync();

        return new CollectionContainerSeed(
            collectionSlot.Id,
            linkCrypterRegistration.Id,
            [firstUpload.Id, secondUpload.Id]
        );
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

        DbContext.Uploads.Add(upload);
        await DbContext.SaveChangesAsync();

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

        DbContext.Uploads.Add(previousUpload);
        DbContext.LinkCrypterContainers.Add(previousContainer);
        DbContext.Uploads.Add(newUpload);
        await DbContext.SaveChangesAsync();

        return new PreviousContainerSeed(
            newUpload.Id,
            previousContainer.Id,
            uploadConfigLinkCrypterId
        );
    }

    private async Task<MultiplePreviousContainerSeed> AddUploadWithMultiplePreviousContainersAsync()
    {
        var uploadConfig = await AddUploadConfigWithLinkCrypterAsync(isActive: true);
        var uploadConfigLinkCrypterId = uploadConfig.LinkCrypters.Single().Id;
        var linkCrypterRegistrationId = uploadConfig
            .LinkCrypters.Single()
            .LinkCrypterRegistrationId;

        var oldestUpload = new Upload
        {
            UploadConfigId = uploadConfig.Id,
            CreatedAt = DateTime.UtcNow.AddHours(-4),
            UploadedAt = DateTime.UtcNow.AddHours(-3),
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Offline,
            UploadedFiles = [CreateUploadedFile("https://hoster.test/oldest-a")],
            ErrorMessages = [],
        };
        var staleContainer = new LinkCrypterContainer
        {
            Scope = LinkCrypterContainerScope.Release,
            Upload = oldestUpload,
            UploadConfigLinkCrypterId = uploadConfigLinkCrypterId,
            LinkCrypterRegistrationId = linkCrypterRegistrationId,
            ContainerUrl = "https://crypter.test/stale",
            ExternalReference = "external-stale",
            Password = "container-secret",
            State = LinkCrypterContainerState.Created,
            Errors = [],
            CreatedAt = DateTime.UtcNow.AddHours(-3),
        };
        var recentUpload = new Upload
        {
            UploadConfigId = uploadConfig.Id,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            UploadedAt = DateTime.UtcNow.AddHours(-1),
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Online,
            UploadedFiles = [CreateUploadedFile("https://hoster.test/recent-a")],
            ErrorMessages = [],
        };
        var recentContainer = new LinkCrypterContainer
        {
            Scope = LinkCrypterContainerScope.Release,
            Upload = recentUpload,
            UploadConfigLinkCrypterId = uploadConfigLinkCrypterId,
            LinkCrypterRegistrationId = linkCrypterRegistrationId,
            ContainerUrl = "https://crypter.test/recent",
            ExternalReference = "external-recent",
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

        DbContext.Uploads.Add(oldestUpload);
        DbContext.LinkCrypterContainers.Add(staleContainer);
        DbContext.Uploads.Add(recentUpload);
        DbContext.LinkCrypterContainers.Add(recentContainer);
        DbContext.Uploads.Add(newUpload);
        await DbContext.SaveChangesAsync();

        return new MultiplePreviousContainerSeed(newUpload.Id, recentContainer.Id);
    }

    private async Task<OnlyFailedContainerSeed> AddUploadWithOnlyFailedPreviousContainerAsync()
    {
        var uploadConfig = await AddUploadConfigWithLinkCrypterAsync(isActive: true);
        var uploadConfigLinkCrypterId = uploadConfig.LinkCrypters.Single().Id;
        var linkCrypterRegistrationId = uploadConfig
            .LinkCrypters.Single()
            .LinkCrypterRegistrationId;

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
        var failedContainer = new LinkCrypterContainer
        {
            Scope = LinkCrypterContainerScope.Release,
            Upload = previousUpload,
            UploadConfigLinkCrypterId = uploadConfigLinkCrypterId,
            LinkCrypterRegistrationId = linkCrypterRegistrationId,
            ContainerUrl = string.Empty,
            ExternalReference = null,
            Password = "container-secret",
            State = LinkCrypterContainerState.CreationFailed,
            Errors = ["Could not create container"],
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

        DbContext.Uploads.Add(previousUpload);
        DbContext.LinkCrypterContainers.Add(failedContainer);
        DbContext.Uploads.Add(newUpload);
        await DbContext.SaveChangesAsync();

        return new OnlyFailedContainerSeed(newUpload.Id, failedContainer.Id);
    }

    private async Task<int> AddContainerAsync(LinkCrypterContainerState state)
    {
        var seed = await AddUploadWithMissingContainerAsync();
        var container = new LinkCrypterContainer
        {
            Scope = LinkCrypterContainerScope.Release,
            UploadId = seed.UploadId,
            UploadConfigLinkCrypterId = seed.UploadConfigLinkCrypterId,
            LinkCrypterRegistrationId = seed.LinkCrypterRegistrationId,
            ContainerUrl = "https://crypter.test/existing",
            ExternalReference = "external-1",
            Password = "container-secret",
            State = state,
            Errors = [],
            CreatedAt = DateTime.UtcNow,
        };

        DbContext.LinkCrypterContainers.Add(container);
        await DbContext.SaveChangesAsync();

        return container.Id;
    }

    private async Task<FailedAndCreatedContainerSeed> AddUploadWithFailedAndCreatedPreviousContainersAsync()
    {
        var uploadConfig = await AddUploadConfigWithLinkCrypterAsync(isActive: true);
        var uploadConfigLinkCrypterId = uploadConfig.LinkCrypters.Single().Id;
        var linkCrypterRegistrationId = uploadConfig
            .LinkCrypters.Single()
            .LinkCrypterRegistrationId;

        var createdUpload = new Upload
        {
            UploadConfigId = uploadConfig.Id,
            CreatedAt = DateTime.UtcNow.AddHours(-4),
            UploadedAt = DateTime.UtcNow.AddHours(-3),
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Online,
            UploadedFiles = [CreateUploadedFile("https://hoster.test/created-a")],
            ErrorMessages = [],
        };
        var createdContainer = new LinkCrypterContainer
        {
            Scope = LinkCrypterContainerScope.Release,
            Upload = createdUpload,
            UploadConfigLinkCrypterId = uploadConfigLinkCrypterId,
            LinkCrypterRegistrationId = linkCrypterRegistrationId,
            ContainerUrl = "https://crypter.test/created",
            ExternalReference = "external-created",
            Password = "container-secret",
            State = LinkCrypterContainerState.Created,
            Errors = [],
            CreatedAt = DateTime.UtcNow.AddHours(-3),
        };
        var failedUpload = new Upload
        {
            UploadConfigId = uploadConfig.Id,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            UploadedAt = DateTime.UtcNow.AddHours(-1),
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Online,
            UploadedFiles = [CreateUploadedFile("https://hoster.test/failed-a")],
            ErrorMessages = [],
        };
        var failedContainer = new LinkCrypterContainer
        {
            Scope = LinkCrypterContainerScope.Release,
            Upload = failedUpload,
            UploadConfigLinkCrypterId = uploadConfigLinkCrypterId,
            LinkCrypterRegistrationId = linkCrypterRegistrationId,
            ContainerUrl = "https://crypter.test/failed",
            ExternalReference = "external-failed",
            Password = "container-secret",
            State = LinkCrypterContainerState.CreationFailed,
            Errors = ["Container not found"],
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

        DbContext.Uploads.Add(createdUpload);
        DbContext.LinkCrypterContainers.Add(createdContainer);
        DbContext.Uploads.Add(failedUpload);
        DbContext.LinkCrypterContainers.Add(failedContainer);
        DbContext.Uploads.Add(newUpload);
        await DbContext.SaveChangesAsync();

        return new FailedAndCreatedContainerSeed(newUpload.Id, createdContainer.Id);
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

        DbContext.Uploads.AddRange(firstUpload, secondUpload);
        await DbContext.SaveChangesAsync();

        return new CollectionContainerSeed(
            collectionSlot.Id,
            linkCrypterRegistration.Id,
            [firstUpload.Id, secondUpload.Id]
        );
    }

    private async Task<int> AddExistingCollectionContainerAsync(
        CollectionContainerSeed seed,
        IReadOnlyList<int> sourceUploadIds
    )
    {
        var container = new LinkCrypterContainer
        {
            Scope = LinkCrypterContainerScope.ReleaseCollection,
            CollectionUploadSlotId = seed.CollectionUploadSlotId,
            LinkCrypterRegistrationId = seed.LinkCrypterRegistrationId,
            ContainerUrl = "https://crypter.test/existing-collection",
            ExternalReference = "collection-existing",
            Password = "container-secret",
            EnableCaptcha = true,
            EnableContainerDownload = true,
            EnableClickAndLoad = true,
            State = LinkCrypterContainerState.Created,
            Errors = [],
            CreatedAt = DateTime.UtcNow,
            SourceUploads = sourceUploadIds
                .Select(uploadId => new LinkCrypterContainerSourceUpload { UploadId = uploadId })
                .ToList(),
        };

        DbContext.LinkCrypterContainers.Add(container);
        await DbContext.SaveChangesAsync();

        return container.Id;
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
            LinkCrypters =
            [
                new UploadConfigLinkCrypter
                {
                    LinkCrypterRegistration = linkCrypterRegistration,
                    Password = "container-secret",
                },
            ],
        };

        DbContext.UploadConfigs.Add(uploadConfig);
        await DbContext.SaveChangesAsync();

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

    private sealed record MultiplePreviousContainerSeed(int NewUploadId, int RecentContainerId);

    private sealed record FailedAndCreatedContainerSeed(int NewUploadId, int CreatedContainerId);

    private sealed record OnlyFailedContainerSeed(int NewUploadId, int FailedContainerId);
}

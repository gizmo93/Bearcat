using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageLinkCrypterContainers;
using Bearcat.Domain.UseCases.ManageNotifications;
using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Dto;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleaseCollections;

public class ReleaseCollectionServiceTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private ReleaseCollectionRepository repository = null!;
    private ReleaseCollectionService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        repository = new ReleaseCollectionRepository(dbContext, dbContext);
        service = new ReleaseCollectionService(
            repository,
            new CollectionLinkCrypterContainerService(
                new LinkCrypterContainerCreationWriteRepository(dbContext),
                Microsoft
                    .Extensions
                    .Logging
                    .Abstractions
                    .NullLogger<CollectionLinkCrypterContainerService>
                    .Instance,
                new Moq.Mock<Bearcat.Abstractions.LinkCrypter.ILinkCrypterFactory>().Object,
                CreateTimeProvider(),
                new NotificationService(
                    new NotificationRepository(dbContext),
                    CreateTimeProvider()
                ),
                NoOpSecretProtector.Instance
            ),
            CreateTimeProvider()
        );
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task UpdateSharedLinkCryptersAsync_CollectionSlotHasMultipleUploadConfigs_SyncsSettingsToAllUploadConfigs()
    {
        // Arrange
        var seed = await AddReleaseCollectionWithUploadSlotAsync();

        // Act
        await service.UpdateSharedLinkCryptersAsync(
            seed.CollectionUploadSlotId,
            [
                new CollectionUploadSlotLinkCrypterSettings(
                    seed.LinkCrypterRegistrationId,
                    "secret",
                    EnableCaptcha: false,
                    EnableContainerDownload: true,
                    EnableClickAndLoad: false
                ),
            ],
            CancellationToken.None
        );

        // Assert
        dbContext.ChangeTracker.Clear();
        var linkCrypters = await dbContext
            .UploadConfigLinkCrypters.Where(linkCrypter =>
                linkCrypter.UploadConfig.CollectionUploadSlotId == seed.CollectionUploadSlotId
            )
            .OrderBy(linkCrypter => linkCrypter.UploadConfig.Name)
            .ToListAsync();

        linkCrypters.Count.ShouldBe(2);
        linkCrypters.ShouldAllBe(linkCrypter =>
            linkCrypter.LinkCrypterRegistrationId == seed.LinkCrypterRegistrationId
            && linkCrypter.ContainerScope == LinkCrypterContainerScope.ReleaseCollection
            && linkCrypter.Password == "secret"
            && !linkCrypter.EnableCaptcha
            && linkCrypter.EnableContainerDownload
            && !linkCrypter.EnableClickAndLoad
        );

        await service.UpdateSharedLinkCryptersAsync(
            seed.CollectionUploadSlotId,
            [
                new CollectionUploadSlotLinkCrypterSettings(
                    seed.LinkCrypterRegistrationId,
                    "changed",
                    EnableCaptcha: true,
                    EnableContainerDownload: false,
                    EnableClickAndLoad: true
                ),
            ],
            CancellationToken.None
        );

        dbContext.ChangeTracker.Clear();
        var updatedLinkCrypters = await dbContext.UploadConfigLinkCrypters.ToListAsync();

        updatedLinkCrypters.Count.ShouldBe(2);
        updatedLinkCrypters.ShouldAllBe(linkCrypter =>
            linkCrypter.Password == "changed"
            && linkCrypter.EnableCaptcha
            && !linkCrypter.EnableContainerDownload
            && linkCrypter.EnableClickAndLoad
        );

        await service.UpdateSharedLinkCryptersAsync(
            seed.CollectionUploadSlotId,
            [],
            CancellationToken.None
        );

        dbContext.ChangeTracker.Clear();
        var hasSharedLinkCrypters = await dbContext.UploadConfigLinkCrypters.AnyAsync();

        hasSharedLinkCrypters.ShouldBeFalse();
    }

    [Test]
    public async Task AssignFromTemplateAsync_ExistingCollectionHasSlotSettings_AppliesCollectionSettingsToNewRelease()
    {
        // Arrange
        var seed = await AddReleaseCollectionWithUploadSlotAsync(
            "collection-secret",
            enableCaptcha: false,
            enableContainerDownload: true,
            enableClickAndLoad: false
        );
        var releaseTemplate = new ReleaseTemplate
        {
            ReleaseGroupId = seed.ReleaseGroupId,
            UseReleaseCollections = true,
            ReleaseCollectionDetectionMode = ReleaseCollectionDetectionMode.SeriesEpisodePattern,
            UploadConfigTemplates =
            [
                new UploadConfigTemplate
                {
                    CollectionUploadSlotKey = "rapidgator",
                    LinkCrypterTemplates =
                    [
                        new UploadConfigLinkCrypterTemplate
                        {
                            LinkCrypterRegistrationId = seed.LinkCrypterRegistrationId,
                            ContainerScope = LinkCrypterContainerScope.ReleaseCollection,
                            Password = "template-secret",
                            EnableCaptcha = true,
                            EnableContainerDownload = false,
                            EnableClickAndLoad = true,
                        },
                    ],
                },
            ],
        };
        var release = new Release
        {
            Name = "Hostage.S01E02.German.AC3.DL.1080p.Web.x265-FuN.mkv",
            UploadConfigs =
            [
                new UploadConfig
                {
                    LinksDistributedTo = [],
                    LinkCrypters =
                    [
                        new UploadConfigLinkCrypter
                        {
                            LinkCrypterRegistrationId = seed.LinkCrypterRegistrationId,
                            ContainerScope = LinkCrypterContainerScope.ReleaseCollection,
                            Password = "template-secret",
                            EnableCaptcha = true,
                            EnableContainerDownload = false,
                            EnableClickAndLoad = true,
                            LinkCrypterContainers = [],
                        },
                    ],
                },
            ],
        };
        var assignmentService = new ReleaseCollectionAssignmentService(
            repository,
            CreateTimeProvider()
        );

        // Act
        await assignmentService.AssignFromTemplateAsync(
            release,
            releaseTemplate,
            [
                new ReleaseUploadConfigMatch(
                    releaseTemplate.UploadConfigTemplates.Single(),
                    release.UploadConfigs.Single()
                ),
            ],
            CancellationToken.None
        );

        // Assert
        var linkCrypter = release.UploadConfigs.Single().LinkCrypters.Single();

        release.ReleaseCollection.ShouldNotBeNull();
        release.UploadConfigs.Single().CollectionUploadSlot.ShouldNotBeNull();
        linkCrypter.Password.ShouldBe("collection-secret");
        linkCrypter.EnableCaptcha.ShouldBeFalse();
        linkCrypter.EnableContainerDownload.ShouldBeTrue();
        linkCrypter.EnableClickAndLoad.ShouldBeFalse();
    }

    private async Task<ReleaseCollectionUploadSlotSeed> AddReleaseCollectionWithUploadSlotAsync(
        string? linkCrypterPassword = null,
        bool enableCaptcha = true,
        bool enableContainerDownload = true,
        bool enableClickAndLoad = true
    )
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = "Series",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };
        var releaseCollection = new ReleaseCollection
        {
            ReleaseGroup = releaseGroup,
            Key = "hostage.s01.german.ac3.dl.1080p.web.x265.fun.mkv",
            Name = "Hostage.S01.German.AC3.DL.1080p.Web.x265-FuN.mkv",
            CreatedAt = DateTime.UtcNow,
        };
        var collectionUploadSlot = new CollectionUploadSlot
        {
            ReleaseCollection = releaseCollection,
            Key = "rapidgator",
            Name = "Rapidgator",
            PasswordPolicy = CollectionUploadSlotPasswordPolicy.Ignore,
        };
        var linkCrypterRegistration = new LinkCrypterRegistration
        {
            Name = "ShareCrypt",
            LinkCrypterClassName = "ShareCryptLinkCrypter",
            SerializedConfig = "{}",
            IsActive = true,
        };

        dbContext.LinkCrypterRegistrations.Add(linkCrypterRegistration);
        dbContext.UploadConfigs.AddRange(
            CreateUploadConfig(
                releaseGroup,
                releaseCollection,
                collectionUploadSlot,
                "Episode 1",
                linkCrypterRegistration,
                linkCrypterPassword,
                enableCaptcha,
                enableContainerDownload,
                enableClickAndLoad
            ),
            CreateUploadConfig(
                releaseGroup,
                releaseCollection,
                collectionUploadSlot,
                "Episode 2",
                linkCrypterRegistration,
                linkCrypterPassword,
                enableCaptcha,
                enableContainerDownload,
                enableClickAndLoad
            )
        );
        await dbContext.SaveChangesAsync();

        return new ReleaseCollectionUploadSlotSeed(
            releaseGroup.Id,
            releaseCollection.Id,
            collectionUploadSlot.Id,
            linkCrypterRegistration.Id
        );
    }

    private static UploadConfig CreateUploadConfig(
        ReleaseGroup releaseGroup,
        ReleaseCollection releaseCollection,
        CollectionUploadSlot collectionUploadSlot,
        string releaseName,
        LinkCrypterRegistration linkCrypterRegistration,
        string? linkCrypterPassword,
        bool enableCaptcha,
        bool enableContainerDownload,
        bool enableClickAndLoad
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
            Name = $"{releaseName} archive",
            ArchiveFilesBasePath = "/tmp/archive",
            ArchiverName = "rar",
            ArchiveNamePrefix = releaseName,
            ArchiveFileSizeMb = 512,
        };
        var hosterRegistration = new HosterRegistration
        {
            Name = $"{releaseName} hoster",
            SerializedConfig = "{}",
            HosterClassName = "RapidgatorHoster",
            IsActive = true,
        };

        return new UploadConfig
        {
            Release = release,
            ArchiveConfig = archiveConfig,
            HosterRegistration = hosterRegistration,
            CollectionUploadSlot = collectionUploadSlot,
            Name = $"{releaseName} upload",
            LinksDistributedTo = [],
            LinkCrypters = linkCrypterPassword is null
                ? []
                :
                [
                    new UploadConfigLinkCrypter
                    {
                        LinkCrypterRegistration = linkCrypterRegistration,
                        ContainerScope = LinkCrypterContainerScope.ReleaseCollection,
                        Password = linkCrypterPassword,
                        EnableCaptcha = enableCaptcha,
                        EnableContainerDownload = enableContainerDownload,
                        EnableClickAndLoad = enableClickAndLoad,
                        LinkCrypterContainers = [],
                    },
                ],
        };
    }

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }

    private sealed record ReleaseCollectionUploadSlotSeed(
        int ReleaseGroupId,
        int ReleaseCollectionId,
        int CollectionUploadSlotId,
        int LinkCrypterRegistrationId
    );
}

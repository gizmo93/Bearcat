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
using Microsoft.Extensions.Logging.Abstractions;
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
    public async Task CreateAsync_ValidData_PersistsCollection()
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = "Series",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };
        dbContext.ReleaseGroups.Add(releaseGroup);
        await dbContext.SaveChangesAsync();

        var id = await service.CreateAsync("  Hostage S01  ", "hostage.s01", releaseGroup.Id);

        dbContext.ChangeTracker.Clear();
        var collection = await dbContext.ReleaseCollections.FindAsync(id);

        collection.ShouldNotBeNull();
        collection.Name.ShouldBe("Hostage S01");
        collection.Key.ShouldBe("hostage.s01");
        collection.ReleaseGroupId.ShouldBe(releaseGroup.Id);
    }

    [Test]
    public async Task UpdateAsync_ValidData_UpdatesName()
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = "Series",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };
        var collection = new ReleaseCollection
        {
            ReleaseGroup = releaseGroup,
            Key = "hostage.s01",
            Name = "Hostage S01",
            CreatedAt = DateTime.UtcNow,
        };
        dbContext.ReleaseCollections.Add(collection);
        await dbContext.SaveChangesAsync();

        await service.UpdateAsync(collection.Id, "  Hostage Season 1  ");

        dbContext.ChangeTracker.Clear();
        var updated = await dbContext.ReleaseCollections.FindAsync(collection.Id);

        updated!.Name.ShouldBe("Hostage Season 1");
    }

    [Test]
    public async Task DeleteAsync_CollectionExists_RemovesCollection()
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = "Series",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };
        var collection = new ReleaseCollection
        {
            ReleaseGroup = releaseGroup,
            Key = "hostage.s01",
            Name = "Hostage S01",
            CreatedAt = DateTime.UtcNow,
        };
        dbContext.ReleaseCollections.Add(collection);
        await dbContext.SaveChangesAsync();

        await service.DeleteAsync(collection.Id);

        dbContext.ChangeTracker.Clear();
        var exists = await dbContext.ReleaseCollections.AnyAsync(c => c.Id == collection.Id);

        exists.ShouldBeFalse();
    }

    [Test]
    public async Task CreateUploadSlotAsync_ValidData_CreatesSlotWithUploadConfigPerRelease()
    {
        var seed = await AddReleaseCollectionWithReleasesAsync("Main");
        var hosterRegistration = new HosterRegistration
        {
            Name = "Rapidgator",
            SerializedConfig = "{}",
            HosterClassName = "RapidgatorHoster",
            IsActive = true,
        };
        dbContext.HosterRegistrations.Add(hosterRegistration);
        await dbContext.SaveChangesAsync();

        var slotId = await service.CreateUploadSlotAsync(
            seed.CollectionId,
            "  Rapidgator  ",
            hosterRegistration.Id,
            "Main",
            premiumOnlyDownload: false,
            isRequired: true,
            CollectionUploadSlotPasswordPolicy.Ignore,
            expectedArchivePassword: null
        );

        dbContext.ChangeTracker.Clear();
        var slot = await dbContext
            .CollectionUploadSlots.Include(s => s.UploadConfigs)
            .SingleAsync(s => s.Id == slotId);

        slot.Key.ShouldBe("rapidgator");
        slot.Name.ShouldBe("Rapidgator");
        slot.PasswordPolicy.ShouldBe(CollectionUploadSlotPasswordPolicy.Ignore);
        slot.ExpectedArchivePassword.ShouldBeNull();
        slot.UploadConfigs.Count.ShouldBe(seed.ReleaseIds.Count);
        slot.UploadConfigs.ShouldAllBe(uc =>
            uc.Name == "Rapidgator"
            && uc.HosterRegistrationId == hosterRegistration.Id
            && !uc.PremiumOnlyDownload
        );
    }

    [Test]
    public async Task CreateUploadSlotAsync_MustEqualExpectedValue_PersistsExpectedPassword()
    {
        var seed = await AddReleaseCollectionWithReleasesAsync("Main");
        var hosterRegistration = new HosterRegistration
        {
            Name = "Rapidgator",
            SerializedConfig = "{}",
            HosterClassName = "RapidgatorHoster",
            IsActive = true,
        };
        dbContext.HosterRegistrations.Add(hosterRegistration);
        await dbContext.SaveChangesAsync();

        var slotId = await service.CreateUploadSlotAsync(
            seed.CollectionId,
            "Rapidgator",
            hosterRegistration.Id,
            "Main",
            premiumOnlyDownload: false,
            isRequired: true,
            CollectionUploadSlotPasswordPolicy.MustEqualExpectedValue,
            expectedArchivePassword: "s3cr3t"
        );

        dbContext.ChangeTracker.Clear();
        var slot = await dbContext.CollectionUploadSlots.FindAsync(slotId);

        slot!.PasswordPolicy.ShouldBe(CollectionUploadSlotPasswordPolicy.MustEqualExpectedValue);
        slot.ExpectedArchivePassword.ShouldBe("s3cr3t");
    }

    [Test]
    public async Task CreateUploadSlotAsync_EmptyName_Throws()
    {
        var seed = await AddReleaseCollectionWithReleasesAsync("Main");
        var hosterRegistration = new HosterRegistration
        {
            Name = "Rapidgator",
            SerializedConfig = "{}",
            HosterClassName = "RapidgatorHoster",
            IsActive = true,
        };
        dbContext.HosterRegistrations.Add(hosterRegistration);
        await dbContext.SaveChangesAsync();

        await Should.ThrowAsync<ArgumentException>(() =>
            service.CreateUploadSlotAsync(
                seed.CollectionId,
                "   ",
                hosterRegistration.Id,
                "Main",
                false,
                true,
                CollectionUploadSlotPasswordPolicy.Ignore,
                null
            )
        );
    }

    [Test]
    public async Task CreateUploadSlotAsync_DuplicateSlotKey_Throws()
    {
        var seed = await AddReleaseCollectionWithReleasesAsync("Main");
        var hosterRegistration = new HosterRegistration
        {
            Name = "Rapidgator",
            SerializedConfig = "{}",
            HosterClassName = "RapidgatorHoster",
            IsActive = true,
        };
        dbContext.HosterRegistrations.Add(hosterRegistration);
        var existingSlot = new CollectionUploadSlot
        {
            ReleaseCollectionId = seed.CollectionId,
            Key = "rapidgator",
            Name = "Rapidgator",
            PasswordPolicy = CollectionUploadSlotPasswordPolicy.Ignore,
        };
        dbContext.CollectionUploadSlots.Add(existingSlot);
        await dbContext.SaveChangesAsync();

        await Should.ThrowAsync<InvalidOperationException>(() =>
            service.CreateUploadSlotAsync(
                seed.CollectionId,
                "Rapidgator",
                hosterRegistration.Id,
                "Main",
                false,
                true,
                CollectionUploadSlotPasswordPolicy.Ignore,
                null
            )
        );
    }

    [Test]
    public async Task CreateUploadSlotAsync_ArchiveConfigMissingFromRelease_Throws()
    {
        var seed = await AddReleaseCollectionWithReleasesAsync("Main");
        var hosterRegistration = new HosterRegistration
        {
            Name = "Rapidgator",
            SerializedConfig = "{}",
            HosterClassName = "RapidgatorHoster",
            IsActive = true,
        };
        dbContext.HosterRegistrations.Add(hosterRegistration);
        await dbContext.SaveChangesAsync();

        await Should.ThrowAsync<InvalidOperationException>(() =>
            service.CreateUploadSlotAsync(
                seed.CollectionId,
                "Rapidgator",
                hosterRegistration.Id,
                "WrongArchiveName",
                false,
                true,
                CollectionUploadSlotPasswordPolicy.Ignore,
                null
            )
        );
    }

    [Test]
    public async Task DeleteUploadSlotAsync_SlotExists_RemovesSlotAndUploadConfigs()
    {
        var seed = await AddReleaseCollectionWithUploadSlotAsync();

        await service.DeleteUploadSlotAsync(seed.CollectionUploadSlotId);

        dbContext.ChangeTracker.Clear();
        var slotExists = await dbContext.CollectionUploadSlots.AnyAsync(s =>
            s.Id == seed.CollectionUploadSlotId
        );
        var uploadConfigsExist = await dbContext.UploadConfigs.AnyAsync(uc =>
            uc.CollectionUploadSlotId == seed.CollectionUploadSlotId
        );

        slotExists.ShouldBeFalse();
        uploadConfigsExist.ShouldBeFalse();
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

    private async Task<ReleaseCollectionWithReleasesSeed> AddReleaseCollectionWithReleasesAsync(
        string archiveConfigName
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
            Key = "hostage.s01",
            Name = "Hostage S01",
            CreatedAt = DateTime.UtcNow,
        };
        var release1 = new Release
        {
            Name = "Hostage.S01E01",
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/tmp/e01",
            ReleaseGroup = releaseGroup,
            ReleaseCollection = releaseCollection,
        };
        var release2 = new Release
        {
            Name = "Hostage.S01E02",
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/tmp/e02",
            ReleaseGroup = releaseGroup,
            ReleaseCollection = releaseCollection,
        };
        dbContext.ArchiveConfigs.AddRange(
            new ArchiveConfig
            {
                Release = release1,
                Name = archiveConfigName,
                ArchiveFilesBasePath = "/tmp/archive",
                ArchiverName = "rar",
                ArchiveNamePrefix = "e01",
                ArchiveFileSizeMb = 512,
            },
            new ArchiveConfig
            {
                Release = release2,
                Name = archiveConfigName,
                ArchiveFilesBasePath = "/tmp/archive",
                ArchiverName = "rar",
                ArchiveNamePrefix = "e02",
                ArchiveFileSizeMb = 512,
            }
        );
        await dbContext.SaveChangesAsync();

        return new ReleaseCollectionWithReleasesSeed(
            releaseCollection.Id,
            [release1.Id, release2.Id]
        );
    }

    private sealed record ReleaseCollectionWithReleasesSeed(
        int CollectionId,
        IReadOnlyList<int> ReleaseIds
    );

    private sealed record ReleaseCollectionUploadSlotSeed(
        int ReleaseGroupId,
        int ReleaseCollectionId,
        int CollectionUploadSlotId,
        int LinkCrypterRegistrationId
    );
}

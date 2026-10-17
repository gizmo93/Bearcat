using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Moq;
using Shouldly;
using static Bearcat.Infrastructure.IntegrationTest.Database.Repositories.ReleaseRead.ReleaseReadRepositoryTestData;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories.ReleaseRead;

public class ReleaseReadRepositoryHosterAccountPoolTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string PoolHosterClassName = "PoolHoster";

    private ReleaseReadRepositoryTestData testData = null!;
    private ReleaseReadRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        testData = new ReleaseReadRepositoryTestData(DbContext);

        var archiverFactory = new Mock<IArchiverFactory>();
        archiverFactory
            .Setup(factory => factory.GetArchivers())
            .Returns([new ArchiverDto("RAR", "RarArchiver", ".rar")]);
        var linkCrypterFactory = new Mock<ILinkCrypterFactory>();
        linkCrypterFactory.Setup(factory => factory.GetLinkCrypters()).Returns([]);
        var poolHoster = new Mock<IHoster>();
        poolHoster.Setup(hoster => hoster.Name).Returns("Pool hoster display name");
        var hosterFactory = new Mock<IHosterFactory>(MockBehavior.Strict);
        hosterFactory
            .Setup(factory => factory.GetByName(PoolHosterClassName))
            .Returns(poolHoster.Object);

        repository = new ReleaseReadRepository(
            ReadDbContext,
            archiverFactory.Object,
            linkCrypterFactory.Object,
            hosterFactory.Object
        );
    }

    [Test]
    public async Task GetPostQueueAsync_HosterAccountPoolConfigWithActiveMemberAndUnassignedPendingUpload_HidesRelease()
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.PendingPool.2026-GRP");
        AddCompletedUpload(testData.AddUploadConfig(release, "Single account"));
        var poolConfig = AddHosterAccountPoolUploadConfig(release, memberIsActive: true);
        AddUnassignedUpload(poolConfig, UploadState.Pending);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetPostQueueAsync(CancellationToken.None);

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetPostQueueAsync_HosterAccountPoolConfigWithoutActiveMember_IgnoresConfigAndShowsPoolNameOfUnassignedUpload()
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.InactivePool.2026-GRP");
        var archiveConfig = testData.AddArchiveConfig(release, "Archive");
        AddCompletedUpload(
            testData.AddUploadConfig(release, "Single account", archiveConfig: archiveConfig)
        );
        var poolConfig = AddHosterAccountPoolUploadConfig(
            release,
            memberIsActive: false,
            archiveConfig
        );
        AddUnassignedUpload(poolConfig, UploadState.Pending);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetPostQueueAsync(CancellationToken.None);

        // Assert
        var item = result.ShouldHaveSingleItem();
        item.ReleaseId.ShouldBe(release.Id);
        item.UploadGroups.ShouldHaveSingleItem()
            .Hosters.Select(hoster => hoster.HosterAccountOrPoolName)
            .ShouldBe(["Pool", "Single account hoster"]);
    }

    [Test]
    public async Task SearchReleasesAsync_HosterRegistrationFilterMatchesMemberOfHosterAccountPool_ReturnsReleaseOfPoolConfig()
    {
        // Arrange
        var poolRelease = testData.AddRelease("Bearcat.Pool.2026-GRP");
        var poolConfig = AddHosterAccountPoolUploadConfig(poolRelease, memberIsActive: true);
        testData.AddUploadConfig(testData.AddRelease("Bearcat.Other.2026-GRP"), "Other");
        await DbContext.SaveChangesAsync();
        var memberRegistrationId = poolConfig.HosterAccountPool!.Members[0].HosterRegistrationId;
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.SearchReleasesAsync(
            new ReleaseSearchQuery(HosterRegistrationId: memberRegistrationId),
            CancellationToken.None
        );

        // Assert
        result.Items.ShouldHaveSingleItem().ReleaseId.ShouldBe(poolRelease.Id);
    }

    [Test]
    public async Task SearchUploadsAsync_UnassignedUploadOfHosterAccountPool_ReturnsPoolNameWithoutHosterRegistration()
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.UnassignedPool.2026-GRP");
        var poolConfig = AddHosterAccountPoolUploadConfig(release, memberIsActive: true);
        AddUnassignedUpload(poolConfig, UploadState.WaitingForArchive);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.SearchUploadsAsync(
            new ReleaseUploadSearchQuery(release.Id),
            CancellationToken.None
        );

        // Assert
        var upload = result.Items.ShouldHaveSingleItem();
        upload.HosterRegistrationName.ShouldBeNull();
        upload.HosterAccountPoolName.ShouldBe("Pool");
        upload.HosterHasUnreadableSecrets.ShouldBeFalse();
    }

    [Test]
    public async Task GetReleaseOverviewAsync_HosterAccountPoolConfig_ReturnsPoolNameAsUploadTargetName()
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.OverviewPool.2026-GRP");
        AddHosterAccountPoolUploadConfig(release, memberIsActive: true);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetReleaseOverviewAsync(release.Id, CancellationToken.None);

        // Assert
        result.ShouldHaveSingleItem().UploadTargetName.ShouldBe("Pool");
    }

    [Test]
    public async Task SearchReleaseResultsAsync_HosterAccountPoolConfig_ReturnsPoolNameAsUploadTargetName()
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.ResultPool.2026-GRP");
        AddHosterAccountPoolUploadConfig(release, memberIsActive: true);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.SearchReleaseResultsAsync(
            new ReleaseSearchQuery(),
            CancellationToken.None
        );

        // Assert
        result
            .Items.ShouldHaveSingleItem()
            .UploadConfigs.ShouldHaveSingleItem()
            .UploadTargetName.ShouldBe("Pool");
    }

    [Test]
    public async Task GetForumPostUploadsAsync_HosterAccountPoolConfig_ReturnsDisplayNameOfPoolHoster()
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.ForumPool.2026-GRP");
        AddHosterAccountPoolUploadConfig(release, memberIsActive: true);
        testData.AddUploadConfig(release, "Single account");
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetForumPostUploadsAsync(release.Id, CancellationToken.None);

        // Assert
        result
            .Select(upload => upload.HosterName)
            .ShouldBe(["Pool hoster display name", "Single account hoster"], ignoreOrder: true);
    }

    private UploadConfig AddHosterAccountPoolUploadConfig(
        Release release,
        bool memberIsActive,
        ArchiveConfig? archiveConfig = null
    )
    {
        var memberRegistration = testData.AddHosterRegistration($"{release.Name} member");
        memberRegistration.IsActive = memberIsActive;
        var uploadConfig = new UploadConfig
        {
            UploadSource = UploadSource.ArchiveFiles,
            Release = release,
            Name = "Pool upload",
            HosterAccountPool = new HosterAccountPool
            {
                Name = "Pool",
                HosterClassName = PoolHosterClassName,
                Members =
                [
                    new HosterAccountPoolMember
                    {
                        HosterRegistration = memberRegistration,
                        Priority = 1,
                    },
                ],
            },
            ArchiveConfig = archiveConfig ?? testData.AddArchiveConfig(release, "Pool archive"),
            LinkCrypters = [],
            Uploads = [],
        };
        DbContext.UploadConfigs.Add(uploadConfig);

        return uploadConfig;
    }

    private void AddCompletedUpload(UploadConfig uploadConfig)
    {
        var upload = testData.AddUpload(
            uploadConfig,
            MidnightUtc,
            MidnightUtc.AddHours(1),
            UploadState.Completed
        );
        testData.AddUploadedFile(upload, "part01.rar", "https://hoster.example/1");
    }

    private void AddUnassignedUpload(UploadConfig uploadConfig, UploadState uploadState)
    {
        DbContext.Uploads.Add(
            new Upload
            {
                UploadConfig = uploadConfig,
                CreatedAt = MidnightUtc.AddHours(2),
                UploadState = uploadState,
                OnlineState = OnlineState.Unknown,
                UploadedFiles = [],
                LinkCrypterContainers = [],
                Notifications = [],
            }
        );
    }
}

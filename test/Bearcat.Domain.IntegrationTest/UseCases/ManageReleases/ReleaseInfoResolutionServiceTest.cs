using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using NfoReleaseInfo = Bearcat.Abstractions.NfoDatabase.ReleaseInfo;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleases;

public class ReleaseInfoResolutionServiceTest : BearcatIntegrationTest
{
    private const string WorkingDatabaseClassName = "WorkingNfoDatabase";
    private const string SerializedConfig = "{\"apiKey\":\"secret\"}";

    private BearcatDbContext dbContext = null!;
    private Mock<INfoDatabaseFactory> nfoDatabaseFactoryMock = null!;
    private ReleaseInfoResolutionService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        nfoDatabaseFactoryMock = new Mock<INfoDatabaseFactory>(MockBehavior.Strict);

        service = new ReleaseInfoResolutionService(
            new ReleaseInfoRepository(dbContext, dbContext, NoOpSecretProtector.Instance),
            nfoDatabaseFactoryMock.Object,
            new Mock<ILogger<ReleaseInfoResolutionService>>().Object
        );
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task ProcessMissingReleaseInfosAsync_ActiveDatabaseReturnsInfo_PersistsReleaseInfo()
    {
        // Arrange
        var release = await AddReleaseAsync("Bearcat.Release.2026-GRP");
        await AddNfoDatabaseRegistrationAsync(WorkingDatabaseClassName, isActive: true);

        var nfoDatabaseMock = SetupNfoDatabase(
            WorkingDatabaseClassName,
            "Bearcat.Release.2026-GRP",
            CreateReleaseInfo("Bearcat.Release.2026-GRP")
        );

        // Act
        var resolvedCount = await service.ProcessMissingReleaseInfosAsync(CancellationToken.None);

        // Assert
        resolvedCount.ShouldBe(1);

        dbContext.ChangeTracker.Clear();
        var persistedInfo = await dbContext
            .ReleaseInfos.Include(info => info.ExternalInfos)
            .SingleAsync();

        persistedInfo.ReleaseId.ShouldBe(release.Id);
        persistedInfo.NfoDatabaseClassName.ShouldBe(WorkingDatabaseClassName);
        persistedInfo.ReleaseName.ShouldBe("Bearcat.Release.2026-GRP");
        persistedInfo.ReleaseDatabaseUrl.ShouldBe("https://www.xrel.to/release/123");
        persistedInfo.SizeNumber.ShouldBe(12);
        persistedInfo.SizeUnit.ShouldBe("GB");
        persistedInfo.VideoType.ShouldBe("WEB");
        persistedInfo.AudioType.ShouldBe("AC3");

        var externalInfo = persistedInfo.ExternalInfos.Single();
        externalInfo.Type.ShouldBe(ExternalInfoType.Movie);
        externalInfo.Title.ShouldBe("Bearcat Movie");
        externalInfo.Urls.ShouldContain(
            url => url.Type == UrlType.Imdb && url.Url == "https://www.imdb.com/de/title/tt1234567"
        );
        externalInfo.Urls.ShouldContain(
            url => url.Type == UrlType.Other && url.Url == "https://www.xrel.to/movie/123"
        );

        nfoDatabaseMock.Verify(
            database =>
                database.GetReleaseInfoAsync(
                    It.IsAny<INfoDatabaseConfig>(),
                    "Bearcat.Release.2026-GRP",
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ProcessMissingReleaseInfosAsync_FirstDatabaseReturnsNull_UsesNextDatabase()
    {
        // Arrange
        const string emptyDatabaseClassName = "EmptyNfoDatabase";
        await AddReleaseAsync("Fallback.Release.2026-GRP");
        await AddNfoDatabaseRegistrationAsync(emptyDatabaseClassName, isActive: true);
        await AddNfoDatabaseRegistrationAsync(WorkingDatabaseClassName, isActive: true);

        var emptyConfigMock = new Mock<INfoDatabaseConfig>(MockBehavior.Strict);
        var emptyDatabaseMock = new Mock<INfoDatabase>(MockBehavior.Strict);
        emptyDatabaseMock
            .Setup(database => database.DeserializeConfig(SerializedConfig))
            .Returns(emptyConfigMock.Object);
        emptyDatabaseMock
            .Setup(database =>
                database.GetReleaseInfoAsync(
                    emptyConfigMock.Object,
                    "Fallback.Release.2026-GRP",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((NfoReleaseInfo?)null);
        nfoDatabaseFactoryMock
            .Setup(factory => factory.Get(emptyDatabaseClassName))
            .Returns(emptyDatabaseMock.Object);

        SetupNfoDatabase(
            WorkingDatabaseClassName,
            "Fallback.Release.2026-GRP",
            CreateReleaseInfo("Fallback.Release.2026-GRP")
        );

        // Act
        var resolvedCount = await service.ProcessMissingReleaseInfosAsync(CancellationToken.None);

        // Assert
        resolvedCount.ShouldBe(1);

        dbContext.ChangeTracker.Clear();
        var persistedInfo = await dbContext.ReleaseInfos.SingleAsync();

        persistedInfo.NfoDatabaseClassName.ShouldBe(WorkingDatabaseClassName);
        emptyDatabaseMock.Verify(
            database =>
                database.GetReleaseInfoAsync(
                    emptyConfigMock.Object,
                    "Fallback.Release.2026-GRP",
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ProcessMissingReleaseInfosAsync_NoActiveRegistration_DoesNothing()
    {
        // Arrange
        await AddReleaseAsync("Inactive.Release.2026-GRP");
        await AddNfoDatabaseRegistrationAsync(WorkingDatabaseClassName, isActive: false);

        // Act
        var resolvedCount = await service.ProcessMissingReleaseInfosAsync(CancellationToken.None);

        // Assert
        resolvedCount.ShouldBe(0);
        (await dbContext.ReleaseInfos.AnyAsync()).ShouldBeFalse();
        nfoDatabaseFactoryMock.Verify(
            factory => factory.Get(It.IsAny<string>()),
            Times.Never
        );
    }

    [Test]
    public async Task TryResolveAndSaveAsync_NewTrackedRelease_PersistsReleaseAndInfo()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync();
        await AddNfoDatabaseRegistrationAsync(WorkingDatabaseClassName, isActive: true);

        var release = new Release
        {
            Name = "New.Tracked.Release.2026-GRP",
            CreatedAt = DateTime.UtcNow,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/tmp/new-tracked-release",
            ReleaseGroupId = releaseGroup.Id,
            ArchiveConfigs = [],
            UploadConfigs = [],
        };
        dbContext.Releases.Add(release);

        SetupNfoDatabase(
            WorkingDatabaseClassName,
            "New.Tracked.Release.2026-GRP",
            CreateReleaseInfo("New.Tracked.Release.2026-GRP")
        );

        // Act
        var resolved = await service.TryResolveAndSaveAsync(release, CancellationToken.None);

        // Assert
        resolved.ShouldBeTrue();

        dbContext.ChangeTracker.Clear();
        var persistedRelease = await dbContext
            .Releases.Include(entity => entity.ReleaseInfos)
            .SingleAsync(entity => entity.Name == "New.Tracked.Release.2026-GRP");

        persistedRelease.ReleaseInfos.Single().ReleaseName.ShouldBe("New.Tracked.Release.2026-GRP");
    }

    private Mock<INfoDatabase> SetupNfoDatabase(
        string className,
        string expectedReleaseName,
        NfoReleaseInfo? releaseInfo
    )
    {
        var configMock = new Mock<INfoDatabaseConfig>(MockBehavior.Strict);
        var nfoDatabaseMock = new Mock<INfoDatabase>(MockBehavior.Strict);
        nfoDatabaseMock
            .Setup(database => database.DeserializeConfig(SerializedConfig))
            .Returns(configMock.Object);
        nfoDatabaseMock
            .Setup(database =>
                database.GetReleaseInfoAsync(
                    configMock.Object,
                    expectedReleaseName,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(releaseInfo);
        nfoDatabaseFactoryMock.Setup(factory => factory.Get(className)).Returns(nfoDatabaseMock.Object);

        return nfoDatabaseMock;
    }

    private async Task<NfoDatabaseRegistration> AddNfoDatabaseRegistrationAsync(
        string className,
        bool isActive
    )
    {
        var registration = new NfoDatabaseRegistration
        {
            NfoDatabaseClassName = className,
            SerializedConfig = SerializedConfig,
            IsActive = isActive,
        };

        dbContext.NfoDatabaseRegistrations.Add(registration);
        await dbContext.SaveChangesAsync();

        return registration;
    }

    private async Task<Release> AddReleaseAsync(string name)
    {
        var releaseGroup = await AddReleaseGroupAsync();
        var release = new Release
        {
            Name = name,
            CreatedAt = DateTime.UtcNow,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = $"/tmp/{name}",
            ReleaseGroupId = releaseGroup.Id,
            ArchiveConfigs = [],
            UploadConfigs = [],
        };

        dbContext.Releases.Add(release);
        await dbContext.SaveChangesAsync();

        return release;
    }

    private async Task<ReleaseGroup> AddReleaseGroupAsync()
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = $"Release group {Guid.NewGuid():N}",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
            Releases = [],
        };

        dbContext.ReleaseGroups.Add(releaseGroup);
        await dbContext.SaveChangesAsync();

        return releaseGroup;
    }

    private static NfoReleaseInfo CreateReleaseInfo(string releaseName)
    {
        return new NfoReleaseInfo(
            ReleaseName: releaseName,
            ReleaseDatabaseUrl: "https://www.xrel.to/release/123",
            Size: new ReleaseInfoSize(12, "GB"),
            VideoType: "WEB",
            AudioType: "AC3",
            ExternalInfos:
            [
                new ExternalInfo(
                    Type: ExternalInfoType.Movie,
                    Title: "Bearcat Movie",
                    Urls:
                    [
                        new Url(UrlType.Imdb, "https://www.imdb.com/de/title/tt1234567"),
                        new Url(UrlType.Other, "https://www.xrel.to/movie/123"),
                    ]
                ),
            ]
        );
    }
}

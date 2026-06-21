using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases;
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
using NfoReleaseInfo = Bearcat.Abstractions.NfoDatabase.ReleaseInfo;
using ReleaseNfo = Bearcat.Abstractions.NfoDatabase.ReleaseNfo;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleases;

public class ReleaseInfoResolutionServiceTest : BearcatIntegrationTest
{
    private const string WorkingDatabaseClassName = "WorkingNfoDatabase";
    private const string NfoProviderDatabaseClassName = "NfoProviderDatabase";
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
            new Mock<ILogger<ReleaseInfoResolutionService>>().Object,
            CreateTimeProvider()
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
        persistedInfo.Genre.ShouldBe("Drama, Sci-Fi");
        persistedInfo.Description.ShouldBe("Bearcat plot");
        persistedInfo.CoverUrl.ShouldBe("https://uploads2.xrel.to/img_cover/movie123.JPG");

        var externalInfo = persistedInfo.ExternalInfos.Single();
        externalInfo.Type.ShouldBe(ExternalInfoType.Movie);
        externalInfo.Title.ShouldBe("Bearcat Movie");
        externalInfo.Urls.ShouldContain(url =>
            url.Type == UrlType.Imdb && url.Url == "https://www.imdb.com/de/title/tt1234567"
        );
        externalInfo.Urls.ShouldContain(url =>
            url.Type == UrlType.Other && url.Url == "https://www.xrel.to/movie/123"
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
    public async Task ProcessMissingReleaseInfosAsync_LocalNfoFileExists_PersistsReleaseNfo()
    {
        // Arrange
        var releaseFolderPath = CreateTempReleaseFolder();
        await File.WriteAllTextAsync(
            Path.Combine(releaseFolderPath, "bearcat.nfo"),
            "local nfo content"
        );
        var release = await AddReleaseAsync("Bearcat.Local.Nfo.2026-GRP", releaseFolderPath);
        await AddNfoDatabaseRegistrationAsync(WorkingDatabaseClassName, isActive: true);

        SetupNfoDatabase(
            WorkingDatabaseClassName,
            "Bearcat.Local.Nfo.2026-GRP",
            CreateReleaseInfo("Bearcat.Local.Nfo.2026-GRP")
        );

        // Act
        var resolvedCount = await service.ProcessMissingReleaseInfosAsync(CancellationToken.None);

        // Assert
        resolvedCount.ShouldBe(1);

        dbContext.ChangeTracker.Clear();
        var persistedInfo = await dbContext
            .ReleaseInfos.Include(info => info.ReleaseNfo)
            .SingleAsync();

        persistedInfo.ReleaseId.ShouldBe(release.Id);
        persistedInfo.ReleaseNfo.ShouldNotBeNull();
        persistedInfo.ReleaseNfo.FileName.ShouldBe("bearcat.nfo");
        persistedInfo.ReleaseNfo.Content.ShouldBe("local nfo content");
    }

    [Test]
    public async Task ProcessMissingReleaseInfosAsync_NoLocalNfo_UsesProviderAfterReleaseInfoDatabase()
    {
        // Arrange
        const string xrelDatabaseClassName = "XrelNfoDatabase";
        var release = await AddReleaseAsync("Bearcat.Remote.Nfo.2026-GRP");
        await AddNfoDatabaseRegistrationAsync(xrelDatabaseClassName, isActive: true);
        await AddNfoDatabaseRegistrationAsync(NfoProviderDatabaseClassName, isActive: true);

        SetupNfoDatabase(
            xrelDatabaseClassName,
            "Bearcat.Remote.Nfo.2026-GRP",
            CreateReleaseInfo("Bearcat.Remote.Nfo.2026-GRP")
        );
        var providerMock = SetupNfoProvider(
            NfoProviderDatabaseClassName,
            "Bearcat.Remote.Nfo.2026-GRP",
            new ReleaseNfo("remote.nfo", "remote nfo content")
        );

        // Act
        var resolvedCount = await service.ProcessMissingReleaseInfosAsync(CancellationToken.None);

        // Assert
        resolvedCount.ShouldBe(1);

        dbContext.ChangeTracker.Clear();
        var persistedInfo = await dbContext
            .ReleaseInfos.Include(info => info.ReleaseNfo)
            .SingleAsync();

        persistedInfo.ReleaseId.ShouldBe(release.Id);
        persistedInfo.NfoDatabaseClassName.ShouldBe(xrelDatabaseClassName);
        persistedInfo.ReleaseNfo.ShouldNotBeNull();
        persistedInfo.ReleaseNfo.FileName.ShouldBe("remote.nfo");
        persistedInfo.ReleaseNfo.Content.ShouldBe("remote nfo content");

        providerMock.Verify(
            provider =>
                provider.GetReleaseNfoAsync(
                    It.IsAny<INfoDatabaseConfig>(),
                    "Bearcat.Remote.Nfo.2026-GRP",
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ProcessMissingReleaseInfosAsync_RemoteNfoAndReleaseFolderExists_WritesNfoFileToDisk()
    {
        // Arrange
        const string xrelDatabaseClassName = "XrelNfoDatabase";
        var releaseFolderPath = CreateTempReleaseFolder();
        var release = await AddReleaseAsync("Bearcat.Remote.Nfo.Save.2026-GRP", releaseFolderPath);
        await AddNfoDatabaseRegistrationAsync(xrelDatabaseClassName, isActive: true);
        await AddNfoDatabaseRegistrationAsync(NfoProviderDatabaseClassName, isActive: true);

        SetupNfoDatabase(
            xrelDatabaseClassName,
            "Bearcat.Remote.Nfo.Save.2026-GRP",
            CreateReleaseInfo("Bearcat.Remote.Nfo.Save.2026-GRP")
        );
        SetupNfoProvider(
            NfoProviderDatabaseClassName,
            "Bearcat.Remote.Nfo.Save.2026-GRP",
            new ReleaseNfo("remote.nfo", "remote nfo content")
        );

        // Act
        var resolvedCount = await service.ProcessMissingReleaseInfosAsync(CancellationToken.None);

        // Assert
        resolvedCount.ShouldBe(1);

        var nfoFilePath = Path.Combine(releaseFolderPath, "remote.nfo");
        File.Exists(nfoFilePath).ShouldBeTrue();
        (await File.ReadAllTextAsync(nfoFilePath)).ShouldBe("remote nfo content");

        dbContext.ChangeTracker.Clear();
        var persistedInfo = await dbContext
            .ReleaseInfos.Include(info => info.ReleaseNfo)
            .SingleAsync();

        persistedInfo.ReleaseId.ShouldBe(release.Id);
        persistedInfo.ReleaseNfo.ShouldNotBeNull();
        persistedInfo.ReleaseNfo.FileName.ShouldBe("remote.nfo");
    }

    [Test]
    public async Task ProcessMissingReleaseInfosAsync_LocalNfoFileExists_DoesNotWriteAdditionalFile()
    {
        // Arrange
        var releaseFolderPath = CreateTempReleaseFolder();
        await File.WriteAllTextAsync(
            Path.Combine(releaseFolderPath, "bearcat.nfo"),
            "local nfo content"
        );
        await AddReleaseAsync("Bearcat.Local.Nfo.Keep.2026-GRP", releaseFolderPath);
        await AddNfoDatabaseRegistrationAsync(WorkingDatabaseClassName, isActive: true);

        SetupNfoDatabase(
            WorkingDatabaseClassName,
            "Bearcat.Local.Nfo.Keep.2026-GRP",
            CreateReleaseInfo("Bearcat.Local.Nfo.Keep.2026-GRP")
        );

        // Act
        await service.ProcessMissingReleaseInfosAsync(CancellationToken.None);

        // Assert
        var nfoFiles = Directory.GetFiles(releaseFolderPath, "*.nfo");
        nfoFiles.Length.ShouldBe(1);
        Path.GetFileName(nfoFiles[0]).ShouldBe("bearcat.nfo");
        (await File.ReadAllTextAsync(nfoFiles[0])).ShouldBe("local nfo content");
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
        emptyDatabaseMock.SetupGet(database => database.ResolutionPriority).Returns(100);
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
        nfoDatabaseFactoryMock.Verify(factory => factory.Get(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task ProcessMissingReleaseInfosAsync_MissingInfosAcrossBatches_MarksCheckedAndSkipsRecentlyChecked()
    {
        // Arrange
        await AddNfoDatabaseRegistrationAsync(WorkingDatabaseClassName, isActive: true);
        var recentlyCheckedAt = DateTime.UtcNow.AddHours(-1);
        var skippedRelease = await AddReleaseAsync(
            "Recently.Checked.Release.2026-GRP",
            releaseInfoCheckedAt: recentlyCheckedAt
        );
        var unresolvedReleases = new List<Release>();

        for (var i = 0; i < 50; i++)
        {
            unresolvedReleases.Add(await AddReleaseAsync($"Unresolved.Release.{i:00}.2026-GRP"));
        }

        var resolvedRelease = await AddReleaseAsync("Resolved.Release.2026-GRP");
        var releaseInfosByReleaseName = unresolvedReleases
            .Select(release => release.Name)
            .ToDictionary(name => name, _ => (NfoReleaseInfo?)null);
        releaseInfosByReleaseName[resolvedRelease.Name] = CreateReleaseInfo(resolvedRelease.Name);
        var nfoDatabaseMock = SetupNfoDatabase(releaseInfosByReleaseName);

        // Act
        await service.ProcessMissingReleaseInfosAsync(CancellationToken.None);
        await service.ProcessMissingReleaseInfosAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var releases = await dbContext
            .Releases.Include(release => release.ReleaseInfo)
            .ToDictionaryAsync(release => release.Name);

        foreach (var release in unresolvedReleases)
        {
            releases[release.Name].ReleaseInfo.ShouldBeNull();
            releases[release.Name].ReleaseInfoCheckedAt.ShouldNotBeNull();
            releases[release.Name]
                .ReleaseInfoCheckedAt.GetValueOrDefault()
                .ShouldBeGreaterThan(release.CreatedAt);
        }

        var persistedResolvedRelease = releases[resolvedRelease.Name];
        persistedResolvedRelease.ReleaseInfo.ShouldNotBeNull();
        persistedResolvedRelease.ReleaseInfo.ReleaseName.ShouldBe(resolvedRelease.Name);
        persistedResolvedRelease.ReleaseInfoCheckedAt.ShouldNotBeNull();
        persistedResolvedRelease
            .ReleaseInfoCheckedAt.GetValueOrDefault()
            .ShouldBeGreaterThan(resolvedRelease.CreatedAt);

        var persistedSkippedRelease = releases[skippedRelease.Name];
        persistedSkippedRelease.ReleaseInfo.ShouldBeNull();
        persistedSkippedRelease.ReleaseInfoCheckedAt.ShouldNotBeNull();
        persistedSkippedRelease.ReleaseInfoCheckedAt.Value.ShouldBe(
            recentlyCheckedAt,
            TimeSpan.FromSeconds(1)
        );

        foreach (var release in unresolvedReleases.Append(resolvedRelease))
        {
            nfoDatabaseMock.Verify(
                database =>
                    database.GetReleaseInfoAsync(
                        It.IsAny<INfoDatabaseConfig>(),
                        release.Name,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        }

        nfoDatabaseMock.Verify(
            database =>
                database.GetReleaseInfoAsync(
                    It.IsAny<INfoDatabaseConfig>(),
                    skippedRelease.Name,
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task SaveNfoFileAsync_NoLocalNfo_WritesFileWithStoredFileName()
    {
        // Arrange
        var releaseFolderPath = CreateTempReleaseFolder();

        // Act
        var result = await ReleaseNfoService.SaveNfoFileAsync(
            releaseFolderPath,
            "bearcat.nfo",
            "Bearcat.Release.2026-GRP",
            "nfo content",
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(ReleaseNfoFileSaveResult.Saved);
        var filePath = Path.Combine(releaseFolderPath, "bearcat.nfo");
        File.Exists(filePath).ShouldBeTrue();
        (await File.ReadAllTextAsync(filePath)).ShouldBe("nfo content");
    }

    [Test]
    public async Task SaveNfoFileAsync_LocalNfoExists_DoesNotOverwrite()
    {
        // Arrange
        var releaseFolderPath = CreateTempReleaseFolder();
        var filePath = Path.Combine(releaseFolderPath, "existing.nfo");
        await File.WriteAllTextAsync(filePath, "existing content");

        // Act
        var result = await ReleaseNfoService.SaveNfoFileAsync(
            releaseFolderPath,
            "bearcat.nfo",
            "Bearcat.Release.2026-GRP",
            "new content",
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(ReleaseNfoFileSaveResult.AlreadyExists);
        (await File.ReadAllTextAsync(filePath)).ShouldBe("existing content");
        File.Exists(Path.Combine(releaseFolderPath, "bearcat.nfo")).ShouldBeFalse();
    }

    [Test]
    public async Task SaveNfoFileAsync_StoredFileNameMissing_UsesReleaseName()
    {
        // Arrange
        var releaseFolderPath = CreateTempReleaseFolder();

        // Act
        var result = await ReleaseNfoService.SaveNfoFileAsync(
            releaseFolderPath,
            string.Empty,
            "Bearcat.Release.2026-GRP",
            "nfo content",
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(ReleaseNfoFileSaveResult.Saved);
        var filePath = Path.Combine(releaseFolderPath, "Bearcat.Release.2026-GRP.nfo");
        File.Exists(filePath).ShouldBeTrue();
        (await File.ReadAllTextAsync(filePath)).ShouldBe("nfo content");
    }

    [Test]
    public async Task TryResolve_NewTrackedRelease_AddsReleaseInfoToRelease()
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
        var resolved = await service.TryResolveAsync(release, CancellationToken.None);

        // Assert
        resolved.ShouldBeTrue();
        var releaseInfo = release.ReleaseInfo;

        releaseInfo.ShouldNotBeNull();
        releaseInfo.ReleaseName.ShouldBe("New.Tracked.Release.2026-GRP");
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
            .SetupGet(database => database.ResolutionPriority)
            .Returns(className.Equals("XrelNfoDatabase", StringComparison.Ordinal) ? 0 : 100);
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
        nfoDatabaseFactoryMock
            .Setup(factory => factory.Get(className))
            .Returns(nfoDatabaseMock.Object);

        return nfoDatabaseMock;
    }

    private Mock<INfoDatabase> SetupNfoDatabase(
        IReadOnlyDictionary<string, NfoReleaseInfo?> releaseInfosByReleaseName
    )
    {
        var configMock = new Mock<INfoDatabaseConfig>(MockBehavior.Strict);
        var nfoDatabaseMock = new Mock<INfoDatabase>(MockBehavior.Strict);
        nfoDatabaseMock.SetupGet(database => database.ResolutionPriority).Returns(100);
        nfoDatabaseMock
            .Setup(database => database.DeserializeConfig(SerializedConfig))
            .Returns(configMock.Object);
        nfoDatabaseMock
            .Setup(database =>
                database.GetReleaseInfoAsync(
                    configMock.Object,
                    It.Is<string>(releaseName =>
                        releaseInfosByReleaseName.ContainsKey(releaseName)
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (INfoDatabaseConfig _, string releaseName, CancellationToken _) =>
                    releaseInfosByReleaseName[releaseName]
            );
        nfoDatabaseFactoryMock
            .Setup(factory => factory.Get(WorkingDatabaseClassName))
            .Returns(nfoDatabaseMock.Object);

        return nfoDatabaseMock;
    }

    private Mock<INfoProvider> SetupNfoProvider(
        string className,
        string expectedReleaseName,
        ReleaseNfo? releaseNfo
    )
    {
        var configMock = new Mock<INfoDatabaseConfig>(MockBehavior.Strict);
        var nfoDatabaseMock = new Mock<INfoDatabase>(MockBehavior.Strict);
        var providerMock = nfoDatabaseMock.As<INfoProvider>();
        nfoDatabaseMock.SetupGet(database => database.ResolutionPriority).Returns(100);
        nfoDatabaseMock
            .Setup(database => database.DeserializeConfig(SerializedConfig))
            .Returns(configMock.Object);
        providerMock
            .Setup(provider =>
                provider.GetReleaseNfoAsync(
                    configMock.Object,
                    expectedReleaseName,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(releaseNfo);
        nfoDatabaseFactoryMock
            .Setup(factory => factory.Get(className))
            .Returns(nfoDatabaseMock.Object);

        return providerMock;
    }

    private async Task AddNfoDatabaseRegistrationAsync(string className, bool isActive)
    {
        var registration = new NfoDatabaseRegistration
        {
            NfoDatabaseClassName = className,
            SerializedConfig = SerializedConfig,
            IsActive = isActive,
        };

        dbContext.NfoDatabaseRegistrations.Add(registration);
        await dbContext.SaveChangesAsync();
    }

    private async Task<Release> AddReleaseAsync(
        string name,
        string? releaseFolderPath = null,
        DateTime? releaseInfoCheckedAt = null
    )
    {
        var releaseGroup = await AddReleaseGroupAsync();
        var release = new Release
        {
            Name = name,
            CreatedAt = DateTime.UtcNow,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = releaseFolderPath ?? $"/tmp/{name}",
            ReleaseGroupId = releaseGroup.Id,
            ReleaseInfoCheckedAt = releaseInfoCheckedAt,
            ArchiveConfigs = [],
            UploadConfigs = [],
        };

        dbContext.Releases.Add(release);
        await dbContext.SaveChangesAsync();

        return release;
    }

    private static string CreateTempReleaseFolder()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bearcat-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
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
            Genre: "Drama, Sci-Fi",
            Description: "Bearcat plot",
            CoverUrl: "https://uploads2.xrel.to/img_cover/movie123.JPG",
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

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }
}

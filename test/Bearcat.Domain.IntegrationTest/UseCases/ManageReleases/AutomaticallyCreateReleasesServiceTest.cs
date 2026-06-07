using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.FileSystem;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using NfoReleaseInfo = Bearcat.Abstractions.NfoDatabase.ReleaseInfo;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleases;

public class AutomaticallyCreateReleasesServiceTest : BearcatIntegrationTest
{
    private const string WorkingDatabaseClassName = "WorkingNfoDatabase";
    private const string SerializedConfig = "{\"apiKey\":\"secret\"}";

    private BearcatDbContext dbContext = null!;
    private Mock<INfoDatabaseFactory> nfoDatabaseFactoryMock = null!;
    private string tempRootPath = null!;
    private AutomaticallyCreateReleasesService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        nfoDatabaseFactoryMock = new Mock<INfoDatabaseFactory>(MockBehavior.Strict);
        tempRootPath = Path.Combine(Path.GetTempPath(), $"bearcat-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRootPath);
        var archiverFactory = new Mock<IArchiverFactory>();
        archiverFactory
            .Setup(f => f.GetArchivers())
            .Returns([new ArchiverDto("RAR", "RarArchiver", ".rar")]);

        service = new AutomaticallyCreateReleasesService(
            new ReleaseFolderAutomationRepository(dbContext, dbContext),
            new FileSystemService(),
            CreateReleaseInfoResolutionService(),
            CreateTimeProvider(),
            archiverFactory.Object
        );
    }

    [TearDown]
    public async Task DisposeResourcesAsync()
    {
        await dbContext.DisposeAsync();

        if (Directory.Exists(tempRootPath))
        {
            Directory.Delete(tempRootPath, recursive: true);
        }
    }

    [Test]
    public async Task ProcessAsync_MatchingDirectFolderWithoutRelease_CreatesReleaseFromTemplate()
    {
        // Arrange
        var releaseTemplate = await AddReleaseTemplateAsync();
        var matchingFolder = Directory.CreateDirectory(
            Path.Combine(tempRootPath, "Bearcat.Release.1080p")
        );
        var nonMatchingFolder = Directory.CreateDirectory(
            Path.Combine(tempRootPath, "Bearcat.Release.720p")
        );
        var existingReleaseFolder = Directory.CreateDirectory(
            Path.Combine(tempRootPath, "Existing.Release.1080p")
        );
        var nestedContainer = Directory.CreateDirectory(Path.Combine(tempRootPath, "Nested"));
        Directory.CreateDirectory(Path.Combine(nestedContainer.FullName, "Nested.Release.1080p"));

        await AddAutomationAsync(releaseTemplate.ReleaseTemplateId, tempRootPath, "*1080p*");
        await AddReleaseAsync(releaseTemplate.ReleaseGroupId, existingReleaseFolder.FullName);

        // Act
        var result = await service.ProcessAsync(CancellationToken.None);

        // Assert
        result.ShouldBe(1);

        var releases = await dbContext
            .Releases.AsSplitQuery()
            .Include(release => release.ArchiveConfigs)
            .Include(release => release.UploadConfigs)
                .ThenInclude(uploadConfig => uploadConfig.LinkCrypters)
            .OrderBy(release => release.Name)
            .ToListAsync();

        releases.Count.ShouldBe(2);
        releases.ShouldContain(release =>
            release.ReleaseFolderPath == existingReleaseFolder.FullName
        );
        releases.ShouldNotContain(release =>
            release.ReleaseFolderPath == nonMatchingFolder.FullName
        );
        releases.ShouldNotContain(release =>
            release.ReleaseFolderPath
            == Path.Combine(nestedContainer.FullName, "Nested.Release.1080p")
        );

        var createdRelease = releases.Single(release =>
            release.ReleaseFolderPath == matchingFolder.FullName
        );
        createdRelease.Name.ShouldBe("Bearcat.Release.1080p");
        createdRelease.ReleaseType.ShouldBe(ReleaseType.Managed);
        createdRelease.ReleaseGroupId.ShouldBe(releaseTemplate.ReleaseGroupId);

        var archiveConfig = createdRelease.ArchiveConfigs.Single();
        archiveConfig.Name.ShouldBe("RAR Forum A");
        archiveConfig.ArchiveFilesBasePath.ShouldBe(Path.Combine(tempRootPath, "archives"));
        archiveConfig.ArchiverName.ShouldBe("rar");
        archiveConfig.ArchivePassword.ShouldBe("archive-secret");
        archiveConfig.ArchiveFileSizeMb.ShouldBe(1024);
        archiveConfig.ArchiveNamePrefix.ShouldBe(createdRelease.Name);

        var uploadConfig = createdRelease.UploadConfigs.Single();
        uploadConfig.Name.ShouldBe("Primary hoster");
        uploadConfig.ArchiveConfigId.ShouldBe(archiveConfig.Id);
        uploadConfig.HosterRegistrationId.ShouldBe(releaseTemplate.HosterRegistrationId);
        uploadConfig.LinksDistributedTo.ShouldBe(["forum-a", "forum-b"]);
        uploadConfig
            .LinkCrypters.Single()
            .LinkCrypterRegistrationId.ShouldBe(releaseTemplate.LinkCrypterRegistrationId);
        uploadConfig.LinkCrypters.Single().Password.ShouldBe("container-secret");

        var notification = await dbContext.Notifications.SingleAsync();
        notification.NotificationType.ShouldBe(NotificationType.Info);
        notification.Message.ShouldBe(
            "Release 'Bearcat.Release.1080p' was created automatically from template 'Managed template'."
        );
    }

    [Test]
    public async Task ProcessAsync_ReleaseInfoIsResolved_PersistsReleaseInfoWithCreatedRelease()
    {
        // Arrange
        var releaseTemplate = await AddReleaseTemplateAsync();
        var releaseFolder = Directory.CreateDirectory(
            Path.Combine(tempRootPath, "Bearcat.Release.1080p")
        );
        await AddAutomationAsync(releaseTemplate.ReleaseTemplateId, tempRootPath, "*1080p*");
        await AddNfoDatabaseRegistrationAsync(WorkingDatabaseClassName, isActive: true);
        SetupNfoDatabase(
            WorkingDatabaseClassName,
            "Bearcat.Release.1080p",
            CreateReleaseInfo("Bearcat.Release.1080p")
        );

        // Act
        var result = await service.ProcessAsync(CancellationToken.None);

        // Assert
        result.ShouldBe(1);

        dbContext.ChangeTracker.Clear();
        var release = await dbContext
            .Releases.AsSplitQuery()
            .Include(release => release.ReleaseInfo)
                .ThenInclude(info => info!.ExternalInfos)
            .SingleAsync(release => release.ReleaseFolderPath == releaseFolder.FullName);

        var releaseInfo = release.ReleaseInfo.ShouldNotBeNull();
        releaseInfo.NfoDatabaseClassName.ShouldBe(WorkingDatabaseClassName);
        releaseInfo.ReleaseName.ShouldBe("Bearcat.Release.1080p");
        releaseInfo.ReleaseDatabaseUrl.ShouldBe("https://www.xrel.to/release/123");
        releaseInfo.SizeNumber.ShouldBe(12);
        releaseInfo.SizeUnit.ShouldBe("GB");
        releaseInfo.VideoType.ShouldBe("WEB");
        releaseInfo.AudioType.ShouldBe("AC3");
        releaseInfo.Genre.ShouldBe("Drama, Sci-Fi");
        releaseInfo.Description.ShouldBe("Bearcat plot");
        releaseInfo.CoverUrl.ShouldBe("https://uploads2.xrel.to/img_cover/movie123.JPG");

        var externalInfo = releaseInfo.ExternalInfos.Single();
        externalInfo.Type.ShouldBe(ExternalInfoType.Movie);
        externalInfo.Title.ShouldBe("Bearcat Movie");
        externalInfo.Urls.ShouldContain(url =>
            url.Type == UrlType.Imdb && url.Url == "https://www.imdb.com/de/title/tt1234567"
        );
        externalInfo.Urls.ShouldContain(url =>
            url.Type == UrlType.Other && url.Url == "https://www.xrel.to/movie/123"
        );
    }

    [Test]
    public async Task ProcessAsync_AutomationIsDisabled_DoesNotCreateRelease()
    {
        // Arrange
        var releaseTemplate = await AddReleaseTemplateAsync();
        Directory.CreateDirectory(Path.Combine(tempRootPath, "Bearcat.Release.1080p"));
        await AddAutomationAsync(
            releaseTemplate.ReleaseTemplateId,
            tempRootPath,
            "*1080p*",
            isEnabled: false
        );

        // Act
        var result = await service.ProcessAsync(CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        var releaseExists = await dbContext.Releases.AnyAsync();
        var notificationExists = await dbContext.Notifications.AnyAsync();

        releaseExists.ShouldBeFalse();
        notificationExists.ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_EnabledAutomationHasNoMatchingFolders_DoesNotCreateRelease()
    {
        // Arrange
        var releaseTemplate = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        Directory.CreateDirectory(Path.Combine(tempRootPath, "Bearcat.Release.720p"));
        await AddAutomationAsync(releaseTemplate.ReleaseTemplateId, tempRootPath, "*1080p*");

        // Act
        var result = await service.ProcessAsync(CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        (await dbContext.Releases.AnyAsync()).ShouldBeFalse();
        (await dbContext.Notifications.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_AutomationHasNoPattern_CreatesReleaseForDirectFolder()
    {
        // Arrange
        var releaseTemplate = await AddReleaseTemplateAsync();
        var releaseBasePath = Directory.CreateDirectory(Path.Combine(tempRootPath, "release-base"));
        var releaseFolder = Directory.CreateDirectory(
            Path.Combine(releaseBasePath.FullName, "Bearcat.Release.720p")
        );
        await AddAutomationAsync(releaseTemplate.ReleaseTemplateId, releaseBasePath.FullName, null);

        // Act
        var result = await service.ProcessAsync(CancellationToken.None);

        // Assert
        result.ShouldBe(1);

        var release = await dbContext.Releases.SingleAsync();
        release.Name.ShouldBe("Bearcat.Release.720p");
        release.ReleaseFolderPath.ShouldBe(releaseFolder.FullName);
    }

    [Test]
    public async Task ProcessAsync_UnmanagedAutomation_CreatesReleaseWithFixedArchive()
    {
        // Arrange
        var releaseTemplate = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        var releaseFolder = Directory.CreateDirectory(
            Path.Combine(tempRootPath, "Bearcat.Release.Unmanaged")
        );
        await File.WriteAllTextAsync(
            Path.Combine(releaseFolder.FullName, "archive.part1.rar"),
            "1"
        );
        await File.WriteAllTextAsync(
            Path.Combine(releaseFolder.FullName, "archive.part2.rar"),
            "2"
        );
        await AddAutomationAsync(releaseTemplate.ReleaseTemplateId, tempRootPath, "*Unmanaged");

        // Act
        var result = await service.ProcessAsync(CancellationToken.None);

        // Assert
        result.ShouldBe(1);
        var release = await dbContext
            .Releases.AsSplitQuery()
            .Include(r => r.ArchiveConfigs)
                .ThenInclude(c => c.Archives)
                    .ThenInclude(a => a.ArchiveFiles)
            .Include(r => r.UploadConfigs)
            .SingleAsync();
        var archiveConfig = release.ArchiveConfigs.Single();
        var archive = archiveConfig.Archives.Single();

        release.ReleaseType.ShouldBe(ReleaseType.Unmanaged);
        archiveConfig.ArchiverName.ShouldBe("RarArchiver");
        archiveConfig.ArchiveFilesBasePath.ShouldBe(releaseFolder.FullName);
        archive.ArchiveFolderPath.ShouldBe(releaseFolder.FullName);
        archive.ArchiveState.ShouldBe(ArchiveState.Created);
        archive.ArchiveFiles.Count.ShouldBe(2);
        release.UploadConfigs.Single().ArchiveConfigId.ShouldBe(archiveConfig.Id);
    }

    private async Task<ReleaseTemplateSeed> AddReleaseTemplateAsync(
        ReleaseType releaseType = ReleaseType.Managed
    )
    {
        var archiveBasePath = Directory
            .CreateDirectory(Path.Combine(tempRootPath, "archives"))
            .FullName;
        var releaseGroup = new ReleaseGroup
        {
            Name = "Managed releases",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };
        var hosterRegistration = new HosterRegistration
        {
            Name = "Primary hoster",
            SerializedConfig = "{}",
            HosterClassName = "TestHoster",
            IsActive = true,
        };
        var linkCrypterRegistration = new LinkCrypterRegistration
        {
            Name = "Main crypter",
            LinkCrypterClassName = "TestCrypter",
            SerializedConfig = "{}",
            IsActive = true,
        };
        var releaseTemplate = new ReleaseTemplate
        {
            Name = "Managed template",
            ReleaseType = releaseType,
            ReleaseGroup = releaseGroup,
            ArchiveConfigTemplates =
            [
                new ArchiveConfigTemplate
                {
                    Name = "RAR Forum A",
                    ArchiveFilesBasePath = archiveBasePath,
                    ArchiverName = "rar",
                    ArchivePassword = "archive-secret",
                    ArchiveFileSizeMb = 1024,
                    UseReleaseNameAsArchiveName = true,
                },
            ],
        };
        releaseTemplate.UploadConfigTemplates =
        [
            new UploadConfigTemplate
            {
                ReleaseTemplate = releaseTemplate,
                ArchiveConfigTemplate = releaseTemplate.ArchiveConfigTemplates.Single(),
                HosterRegistration = hosterRegistration,
                LinksDistributedTo = ["forum-a", "forum-b"],
                LinkCrypterTemplates =
                [
                    new UploadConfigLinkCrypterTemplate
                    {
                        LinkCrypterRegistration = linkCrypterRegistration,
                        Password = "container-secret",
                    },
                ],
            },
        ];

        dbContext.ReleaseTemplates.Add(releaseTemplate);
        await dbContext.SaveChangesAsync();

        return new ReleaseTemplateSeed(
            releaseTemplate.Id,
            releaseGroup.Id,
            hosterRegistration.Id,
            linkCrypterRegistration.Id
        );
    }

    private async Task AddAutomationAsync(
        int releaseTemplateId,
        string basePath,
        string? folderNamePattern,
        bool isEnabled = true
    )
    {
        dbContext.ReleaseFolderAutomations.Add(
            new ReleaseFolderAutomation
            {
                BasePath = basePath,
                FolderNamePattern = folderNamePattern,
                ReleaseTemplateId = releaseTemplateId,
                IsEnabled = isEnabled,
            }
        );
        await dbContext.SaveChangesAsync();
    }

    private async Task AddReleaseAsync(int releaseGroupId, string releaseFolderPath)
    {
        dbContext.Releases.Add(
            new Release
            {
                Name = Path.GetFileName(releaseFolderPath),
                ReleaseType = ReleaseType.Managed,
                ReleaseGroupId = releaseGroupId,
                ReleaseFolderPath = releaseFolderPath,
                ArchiveConfigs = [],
                UploadConfigs = [],
            }
        );
        await dbContext.SaveChangesAsync();
    }

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }

    private ReleaseInfoResolutionService CreateReleaseInfoResolutionService()
    {
        return new ReleaseInfoResolutionService(
            new ReleaseInfoRepository(dbContext, dbContext, NoOpSecretProtector.Instance),
            nfoDatabaseFactoryMock.Object,
            NullLogger<ReleaseInfoResolutionService>.Instance,
            CreateTimeProvider()
        );
    }

    private void SetupNfoDatabase(string className,
        string expectedReleaseName,
        NfoReleaseInfo? releaseInfo)
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
                    expectedReleaseName,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(releaseInfo);
        nfoDatabaseFactoryMock
            .Setup(factory => factory.Get(className))
            .Returns(nfoDatabaseMock.Object);
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

    private sealed record ReleaseTemplateSeed(
        int ReleaseTemplateId,
        int ReleaseGroupId,
        int HosterRegistrationId,
        int LinkCrypterRegistrationId
    );
}

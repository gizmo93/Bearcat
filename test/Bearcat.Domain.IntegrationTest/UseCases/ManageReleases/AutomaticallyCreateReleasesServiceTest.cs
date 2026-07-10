using System.Linq.Expressions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Media;
using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Abstractions.SeriesDatabase;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageNotifications;
using Bearcat.Domain.UseCases.ManageReleaseCollections;
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
    private int stabilityMinutes;
    private int minimumFolderSizeMegabytes;

    [SetUp]
    public void Setup()
    {
        stabilityMinutes = 0;
        minimumFolderSizeMegabytes = 0;
        dbContext = Database.CreateDbContext();
        nfoDatabaseFactoryMock = new Mock<INfoDatabaseFactory>(MockBehavior.Strict);
        tempRootPath = Path.Combine(Path.GetTempPath(), $"bearcat-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(tempRootPath);

        var archiverFactory = new Mock<IArchiverFactory>();

        archiverFactory
            .Setup(f => f.GetArchivers())
            .Returns([new ArchiverDto("RAR", "RarArchiver", ".rar")]);

        var notificationRepository = new NotificationRepository(dbContext);

        var notificationService = new NotificationService(
            repository: notificationRepository,
            timeProvider: CreateTimeProvider()
        );

        service = new AutomaticallyCreateReleasesService(
            repository: new ReleaseFolderAutomationRepository(dbContext, dbContext),
            fileSystemService: new FileSystemService(),
            releaseInfoResolutionService: CreateReleaseInfoResolutionService(),
            mediaMetadataService: CreateMediaMetadataService(),
            timeProvider: CreateTimeProvider(),
            archiverFactory: archiverFactory.Object,
            releaseCollectionAssignmentService: new ReleaseCollectionAssignmentService(
                new ReleaseCollectionRepository(
                    dbRead: dbContext,
                    dbWrite: dbContext,
                    seriesDatabaseFactory: Mock.Of<ISeriesDatabaseFactory>()
                ),
                CreateTimeProvider()
            ),
            configuration: CreateConfigurationProvider(),
            notificationService: notificationService
        );
    }

    private IApplicationConfigurationProvider CreateConfigurationProvider()
    {
        var configurationProvider = new Mock<IApplicationConfigurationProvider>();
        configurationProvider
            .Setup(provider =>
                provider.GetValue(
                    It.Is<Expression<Func<FolderAutomationConfiguration, int>>>(selector =>
                        SelectsMember(
                            selector,
                            nameof(FolderAutomationConfiguration.StabilityMinutes)
                        )
                    )
                )
            )
            .Returns(() => stabilityMinutes);
        configurationProvider
            .Setup(provider =>
                provider.GetValue(
                    It.Is<Expression<Func<FolderAutomationConfiguration, int>>>(selector =>
                        SelectsMember(
                            selector,
                            nameof(FolderAutomationConfiguration.MinimumFolderSizeMegabytes)
                        )
                    )
                )
            )
            .Returns(() => minimumFolderSizeMegabytes);

        return configurationProvider.Object;
    }

    private static bool SelectsMember(
        Expression<Func<FolderAutomationConfiguration, int>> selector,
        string memberName
    )
    {
        return selector.Body is MemberExpression member && member.Member.Name == memberName;
    }

    private async Task<int> ProcessUntilStableAsync()
    {
        await service.ProcessAsync(CancellationToken.None);
        return await service.ProcessAsync(CancellationToken.None);
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

        await AddAutomationAsync(
            releaseTemplate.ReleaseTemplateId,
            tempRootPath,
            "*1080p*",
            primaryLanguageCode: "de"
        );
        await AddReleaseAsync(releaseTemplate.ReleaseGroupId, existingReleaseFolder.FullName);

        // Act
        var result = await ProcessUntilStableAsync();

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
        createdRelease.PrimaryLanguageCode.ShouldBe("de");

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
        uploadConfig
            .LinkCrypters.Single()
            .LinkCrypterRegistrationId.ShouldBe(releaseTemplate.LinkCrypterRegistrationId);
        uploadConfig.LinkCrypters.Single().Password.ShouldBe("container-secret");

        var notification = await dbContext.Notifications.SingleAsync();
        notification.NotificationType.ShouldBe(NotificationType.Info);
        notification.Message.ShouldBe(
            "Release 'Bearcat.Release.1080p' was created automatically from template 'Managed template'"
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
        var result = await ProcessUntilStableAsync();

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
    public async Task ProcessAsync_MultipleEpisodesInSameTick_ReusesPendingReleaseCollection()
    {
        // Arrange
        var releaseTemplate = await AddReleaseTemplateAsync();
        var template = await dbContext.ReleaseTemplates.SingleAsync(template =>
            template.Id == releaseTemplate.ReleaseTemplateId
        );
        template.ReleaseCollectionDetectionMode =
            ReleaseCollectionDetectionMode.SeriesEpisodePattern;
        await dbContext.SaveChangesAsync();

        Directory.CreateDirectory(
            Path.Combine(
                tempRootPath,
                "Bodies.2023.S01E01.German.DL.EAC3.1080p.DV.HDR.NF.WEB.H265-ZeroTwo"
            )
        );
        Directory.CreateDirectory(
            Path.Combine(
                tempRootPath,
                "Bodies.2023.S01E02.German.DL.EAC3.1080p.DV.HDR.NF.WEB.H265-ZeroTwo"
            )
        );
        Directory.CreateDirectory(
            Path.Combine(
                tempRootPath,
                "Bodies.2023.S01E03.German.DL.EAC3.1080p.DV.HDR.NF.WEB.H265-ZeroTwo"
            )
        );
        await AddAutomationAsync(releaseTemplate.ReleaseTemplateId, tempRootPath, "Bodies.*");

        // Act
        var result = await ProcessUntilStableAsync();

        // Assert
        result.ShouldBe(3);

        var releaseCollection = await dbContext
            .ReleaseCollections.Include(collection => collection.Releases)
            .SingleAsync();

        releaseCollection.Key.ShouldBe(
            "bodies.2023.s01.german.dl.eac3.1080p.dv.hdr.nf.web.h265.zerotwo"
        );
        releaseCollection.Releases.Count.ShouldBe(3);
    }

    [Test]
    public async Task ProcessAsync_TemplateWithCollectionImageConfig_MaterializesDeduplicatedConfigOnCollection()
    {
        // Arrange
        var releaseTemplate = await AddReleaseTemplateAsync();
        var imageHosterRegistration = await AddImageHosterRegistrationAsync();
        var template = await dbContext.ReleaseTemplates.SingleAsync(template =>
            template.Id == releaseTemplate.ReleaseTemplateId
        );
        template.ReleaseCollectionDetectionMode =
            ReleaseCollectionDetectionMode.SeriesEpisodePattern;
        template.CollectionImageUploadConfigTemplates =
        [
            new CollectionImageUploadConfigTemplate
            {
                ImageHosterRegistrationId = imageHosterRegistration.Id,
                Name = "Series cover",
            },
        ];
        await dbContext.SaveChangesAsync();

        Directory.CreateDirectory(
            Path.Combine(
                tempRootPath,
                "Bodies.2023.S01E01.German.DL.EAC3.1080p.DV.HDR.NF.WEB.H265-ZeroTwo"
            )
        );
        Directory.CreateDirectory(
            Path.Combine(
                tempRootPath,
                "Bodies.2023.S01E02.German.DL.EAC3.1080p.DV.HDR.NF.WEB.H265-ZeroTwo"
            )
        );
        await AddAutomationAsync(releaseTemplate.ReleaseTemplateId, tempRootPath, "Bodies.*");

        // Act
        var result = await ProcessUntilStableAsync();

        // Assert
        result.ShouldBe(2);

        var releaseCollection = await dbContext
            .ReleaseCollections.Include(collection => collection.ImageUploadConfigs)
            .SingleAsync();

        var config = releaseCollection.ImageUploadConfigs.ShouldHaveSingleItem();
        config.Name.ShouldBe("Series cover");
        config.ImageHosterRegistrationId.ShouldBe(imageHosterRegistration.Id);
        config.ReleaseId.ShouldBeNull();
        config.ReleaseCollectionId.ShouldBe(releaseCollection.Id);
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
        var result = await ProcessUntilStableAsync();

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
        var result = await ProcessUntilStableAsync();

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
        var result = await ProcessUntilStableAsync();

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
        var result = await ProcessUntilStableAsync();

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

    [Test]
    public async Task ProcessAsync_UnmanagedReleaseAlreadyExists_DoesNotRecreateForSameArchiveFolder()
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
        var created = await ProcessUntilStableAsync();
        var firstRerun = await service.ProcessAsync(CancellationToken.None);
        var secondRerun = await service.ProcessAsync(CancellationToken.None);

        // Assert
        created.ShouldBe(1);
        firstRerun.ShouldBe(0);
        secondRerun.ShouldBe(0);
        (await dbContext.Releases.CountAsync()).ShouldBe(1);
        (await dbContext.ReleaseFolderObservations.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_FirstSightingOfFolder_RecordsObservationButCreatesNoRelease()
    {
        // Arrange
        var releaseTemplate = await AddReleaseTemplateAsync();
        var folder = Directory.CreateDirectory(Path.Combine(tempRootPath, "Bearcat.Release.1080p"));
        await AddAutomationAsync(releaseTemplate.ReleaseTemplateId, tempRootPath, "*1080p*");

        // Act
        var result = await service.ProcessAsync(CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        (await dbContext.Releases.AnyAsync()).ShouldBeFalse();
        (await dbContext.Notifications.AnyAsync()).ShouldBeFalse();

        var observation = await dbContext.ReleaseFolderObservations.SingleAsync();
        observation.FolderPath.ShouldBe(folder.FullName);
    }

    [Test]
    public async Task ProcessAsync_FolderContentChangesBetweenTicks_DoesNotCreateUntilStable()
    {
        // Arrange
        var releaseTemplate = await AddReleaseTemplateAsync();
        var folder = Directory.CreateDirectory(Path.Combine(tempRootPath, "Bearcat.Release.1080p"));
        await AddAutomationAsync(releaseTemplate.ReleaseTemplateId, tempRootPath, "*1080p*");

        // Act
        var firstTick = await service.ProcessAsync(CancellationToken.None);

        await File.WriteAllTextAsync(Path.Combine(folder.FullName, "video.mkv"), "partial");
        var secondTick = await service.ProcessAsync(CancellationToken.None);

        var thirdTick = await service.ProcessAsync(CancellationToken.None);

        // Assert
        firstTick.ShouldBe(0);
        secondTick.ShouldBe(0);
        thirdTick.ShouldBe(1);

        var release = await dbContext.Releases.SingleAsync();
        release.ReleaseFolderPath.ShouldBe(folder.FullName);
        (await dbContext.ReleaseFolderObservations.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_StableButWithinStabilityWindow_DoesNotCreateRelease()
    {
        // Arrange
        stabilityMinutes = 60;
        var releaseTemplate = await AddReleaseTemplateAsync();
        var folder = Directory.CreateDirectory(Path.Combine(tempRootPath, "Bearcat.Release.1080p"));
        await File.WriteAllTextAsync(Path.Combine(folder.FullName, "video.mkv"), "done");
        await AddAutomationAsync(releaseTemplate.ReleaseTemplateId, tempRootPath, "*1080p*");

        // Act
        await service.ProcessAsync(CancellationToken.None);
        var result = await service.ProcessAsync(CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        (await dbContext.Releases.AnyAsync()).ShouldBeFalse();
        (await dbContext.ReleaseFolderObservations.SingleAsync()).FolderPath.ShouldBe(
            folder.FullName
        );
    }

    [Test]
    public async Task ProcessAsync_PendingFolderDisappears_RemovesStaleObservation()
    {
        // Arrange
        var releaseTemplate = await AddReleaseTemplateAsync();
        var folder = Directory.CreateDirectory(Path.Combine(tempRootPath, "Bearcat.Release.1080p"));
        await AddAutomationAsync(releaseTemplate.ReleaseTemplateId, tempRootPath, "*1080p*");

        // Act
        await service.ProcessAsync(CancellationToken.None);
        (await dbContext.ReleaseFolderObservations.AnyAsync()).ShouldBeTrue();

        Directory.Delete(folder.FullName, recursive: true);
        var result = await service.ProcessAsync(CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        (await dbContext.ReleaseFolderObservations.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_FolderBelowMinimumSize_DoesNotCreateRelease()
    {
        // Arrange
        minimumFolderSizeMegabytes = 1;
        var releaseTemplate = await AddReleaseTemplateAsync();
        var folder = Directory.CreateDirectory(Path.Combine(tempRootPath, "Bearcat.Release.1080p"));
        await File.WriteAllTextAsync(Path.Combine(folder.FullName, "readme.txt"), "tiny");
        await AddAutomationAsync(releaseTemplate.ReleaseTemplateId, tempRootPath, "*1080p*");

        // Act
        var result = await ProcessUntilStableAsync();

        // Assert
        result.ShouldBe(0);
        (await dbContext.Releases.AnyAsync()).ShouldBeFalse();
        (await dbContext.ReleaseFolderObservations.SingleAsync()).FolderPath.ShouldBe(
            folder.FullName
        );
    }

    [Test]
    public async Task ProcessAsync_FolderReachesMinimumSize_CreatesRelease()
    {
        // Arrange
        minimumFolderSizeMegabytes = 1;
        var releaseTemplate = await AddReleaseTemplateAsync();
        var folder = Directory.CreateDirectory(Path.Combine(tempRootPath, "Bearcat.Release.1080p"));
        await File.WriteAllBytesAsync(
            Path.Combine(folder.FullName, "video.mkv"),
            new byte[2 * 1024 * 1024]
        );
        await AddAutomationAsync(releaseTemplate.ReleaseTemplateId, tempRootPath, "*1080p*");

        // Act
        var result = await ProcessUntilStableAsync();

        // Assert
        result.ShouldBe(1);
        var release = await dbContext.Releases.SingleAsync();
        release.ReleaseFolderPath.ShouldBe(folder.FullName);
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
        bool isEnabled = true,
        string? primaryLanguageCode = null
    )
    {
        dbContext.ReleaseFolderAutomations.Add(
            new ReleaseFolderAutomation
            {
                BasePath = basePath,
                FolderNamePattern = folderNamePattern,
                PrimaryLanguageCode = primaryLanguageCode,
                ReleaseTemplateId = releaseTemplateId,
                IsEnabled = isEnabled,
            }
        );
        await dbContext.SaveChangesAsync();
    }

    private async Task<ImageHosterRegistration> AddImageHosterRegistrationAsync()
    {
        var imageHosterRegistration = new ImageHosterRegistration
        {
            Name = "ImgBB",
            ImageHosterClassName = "ImgBb",
            SerializedConfig = "{}",
            IsActive = true,
        };

        dbContext.ImageHosterRegistrations.Add(imageHosterRegistration);
        await dbContext.SaveChangesAsync();

        return imageHosterRegistration;
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

    private MediaMetadataService CreateMediaMetadataService()
    {
        var extractorMock = new Mock<IMediaMetadataExtractor>();
        extractorMock
            .Setup(extractor =>
                extractor.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((MediaProbeResult?)null);

        return new MediaMetadataService(
            new MediaMetadataRepository(dbContext),
            extractorMock.Object,
            new FileSystemService(),
            CreateTimeProvider(),
            NullLogger<MediaMetadataService>.Instance
        );
    }

    private void SetupNfoDatabase(
        string className,
        string expectedReleaseName,
        NfoReleaseInfo? releaseInfo
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

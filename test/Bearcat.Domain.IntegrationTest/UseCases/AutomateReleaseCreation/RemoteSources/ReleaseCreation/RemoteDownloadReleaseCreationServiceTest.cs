using Bearcat.Abstractions.Media;
using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.IntegrationTest.Shared;
using Bearcat.Domain.IntegrationTest.Shared.UnmanagedReleases;
using Bearcat.Domain.Shared.MediaMetadataResolution;
using Bearcat.Domain.Shared.UnmanagedReleases;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.Creation;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.ReleaseCreation;
using Bearcat.Domain.UseCases.ManageNotifications;
using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.ReleaseInfoResolution;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.FileSystem;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.AutomateReleaseCreation.RemoteSources.ReleaseCreation;

public class RemoteDownloadReleaseCreationServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string FirstEpisodeName =
        "Bodies.2023.S01E01.German.DL.EAC3.1080p.DV.HDR.NF.WEB.H265-ZeroTwo";
    private const string SecondEpisodeName =
        "Bodies.2023.S01E02.German.DL.EAC3.1080p.DV.HDR.NF.WEB.H265-ZeroTwo";
    private const string ExpectedCollectionKey =
        "bodies.2023.s01.german.dl.eac3.1080p.dv.hdr.nf.web.h265.zerotwo";
    private const string ExpectedCollectionName =
        "Bodies.2023.S01.German.DL.EAC3.1080p.DV.HDR.NF.WEB.H265-ZeroTwo";

    private static readonly DateTime StartTime = new(
        2026,
        9,
        27,
        12,
        0,
        0,
        DateTimeKind.Unspecified
    );

    private string tempRootPath = null!;

    [SetUp]
    public void Setup()
    {
        tempRootPath = Path.Combine(Path.GetTempPath(), $"bearcat-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRootPath);
    }

    [TearDown]
    public void DeleteTempFolder()
    {
        if (Directory.Exists(tempRootPath))
        {
            Directory.Delete(tempRootPath, recursive: true);
        }
    }

    [Test]
    public async Task ProcessAsync_TemplateDetectsSeriesCollection_CreatesCollectionForEpisodesOfSameSeason()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(
            ReleaseCollectionDetectionMode.SeriesEpisodePattern
        );
        var firstDownload = await AddDownloadAsync(seed.ReleaseTemplateId, FirstEpisodeName, "de");
        var secondDownload = await AddDownloadAsync(
            seed.ReleaseTemplateId,
            SecondEpisodeName,
            "de"
        );

        // Act
        var createdCount = await ProcessAsync();

        // Assert
        createdCount.ShouldBe(2);

        var releaseCollection = await QueryReleaseCollectionsWithReleasesAndSlots().SingleAsync();
        releaseCollection.Key.ShouldBe(ExpectedCollectionKey);
        releaseCollection.Name.ShouldBe(ExpectedCollectionName);
        releaseCollection.ReleaseGroupId.ShouldBe(seed.ReleaseGroupId);
        releaseCollection.ReleaseContentType.ShouldBe(ReleaseContentType.TvShowEpisode);
        releaseCollection.PrimaryLanguageCode.ShouldBe("de");
        releaseCollection.CreatedAt.ShouldBe(StartTime);
        releaseCollection
            .Releases.Select(release => release.Name)
            .OrderBy(name => name)
            .ShouldBe([FirstEpisodeName, SecondEpisodeName]);

        var slot = releaseCollection.UploadSlots.ShouldHaveSingleItem();
        slot.Key.ShouldBe("main");
        slot.Name.ShouldBe("Main hoster");
        slot.IsRequired.ShouldBeTrue();
        slot.PasswordPolicy.ShouldBe(CollectionUploadSlotPasswordPolicy.MustEqualExpectedValue);
        slot.ExpectedArchivePassword.ShouldBe("archive-secret");
        slot.UploadConfigs.Count.ShouldBe(2);
        slot.UploadConfigs.SelectMany(uploadConfig => uploadConfig.LinkCrypters)
            .ShouldAllBe(linkCrypter =>
                linkCrypter.ContainerScope == LinkCrypterContainerScope.ReleaseCollection
            );

        var imageUploadConfig = releaseCollection.ImageUploadConfigs.ShouldHaveSingleItem();
        imageUploadConfig.Name.ShouldBe("Series cover");
        imageUploadConfig.ImageHosterRegistrationId.ShouldBe(seed.ImageHosterRegistrationId);
        imageUploadConfig.ReleaseId.ShouldBeNull();

        var downloads = await CreateDbContext()
            .RemoteSourceDownloads.OrderBy(download => download.Id)
            .ToListAsync();
        downloads.Select(download => download.Id).ShouldBe([firstDownload.Id, secondDownload.Id]);
        downloads.ShouldAllBe(download =>
            download.State == RemoteSourceDownloadState.ReleaseCreated && download.ReleaseId != null
        );
    }

    [Test]
    public async Task ProcessAsync_CollectionExistsFromEarlierDownload_AddsReleaseToExistingCollection()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(
            ReleaseCollectionDetectionMode.SeriesEpisodePattern
        );
        await AddDownloadAsync(seed.ReleaseTemplateId, FirstEpisodeName, primaryLanguageCode: null);
        await ProcessAsync();
        await ChangeCollectionLinkCrypterSettingsAsync("collection-secret", enableCaptcha: false);
        await AddDownloadAsync(seed.ReleaseTemplateId, SecondEpisodeName, "de");

        // Act
        var createdCount = await ProcessAsync();

        // Assert
        createdCount.ShouldBe(1);

        var releaseCollection = await QueryReleaseCollectionsWithReleasesAndSlots().SingleAsync();
        releaseCollection.Key.ShouldBe(ExpectedCollectionKey);
        releaseCollection.PrimaryLanguageCode.ShouldBe("de");
        releaseCollection
            .Releases.Select(release => release.Name)
            .OrderBy(name => name)
            .ShouldBe([FirstEpisodeName, SecondEpisodeName]);
        releaseCollection.ImageUploadConfigs.ShouldHaveSingleItem();

        var slot = releaseCollection.UploadSlots.ShouldHaveSingleItem();
        slot.UploadConfigs.Count.ShouldBe(2);

        var secondEpisodeLinkCrypter = slot
            .UploadConfigs.Single(uploadConfig => uploadConfig.Release.Name == SecondEpisodeName)
            .LinkCrypters.ShouldHaveSingleItem();
        secondEpisodeLinkCrypter.LinkCrypterRegistrationId.ShouldBe(seed.LinkCrypterRegistrationId);
        secondEpisodeLinkCrypter.ContainerScope.ShouldBe(
            LinkCrypterContainerScope.ReleaseCollection
        );
        secondEpisodeLinkCrypter.Password.ShouldBe("collection-secret");
        secondEpisodeLinkCrypter.EnableCaptcha.ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_TemplateHasCollectionDetectionDisabled_CreatesReleaseWithoutCollection()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync(ReleaseCollectionDetectionMode.Disabled);
        await AddDownloadAsync(seed.ReleaseTemplateId, FirstEpisodeName, "de");

        // Act
        var createdCount = await ProcessAsync();

        // Assert
        createdCount.ShouldBe(1);

        var dbContext = CreateDbContext();
        var release = await dbContext
            .Releases.Include(release => release.UploadConfigs)
                .ThenInclude(uploadConfig => uploadConfig.LinkCrypters)
            .SingleAsync();
        release.ReleaseCollectionId.ShouldBeNull();
        release.UploadConfigs.ShouldHaveSingleItem().CollectionUploadSlotId.ShouldBeNull();
        release
            .UploadConfigs.Single()
            .LinkCrypters.ShouldHaveSingleItem()
            .ContainerScope.ShouldBe(LinkCrypterContainerScope.Release);
        (await dbContext.ReleaseCollections.AnyAsync()).ShouldBeFalse();
    }

    private async Task<int> ProcessAsync()
    {
        var dbContext = CreateDbContext();
        var timeProvider = new ControllableTimeProvider(StartTime);

        var service = new RemoteDownloadReleaseCreationService(
            new RemoteSourceDownloadRepository(dbContext),
            new ReleaseFromFolderCreationService(
                releaseInfoResolutionService: CreateReleaseInfoResolutionService(
                    dbContext,
                    timeProvider
                ),
                mediaMetadataService: CreateMediaMetadataService(dbContext, timeProvider),
                unmanagedReleaseArchiveInitializationService: new UnmanagedReleaseArchiveInitializationService(
                    new RealArchiverFactory()
                ),
                releaseCollectionAssigner: new ReleaseCollectionAssignmentService(
                    new ReleaseCollectionRepository(
                        dbRead: dbContext,
                        dbWrite: dbContext,
                        metadataDatabaseFactory: Mock.Of<IMediaMetadataDatabaseFactory>()
                    ),
                    timeProvider
                )
            ),
            new NotificationService(
                new NotificationRepository(dbContext),
                timeProvider,
                CreateNotificationConfigurationProvider()
            ),
            timeProvider,
            NullLogger<RemoteDownloadReleaseCreationService>.Instance
        );

        return await service.ProcessAsync(CancellationToken.None);
    }

    private IQueryable<ReleaseCollection> QueryReleaseCollectionsWithReleasesAndSlots()
    {
        return CreateDbContext()
            .ReleaseCollections.AsSplitQuery()
            .Include(collection => collection.Releases)
            .Include(collection => collection.ImageUploadConfigs)
            .Include(collection => collection.UploadSlots)
                .ThenInclude(slot => slot.UploadConfigs)
                    .ThenInclude(uploadConfig => uploadConfig.LinkCrypters)
            .Include(collection => collection.UploadSlots)
                .ThenInclude(slot => slot.UploadConfigs)
                    .ThenInclude(uploadConfig => uploadConfig.Release);
    }

    private async Task ChangeCollectionLinkCrypterSettingsAsync(string password, bool enableCaptcha)
    {
        var dbContext = CreateDbContext();
        var linkCrypter = await dbContext.UploadConfigLinkCrypters.SingleAsync();
        linkCrypter.Password = password;
        linkCrypter.EnableCaptcha = enableCaptcha;
        await dbContext.SaveChangesAsync();
    }

    private async Task<RemoteSourceDownload> AddDownloadAsync(
        int releaseTemplateId,
        string folderName,
        string? primaryLanguageCode
    )
    {
        var localFolderPath = Directory
            .CreateDirectory(Path.Combine(tempRootPath, "downloads", folderName))
            .FullName;
        var download = new RemoteSourceDownload
        {
            SourceName = "Main FTP",
            RemoteFolderPath = $"/incoming/{folderName}",
            FolderName = folderName,
            LocalFolderPath = localFolderPath,
            ReleaseTemplateId = releaseTemplateId,
            PrimaryLanguageCode = primaryLanguageCode,
            State = RemoteSourceDownloadState.ReadyForReleaseCreation,
            FileCount = 1,
            TotalBytes = 1,
            LastChangedAt = StartTime,
            DiscoveredAt = StartTime,
            StartedAt = StartTime,
            CompletedAt = StartTime,
        };

        var dbContext = CreateDbContext();
        dbContext.RemoteSourceDownloads.Add(download);
        await dbContext.SaveChangesAsync();

        return download;
    }

    private async Task<ReleaseTemplateSeed> AddReleaseTemplateAsync(
        ReleaseCollectionDetectionMode releaseCollectionDetectionMode
    )
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = "Series releases",
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
        var imageHosterRegistration = new ImageHosterRegistration
        {
            Name = "ImgBB",
            ImageHosterClassName = "ImgBb",
            SerializedConfig = "{}",
            IsActive = true,
        };
        var releaseTemplate = new ReleaseTemplate
        {
            Name = "Series template",
            ReleaseType = ReleaseType.Managed,
            ReleaseContentType = ReleaseContentType.TvShowEpisode,
            ReleaseGroup = releaseGroup,
            ReleaseCollectionDetectionMode = releaseCollectionDetectionMode,
            ArchiveConfigTemplates =
            [
                new ArchiveConfigTemplate
                {
                    Name = "RAR",
                    ArchiveFilesBasePath = Path.Combine(tempRootPath, "archives"),
                    ArchiverName = "rar",
                    ArchivePassword = "archive-secret",
                    ArchiveFileSizeMb = 1024,
                    UseReleaseNameAsArchiveName = true,
                },
            ],
            CollectionImageUploadConfigTemplates =
            [
                new CollectionImageUploadConfigTemplate
                {
                    ImageHosterRegistration = imageHosterRegistration,
                    Name = "Series cover",
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
                CollectionUploadSlotKey = " main ",
                CollectionUploadSlotName = "Main hoster",
                CollectionUploadSlotIsRequired = true,
                CollectionUploadSlotPasswordPolicy =
                    CollectionUploadSlotPasswordPolicy.MustEqualExpectedValue,
                CollectionUploadSlotExpectedArchivePassword = "archive-secret",
                LinkCrypterTemplates =
                [
                    new UploadConfigLinkCrypterTemplate
                    {
                        LinkCrypterRegistration = linkCrypterRegistration,
                        Password = "template-secret",
                    },
                ],
            },
        ];

        var dbContext = CreateDbContext();
        dbContext.ReleaseTemplates.Add(releaseTemplate);
        await dbContext.SaveChangesAsync();

        return new ReleaseTemplateSeed(
            releaseTemplate.Id,
            releaseGroup.Id,
            linkCrypterRegistration.Id,
            imageHosterRegistration.Id
        );
    }

    private static ReleaseInfoResolutionService CreateReleaseInfoResolutionService(
        BearcatDbContext dbContext,
        ControllableTimeProvider timeProvider
    )
    {
        var nfoDatabaseFactory = new Mock<INfoDatabaseFactory>(MockBehavior.Strict);
        nfoDatabaseFactory
            .Setup(factory => factory.GetByClassName())
            .Returns(new Dictionary<string, INfoDatabase>());
        var releaseInfoRepository = new ReleaseInfoRepository(
            dbContext,
            dbContext,
            NoOpSecretProtector.Instance
        );

        return new ReleaseInfoResolutionService(
            releaseInfoRepository,
            nfoDatabaseFactory.Object,
            new ReleaseNfoResolver(
                nfoDatabaseFactory.Object,
                NullLogger<ReleaseNfoResolver>.Instance
            ),
            new ReleaseInfoResolver(
                releaseInfoRepository,
                nfoDatabaseFactory.Object,
                NullLogger<ReleaseInfoResolver>.Instance
            ),
            new ReleaseMetadataResolver(
                new MediaMetadataResolver(
                    new MediaMetadataResolverRepository(dbContext, NoOpSecretProtector.Instance),
                    new Mock<IMediaMetadataDatabaseFactory>(MockBehavior.Strict).Object,
                    NullLogger<MediaMetadataResolver>.Instance
                ),
                nfoDatabaseFactory.Object,
                NullLogger<ReleaseMetadataResolver>.Instance
            ),
            CreateReleaseClassificationService(dbContext, timeProvider),
            NullLogger<ReleaseInfoResolutionService>.Instance,
            timeProvider
        );
    }

    private static MediaMetadataService CreateMediaMetadataService(
        BearcatDbContext dbContext,
        ControllableTimeProvider timeProvider
    )
    {
        var extractor = new Mock<IMediaMetadataExtractor>();
        extractor
            .Setup(mediaMetadataExtractor =>
                mediaMetadataExtractor.ExtractAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((MediaProbeResult?)null);

        return new MediaMetadataService(
            new MediaMetadataRepository(dbContext),
            extractor.Object,
            new FileSystemService(),
            CreateReleaseClassificationService(dbContext, timeProvider),
            timeProvider,
            NullLogger<MediaMetadataService>.Instance
        );
    }

    private static ReleaseClassificationService CreateReleaseClassificationService(
        BearcatDbContext dbContext,
        ControllableTimeProvider timeProvider
    )
    {
        return new ReleaseClassificationService(
            new ReleaseClassificationRepository(dbContext),
            timeProvider,
            NullLogger<ReleaseClassificationService>.Instance
        );
    }

    private sealed record ReleaseTemplateSeed(
        int ReleaseTemplateId,
        int ReleaseGroupId,
        int LinkCrypterRegistrationId,
        int ImageHosterRegistrationId
    );
}

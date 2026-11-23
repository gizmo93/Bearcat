using Bearcat.Abstractions;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.LinkCrypter.Results;
using Bearcat.Abstractions.Media;
using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.IntegrationTest.Shared.UnmanagedReleases;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.MediaMetadataResolution;
using Bearcat.Domain.Shared.UnmanagedReleases;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.Creation;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Dto;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Importing;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Reading;
using Bearcat.Domain.UseCases.ManageNotifications;
using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.ReleaseInfoResolution;
using Bearcat.Domain.UseCases.ManageReleaseTemplates;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.FileSystem;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ImportLinkCrypterContainers;

public abstract class LinkCrypterImportIntegrationTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    protected const string LinkCrypterClassName = "TestCrypter";
    protected const string FirstHosterClassName = "FirstHoster";
    protected const string SecondHosterClassName = "SecondHoster";
    protected const string ThirdHosterClassName = "ThirdHoster";
    protected const string ReleaseName = "Bearcat.Movie.2026.German.1080p.WEB.x264-GRP";
    protected const string OtherReleaseName = "Bearcat.Other.2026.German.1080p.WEB.x264-GRP";
    protected const string ThirdReleaseName = "Bearcat.Third.2026.German.1080p.WEB.x264-GRP";
    protected const string FirstArchiveFileName = "Bearcat.Movie.part1.rar";
    protected const string SecondArchiveFileName = "Bearcat.Movie.part2.rar";
    protected const string SeasonContainerName = "Bearcat.Show.S01.German.1080p.WEB.x264-GRP";
    protected const string FirstEpisodeReleaseName =
        "Bearcat.Show.S01E01.German.1080p.WEB.x264-GRP";
    protected const string SecondEpisodeReleaseName =
        "Bearcat.Show.S01E02.German.1080p.WEB.x264-GRP";
    protected const string SeasonSlotKey = "season";

    protected static readonly DateTime StartTime = new(
        2026,
        11,
        2,
        12,
        0,
        0,
        DateTimeKind.Unspecified
    );

    private readonly Dictionary<string, ReadContainerLinksResult> linksPerContainerUrl = new();
    private readonly Dictionary<string, HostedFileInfo> fileInfosPerLink = new();
    private readonly List<ListedContainer> listedContainers = [];

    private string tempRootPath = null!;
    private string workingDirectoryPath = null!;
    private string archiveFilesBasePath = null!;
    private Mock<ILinkCrypterWithContainerReading> linkCrypterMock = null!;
    private Mock<ILinkCrypterFactory> linkCrypterFactoryMock = null!;
    private Dictionary<string, Mock<IHosterWithFileInfo>> hosterMocksByClassName = null!;
    private Mock<IHosterFactory> hosterFactoryMock = null!;
    private Mock<IFileSystemService> previewFileSystemServiceMock = null!;

    protected List<ListedContainer> ListedContainers => listedContainers;

    protected string TempRootPath => tempRootPath;

    protected string WorkingDirectoryPath => workingDirectoryPath;

    protected string ArchiveFilesBasePath => archiveFilesBasePath;

    protected Dictionary<string, Mock<IHosterWithFileInfo>> HosterMocksByClassName =>
        hosterMocksByClassName;

    protected Mock<IFileSystemService> PreviewFileSystemServiceMock => previewFileSystemServiceMock;

    [SetUp]
    public void Setup()
    {
        tempRootPath = Path.Combine(Path.GetTempPath(), $"bearcat-tests-{Guid.NewGuid():N}");
        workingDirectoryPath = Directory
            .CreateDirectory(Path.Combine(tempRootPath, "working"))
            .FullName;
        archiveFilesBasePath = Directory
            .CreateDirectory(Path.Combine(tempRootPath, "restored-archives"))
            .FullName;

        linkCrypterMock = new Mock<ILinkCrypterWithContainerReading>();
        linkCrypterMock
            .Setup(crypter => crypter.DeserializeConfig(It.IsAny<string>()))
            .Returns(Mock.Of<ILinkCrypterConfig>());
        linkCrypterMock
            .Setup(crypter =>
                crypter.ListContainersAsync(
                    It.IsAny<ILinkCrypterConfig>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() => new ListContainersResult(true, listedContainers.ToList(), []));
        linkCrypterMock
            .Setup(crypter =>
                crypter.ReadContainerLinksAsync(
                    It.IsAny<ILinkCrypterConfig>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (ILinkCrypterConfig _, string containerUrl, string? _, CancellationToken _) =>
                    linksPerContainerUrl[containerUrl]
            );
        linkCrypterFactoryMock = new Mock<ILinkCrypterFactory>();
        linkCrypterFactoryMock
            .Setup(factory => factory.Get(LinkCrypterClassName))
            .Returns(linkCrypterMock.Object);

        hosterMocksByClassName = new Dictionary<string, Mock<IHosterWithFileInfo>>
        {
            [FirstHosterClassName] = CreateHosterMock(),
            [SecondHosterClassName] = CreateHosterMock(),
            [ThirdHosterClassName] = CreateHosterMock(),
        };
        var hostersByHostName = new Dictionary<string, IHoster>
        {
            ["first.test"] = hosterMocksByClassName[FirstHosterClassName].Object,
            ["second.test"] = hosterMocksByClassName[SecondHosterClassName].Object,
            ["third.test"] = hosterMocksByClassName[ThirdHosterClassName].Object,
        };
        hosterFactoryMock = new Mock<IHosterFactory>();
        hosterFactoryMock
            .Setup(factory => factory.GetHostersByName())
            .Returns(
                hosterMocksByClassName.ToDictionary(
                    entry => entry.Key,
                    entry => (IHoster)entry.Value.Object
                )
            );
        hosterFactoryMock
            .Setup(factory => factory.GetByName(It.IsAny<string>()))
            .Returns((string hosterClassName) => hosterMocksByClassName[hosterClassName].Object);
        hosterFactoryMock
            .Setup(factory => factory.FindHosterByFileLink(It.IsAny<string>()))
            .Returns(
                (string fileLink) => hostersByHostName.GetValueOrDefault(new Uri(fileLink).Host)
            );

        var fileSystemService = new FileSystemService();
        previewFileSystemServiceMock = new Mock<IFileSystemService>(MockBehavior.Strict);
        previewFileSystemServiceMock
            .Setup(service => service.DirectoryExists(It.IsAny<string>()))
            .Returns((string path) => fileSystemService.DirectoryExists(path));
        previewFileSystemServiceMock
            .Setup(service => service.GetFoldersInPath(It.IsAny<string>()))
            .Returns((string path) => fileSystemService.GetFoldersInPath(path));
        previewFileSystemServiceMock
            .Setup(service => service.GetFilesInPath(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(
                (string path, bool recursive) => fileSystemService.GetFilesInPath(path, recursive)
            );
    }

    [TearDown]
    public void DeleteTempFolder()
    {
        if (Directory.Exists(tempRootPath))
        {
            Directory.Delete(tempRootPath, recursive: true);
        }
    }

    protected async Task<int> RunWholeImportAsync(
        TemplateSeed seed,
        int? linkCrypterRegistrationId = null,
        string? primaryLanguageCode = null
    )
    {
        var linkCrypterImportId = await StartImportAsync(
            seed: seed,
            linkCrypterRegistrationId: linkCrypterRegistrationId,
            primaryLanguageCode: primaryLanguageCode
        );
        await ProcessAsync();
        await ChooseDefaultHosterRegistrationsAsync(linkCrypterImportId, seed);
        await ProcessAsync();
        await CreateLinkCrypterImportService(CreateDbContext())
            .StartImportAsync(linkCrypterImportId);
        await ProcessAsync();

        return linkCrypterImportId;
    }

    protected async Task<int> StartImportAsync(
        TemplateSeed seed,
        int? linkCrypterRegistrationId = null,
        string? primaryLanguageCode = null,
        int? seriesReleaseTemplateId = null
    )
    {
        return await CreateLinkCrypterImportService(CreateDbContext())
            .StartAsync(
                linkCrypterRegistrationId: linkCrypterRegistrationId
                    ?? seed.LinkCrypterRegistrationId,
                releaseTemplateId: seed.ReleaseTemplateId,
                seriesReleaseTemplateId: seriesReleaseTemplateId,
                archiveFilesBasePathForReleasesWithoutLocalFiles: archiveFilesBasePath,
                primaryLanguageCode: primaryLanguageCode
            );
    }

    protected async Task ChooseDefaultHosterRegistrationsAsync(
        int linkCrypterImportId,
        TemplateSeed seed
    )
    {
        await CreateLinkCrypterImportService(CreateDbContext())
            .SetHosterRegistrationsAsync(
                linkCrypterImportId,
                [
                    new LinkCrypterImportHosterRegistrationChoice(
                        FirstHosterClassName,
                        seed.FirstHosterRegistrationId
                    ),
                    new LinkCrypterImportHosterRegistrationChoice(
                        SecondHosterClassName,
                        seed.SecondHosterRegistrationId
                    ),
                ]
            );
    }

    protected async Task ProcessAsync()
    {
        var dbContext = CreateDbContext();
        var timeProvider = new ControllableTimeProvider(StartTime);
        var repository = new LinkCrypterImportRepository(dbContext);
        var releaseWriteRepository = new ReleaseWriteRepository(dbContext);
        var notificationService = new NotificationService(
            new NotificationRepository(dbContext),
            timeProvider,
            CreateNotificationConfigurationProvider()
        );

        var releaseCollectionAssigner = CreateReleaseCollectionAssigner(dbContext, timeProvider);

        var releaseImportService = CreateReleaseImportService(
            dbContext: dbContext,
            timeProvider: timeProvider,
            releaseCollectionAssigner: releaseCollectionAssigner
        );

        var service = new LinkCrypterImportProcessingService(
            repository,
            new LinkCrypterImportContainerReadingService(
                repository,
                releaseWriteRepository,
                linkCrypterFactoryMock.Object,
                hosterFactoryMock.Object,
                NoOpSecretProtector.Instance,
                notificationService,
                timeProvider,
                NullLogger<LinkCrypterImportContainerReadingService>.Instance
            ),
            CreatePreviewService(dbContext, timeProvider),
            new LinkCrypterImportContainerImportService(
                repository,
                releaseImportService,
                new CollectionContainerImportService(
                    repository,
                    releaseImportService,
                    timeProvider,
                    NullLogger<CollectionContainerImportService>.Instance
                ),
                CreateArchivePartMover(),
                releaseCollectionAssigner,
                notificationService,
                timeProvider,
                NullLogger<LinkCrypterImportContainerImportService>.Instance
            ),
            notificationService,
            timeProvider,
            NullLogger<LinkCrypterImportProcessingService>.Instance
        );

        await service.ProcessAsync(CancellationToken.None);
    }

    protected static ReleaseCollectionAssignmentService CreateReleaseCollectionAssigner(
        BearcatDbContext dbContext,
        ControllableTimeProvider timeProvider
    )
    {
        return new ReleaseCollectionAssignmentService(
            new ReleaseCollectionRepository(
                dbRead: dbContext,
                dbWrite: dbContext,
                metadataDatabaseFactory: Mock.Of<IMediaMetadataDatabaseFactory>()
            ),
            timeProvider
        );
    }

    protected static ArchivePartMover CreateArchivePartMover()
    {
        return new ArchivePartMover(new FileSystemService(), NullLogger<ArchivePartMover>.Instance);
    }

    protected static LinkCrypterImportReleaseImportService CreateReleaseImportService(
        BearcatDbContext dbContext,
        ControllableTimeProvider timeProvider,
        ReleaseCollectionAssignmentService releaseCollectionAssigner
    )
    {
        return new LinkCrypterImportReleaseImportService(
            new ReleaseWriteRepository(dbContext),
            CreateReleaseFromFolderCreationService(
                dbContext: dbContext,
                timeProvider: timeProvider,
                releaseCollectionAssigner: releaseCollectionAssigner
            ),
            new UnmanagedReleaseArchiveInitializationService(new RealArchiverFactory()),
            new FileSystemService()
        );
    }

    protected LinkCrypterImportService CreateLinkCrypterImportService(BearcatDbContext dbContext)
    {
        var timeProvider = new ControllableTimeProvider(StartTime);

        return new LinkCrypterImportService(
            new LinkCrypterImportRepository(dbContext),
            CreatePreviewService(dbContext, timeProvider),
            linkCrypterFactoryMock.Object,
            new FileSystemService(),
            timeProvider
        );
    }

    protected LinkCrypterImportPreviewService CreatePreviewService(
        BearcatDbContext dbContext,
        ControllableTimeProvider timeProvider
    )
    {
        return new LinkCrypterImportPreviewService(
            new LinkCrypterImportRepository(dbContext),
            new ReleaseWriteRepository(dbContext),
            new ReleaseFolderUsageRepository(dbContext),
            hosterFactoryMock.Object,
            NoOpSecretProtector.Instance,
            previewFileSystemServiceMock.Object,
            Options.Create(
                new WorkingDirectoriesConfig { WorkingDirectories = [workingDirectoryPath] }
            ),
            new NotificationService(
                new NotificationRepository(dbContext),
                timeProvider,
                CreateNotificationConfigurationProvider()
            ),
            timeProvider,
            NullLogger<LinkCrypterImportPreviewService>.Instance
        );
    }

    protected static ReleaseFromFolderCreationService CreateReleaseFromFolderCreationService(
        BearcatDbContext dbContext,
        ControllableTimeProvider timeProvider,
        ReleaseCollectionAssignmentService releaseCollectionAssigner
    )
    {
        var nfoDatabaseFactory = new Mock<INfoDatabaseFactory>();
        nfoDatabaseFactory
            .Setup(factory => factory.GetByClassName())
            .Returns(new Dictionary<string, INfoDatabase>());
        var releaseInfoRepository = new ReleaseInfoRepository(
            dbContext,
            dbContext,
            NoOpSecretProtector.Instance
        );
        var classificationService = new ReleaseClassificationService(
            new ReleaseClassificationRepository(dbContext),
            timeProvider,
            NullLogger<ReleaseClassificationService>.Instance
        );
        var mediaMetadataExtractor = new Mock<IMediaMetadataExtractor>();
        mediaMetadataExtractor
            .Setup(extractor =>
                extractor.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((MediaProbeResult?)null);

        return new ReleaseFromFolderCreationService(
            new ReleaseInfoResolutionService(
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
                        new MediaMetadataResolverRepository(
                            dbContext,
                            NoOpSecretProtector.Instance
                        ),
                        new Mock<IMediaMetadataDatabaseFactory>().Object,
                        NullLogger<MediaMetadataResolver>.Instance
                    ),
                    nfoDatabaseFactory.Object,
                    NullLogger<ReleaseMetadataResolver>.Instance
                ),
                classificationService,
                NullLogger<ReleaseInfoResolutionService>.Instance,
                timeProvider
            ),
            new MediaMetadataService(
                new MediaMetadataRepository(dbContext),
                mediaMetadataExtractor.Object,
                new FileSystemService(),
                classificationService,
                timeProvider,
                NullLogger<MediaMetadataService>.Instance
            ),
            new UnmanagedReleaseArchiveInitializationService(new RealArchiverFactory()),
            releaseCollectionAssigner
        );
    }

    protected async Task<int> AddArchiveStorageFolderAsync(string archiveStorageFolderPath)
    {
        var archiveStorageFolder = new ArchiveStorageFolder
        {
            Name = Path.GetFileName(archiveStorageFolderPath),
            Path = archiveStorageFolderPath,
            IsActive = true,
        };
        var dbContext = CreateDbContext();
        dbContext.ArchiveStorageFolders.Add(archiveStorageFolder);
        await dbContext.SaveChangesAsync();

        return archiveStorageFolder.Id;
    }

    protected static string CreateArchivePartFiles(string folderPath)
    {
        Directory.CreateDirectory(folderPath);
        File.WriteAllText(Path.Combine(folderPath, FirstArchiveFileName), "first");
        File.WriteAllText(Path.Combine(folderPath, SecondArchiveFileName), "second");

        return folderPath;
    }

    protected static string CreateSeasonArchivePartFiles(string folderPath)
    {
        Directory.CreateDirectory(folderPath);

        foreach (var fileName in GetSeasonArchiveFileNames())
        {
            File.WriteAllText(Path.Combine(folderPath, fileName), fileName);
        }

        return folderPath;
    }

    protected static List<string> GetSeasonArchiveFileNames()
    {
        return
        [
            $"{FirstEpisodeReleaseName}.part1.rar",
            $"{FirstEpisodeReleaseName}.part2.rar",
            $"{SecondEpisodeReleaseName}.part1.rar",
            $"{SecondEpisodeReleaseName}.part2.rar",
        ];
    }

    protected void AddSeasonContainer()
    {
        var fileNames = GetSeasonArchiveFileNames();

        AddListedContainer(
            "https://crypter.test/season",
            SeasonContainerName,
            fileNames
                .Select(
                    (fileName, index) => OnlineArchiveLink($"https://first.test/s{index}", fileName)
                )
                .ToList(),
            fileNames
                .Select(
                    (fileName, index) =>
                        OnlineArchiveLink($"https://second.test/s{index}", fileName)
                )
                .ToList()
        );
    }

    protected void AddEpisodeContainer(string containerUrl, string releaseName)
    {
        AddListedContainer(
            containerUrl,
            releaseName,
            [
                OnlineArchiveLink(
                    $"https://first.test/{releaseName}/1",
                    $"{releaseName}.part1.rar"
                ),
                OnlineArchiveLink(
                    $"https://first.test/{releaseName}/2",
                    $"{releaseName}.part2.rar"
                ),
            ]
        );
    }

    protected async Task<ReleaseCollection> LoadReleaseCollectionAsync()
    {
        return await CreateDbContext()
            .ReleaseCollections.AsSplitQuery()
            .Include(releaseCollection => releaseCollection.Releases)
            .Include(releaseCollection => releaseCollection.UploadSlots)
                .ThenInclude(slot => slot.UploadConfigs)
                    .ThenInclude(uploadConfig => uploadConfig.LinkCrypters)
            .Include(releaseCollection => releaseCollection.UploadSlots)
                .ThenInclude(slot => slot.UploadConfigs)
                    .ThenInclude(uploadConfig => uploadConfig.Uploads)
                        .ThenInclude(upload => upload.UploadedFiles)
            .SingleAsync();
    }

    protected async Task<List<LinkCrypterContainer>> LoadLinkCrypterContainersAsync()
    {
        return await CreateDbContext()
            .LinkCrypterContainers.Include(container => container.SourceUploads)
            .ToListAsync();
    }

    protected Mock<IHosterWithFileInfo> CreateHosterMock()
    {
        var hosterMock = new Mock<IHosterWithFileInfo>();
        hosterMock
            .Setup(hoster => hoster.ConvertToOwnFileLinkFormat(It.IsAny<string>()))
            .Returns((string fileLink) => fileLink);
        hosterMock
            .Setup(hoster => hoster.DeserializeHosterConfig(It.IsAny<string>()))
            .Returns(Mock.Of<IHosterConfig>());
        hosterMock
            .Setup(hoster =>
                hoster.GetFileInfosAsync(
                    It.IsAny<IHosterConfig>(),
                    It.IsAny<IReadOnlyList<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (IHosterConfig _, IReadOnlyList<string> fileLinks, CancellationToken _) =>
                    new FileInfoResult(
                        IsSuccess: true,
                        ErrorMessages: [],
                        FileInfoPerFileLink: fileLinks
                            .Where(fileInfosPerLink.ContainsKey)
                            .ToDictionary(
                                fileLink => fileLink,
                                fileLink => fileInfosPerLink[fileLink]
                            )
                    )
            );

        return hosterMock;
    }

    protected void VerifyFileInfosWereNeverRequested(string hosterClassName)
    {
        hosterMocksByClassName[hosterClassName]
            .Verify(
                hoster =>
                    hoster.GetFileInfosAsync(
                        It.IsAny<IHosterConfig>(),
                        It.IsAny<IReadOnlyList<string>>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never
            );
    }

    protected string OnlineArchiveLink(string link, string fileName)
    {
        fileInfosPerLink[link] = new HostedFileInfo(
            IsOnline: true,
            FileName: fileName,
            SizeBytes: 100
        );
        return link;
    }

    protected string OfflineLink(string link)
    {
        fileInfosPerLink[link] = new HostedFileInfo(
            IsOnline: false,
            FileName: null,
            SizeBytes: null
        );
        return link;
    }

    protected void AddListedContainer(
        string containerUrl,
        string containerName,
        params IReadOnlyList<string>[] linksPerMirror
    )
    {
        listedContainers.Add(
            new ListedContainer(
                ContainerId: containerUrl,
                ContainerUrl: containerUrl,
                ContainerName: containerName,
                ExternalReference: $"reference-{containerUrl}"
            )
        );
        linksPerContainerUrl[containerUrl] = new ReadContainerLinksResult(
            IsSuccess: true,
            LinksPerMirrorNumber: linksPerMirror
                .Index()
                .ToDictionary(entry => entry.Index + 1, entry => entry.Item),
            HasBackupLinks: false,
            ErrorMessages: []
        );
    }

    protected async Task<LinkCrypterImport> LoadImportAsync(int linkCrypterImportId)
    {
        return await CreateDbContext()
            .LinkCrypterImports.AsSplitQuery()
            .Include(import => import.Containers)
            .Include(import => import.HosterRegistrations)
            .SingleAsync(import => import.Id == linkCrypterImportId);
    }

    protected static List<(
        LinkCrypterImportMirrorDecision?,
        LinkCrypterImportUploadByBearcatReason?
    )> GetMirrorDecisions(LinkCrypterImportContainer container)
    {
        return GetReleasePreview(container)
            .Mirrors.Select(mirror => (mirror.Decision, mirror.UploadByBearcatReason))
            .ToList();
    }

    protected static LinkCrypterImportReleasePreview GetReleasePreview(
        LinkCrypterImportContainer container
    )
    {
        return LinkCrypterImportContainerPreview
            .FromJson(container.PreviewJson)
            .Releases!.ShouldHaveSingleItem();
    }

    protected async Task<Release> LoadReleaseAsync(string releaseName)
    {
        return await CreateDbContext()
            .Releases.AsSplitQuery()
            .Include(release => release.LinkCrypterContainers)
                .ThenInclude(container => container.SourceUploads)
            .Include(release => release.ArchiveConfigs)
                .ThenInclude(archiveConfig => archiveConfig.Archives)
                    .ThenInclude(archive => archive.ArchiveFiles)
            .Include(release => release.UploadConfigs)
                .ThenInclude(uploadConfig => uploadConfig.Uploads)
                    .ThenInclude(upload => upload.UploadedFiles)
                        .ThenInclude(file => file.ArchiveFile)
            .Include(release => release.UploadConfigs)
                .ThenInclude(uploadConfig => uploadConfig.LinkCrypters)
                    .ThenInclude(linkCrypter => linkCrypter.LinkCrypterContainers)
            .SingleAsync(release => release.Name == releaseName);
    }

    protected async Task AddExistingLinkCrypterContainerAsync(
        TemplateSeed seed,
        string containerUrl
    )
    {
        var dbContext = CreateDbContext();
        dbContext.LinkCrypterContainers.Add(
            new LinkCrypterContainer
            {
                Scope = LinkCrypterContainerScope.Release,
                LinkCrypterRegistrationId = seed.LinkCrypterRegistrationId,
                ContainerUrl = containerUrl,
                State = LinkCrypterContainerState.Created,
                Errors = [],
                CreatedAt = StartTime,
            }
        );
        await dbContext.SaveChangesAsync();
    }

    protected async Task<int> AddLinkCrypterRegistrationAsync()
    {
        var linkCrypterRegistration = new LinkCrypterRegistration
        {
            Name = "Second crypter",
            LinkCrypterClassName = LinkCrypterClassName,
            SerializedConfig = "{}",
            IsActive = true,
        };
        var dbContext = CreateDbContext();
        dbContext.LinkCrypterRegistrations.Add(linkCrypterRegistration);
        await dbContext.SaveChangesAsync();

        return linkCrypterRegistration.Id;
    }

    protected async Task AddReleaseAsync(TemplateSeed seed, string releaseName)
    {
        var dbContext = CreateDbContext();
        dbContext.Releases.Add(
            new Release
            {
                Name = releaseName,
                ReleaseType = ReleaseType.Unmanaged,
                ReleaseGroupId = seed.ReleaseGroupId,
                CreatedAt = StartTime,
                UploadConfigs = [],
                ArchiveConfigs = [],
            }
        );
        await dbContext.SaveChangesAsync();
    }

    protected async Task RemoveChosenHosterRegistrationAsync(
        int linkCrypterImportId,
        string hosterClassName
    )
    {
        var dbContext = CreateDbContext();
        var choice = await dbContext.LinkCrypterImportHosterRegistrations.SingleAsync(choice =>
            choice.LinkCrypterImportId == linkCrypterImportId
            && choice.HosterClassName == hosterClassName
        );
        choice.HosterRegistrationId = null;
        await dbContext.SaveChangesAsync();
    }

    protected async Task<TemplateSeed> AddReleaseTemplateAsync(
        ReleaseType releaseType,
        ReleaseCollectionDetectionMode releaseCollectionDetectionMode =
            ReleaseCollectionDetectionMode.Disabled,
        string? firstHosterCollectionUploadSlotKey = null,
        string? secondHosterCollectionUploadSlotKey = null
    )
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = "Movies",
            EnableAutomaticReuploads = true,
            NumberOfHoursUntilReupload = 24,
        };
        var firstHosterRegistration = CreateHosterRegistration(
            "First account",
            FirstHosterClassName
        );
        var secondHosterRegistration = CreateHosterRegistration(
            "Second account",
            SecondHosterClassName
        );
        var thirdHosterRegistration = CreateHosterRegistration(
            "Third account",
            ThirdHosterClassName
        );
        var linkCrypterRegistration = new LinkCrypterRegistration
        {
            Name = "Crypter",
            LinkCrypterClassName = LinkCrypterClassName,
            SerializedConfig = "{}",
            IsActive = true,
        };
        var archiveConfigTemplate =
            releaseType is ReleaseType.Managed
                ? new ArchiveConfigTemplate
                {
                    Name = "RAR",
                    ArchiveFilesBasePath = Path.Combine(tempRootPath, "archives"),
                    ArchiverName = "RarArchiver",
                    ArchiveFileSizeMb = 1024,
                }
                : UnmanagedArchiveConfigTemplates.Create();
        var releaseTemplate = new ReleaseTemplate
        {
            Name = "Movie template",
            ReleaseType = releaseType,
            ReleaseContentType = ReleaseContentType.Movie,
            ReleaseGroup = releaseGroup,
            ReleaseCollectionDetectionMode = releaseCollectionDetectionMode,
            ArchiveConfigTemplates = [archiveConfigTemplate],
        };
        releaseTemplate.UploadConfigTemplates =
        [
            new UploadConfigTemplate
            {
                ReleaseTemplate = releaseTemplate,
                UploadSource = UploadSource.ArchiveFiles,
                ArchiveConfigTemplate = archiveConfigTemplate,
                HosterRegistration = firstHosterRegistration,
                CollectionUploadSlotKey = firstHosterCollectionUploadSlotKey,
                LinkCrypterTemplates =
                [
                    new UploadConfigLinkCrypterTemplate
                    {
                        LinkCrypterRegistration = linkCrypterRegistration,
                        Password = "template-secret",
                    },
                ],
            },
            new UploadConfigTemplate
            {
                ReleaseTemplate = releaseTemplate,
                UploadSource = UploadSource.ArchiveFiles,
                ArchiveConfigTemplate = archiveConfigTemplate,
                HosterRegistration = secondHosterRegistration,
                CollectionUploadSlotKey = secondHosterCollectionUploadSlotKey,
            },
        ];

        var dbContext = CreateDbContext();
        dbContext.ReleaseTemplates.Add(releaseTemplate);
        dbContext.HosterRegistrations.Add(thirdHosterRegistration);
        await dbContext.SaveChangesAsync();

        return new TemplateSeed(
            ReleaseTemplateId: releaseTemplate.Id,
            ReleaseGroupId: releaseGroup.Id,
            LinkCrypterRegistrationId: linkCrypterRegistration.Id,
            FirstHosterRegistrationId: firstHosterRegistration.Id,
            SecondHosterRegistrationId: secondHosterRegistration.Id
        );
    }

    protected async Task<int> AddSeriesReleaseTemplateWithFirstHosterAsync(TemplateSeed seed)
    {
        var archiveConfigTemplate = UnmanagedArchiveConfigTemplates.Create();
        var releaseTemplate = new ReleaseTemplate
        {
            Name = "Series template",
            ReleaseType = ReleaseType.Unmanaged,
            ReleaseContentType = ReleaseContentType.TvShowEpisode,
            ReleaseGroupId = seed.ReleaseGroupId,
            ReleaseCollectionDetectionMode = ReleaseCollectionDetectionMode.SeriesEpisodePattern,
            ArchiveConfigTemplates = [archiveConfigTemplate],
        };
        releaseTemplate.UploadConfigTemplates =
        [
            new UploadConfigTemplate
            {
                ReleaseTemplate = releaseTemplate,
                UploadSource = UploadSource.ArchiveFiles,
                ArchiveConfigTemplate = archiveConfigTemplate,
                HosterRegistrationId = seed.FirstHosterRegistrationId,
            },
        ];

        var dbContext = CreateDbContext();
        dbContext.ReleaseTemplates.Add(releaseTemplate);
        await dbContext.SaveChangesAsync();

        return releaseTemplate.Id;
    }

    protected async Task AddThirdHosterUploadConfigTemplateAsync(TemplateSeed seed)
    {
        var dbContext = CreateDbContext();
        var releaseTemplate = await dbContext
            .ReleaseTemplates.Include(template => template.ArchiveConfigTemplates)
            .SingleAsync(template => template.Id == seed.ReleaseTemplateId);
        var thirdHosterRegistration = await dbContext.HosterRegistrations.SingleAsync(
            registration => registration.HosterClassName == ThirdHosterClassName
        );
        dbContext.UploadConfigTemplates.Add(
            new UploadConfigTemplate
            {
                ReleaseTemplate = releaseTemplate,
                UploadSource = UploadSource.ArchiveFiles,
                ArchiveConfigTemplate = releaseTemplate.ArchiveConfigTemplates.Single(),
                HosterRegistration = thirdHosterRegistration,
            }
        );
        await dbContext.SaveChangesAsync();
    }

    protected static HosterRegistration CreateHosterRegistration(
        string name,
        string hosterClassName
    )
    {
        return new HosterRegistration
        {
            Name = name,
            SerializedConfig = "{}",
            HosterClassName = hosterClassName,
            IsActive = true,
        };
    }

    protected sealed record TemplateSeed(
        int ReleaseTemplateId,
        int ReleaseGroupId,
        int LinkCrypterRegistrationId,
        int FirstHosterRegistrationId,
        int SecondHosterRegistrationId
    );
}

using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.Media;
using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.MediaMetadataResolution;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.Creation;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.Exceptions;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.FolderUsage;
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.AutomateReleaseCreation.Creation;

public class ReleaseFromFolderPathCreationServiceTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private string tempRootPath = null!;
    private ReleaseFromFolderPathCreationService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        tempRootPath = Path.Combine(Path.GetTempPath(), $"bearcat-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(tempRootPath);

        var timeProvider = CreateTimeProvider();

        service = new ReleaseFromFolderPathCreationService(
            repository: new ReleaseWriteRepository(dbContext),
            releaseFolderUsageRepository: new ReleaseFolderUsageRepository(dbContext),
            fileSystemService: new FileSystemService(),
            releaseFromFolderCreationService: CreateReleaseFromFolderCreationService(timeProvider),
            timeProvider: timeProvider,
            notificationService: new NotificationService(
                repository: new NotificationRepository(dbContext),
                timeProvider: timeProvider,
                configurationProvider: CreateNotificationConfigurationProvider()
            )
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
    public async Task CreateAsync_ManagedTemplate_SavesReleaseWithTemplateConfigsNameAndLanguage()
    {
        // Arrange
        var releaseTemplate = await AddReleaseTemplateAsync();
        var releaseFolder = Directory.CreateDirectory(
            Path.Combine(tempRootPath, "Bearcat.Release.1080p")
        );

        // Act
        var releaseId = await service.CreateAsync(
            folderPath: $"  {releaseFolder.FullName}{Path.DirectorySeparatorChar}  ",
            releaseTemplateId: releaseTemplate.ReleaseTemplateId,
            name: "Custom.Release.Name",
            primaryLanguageCode: " DE ",
            cancellationToken: CancellationToken.None
        );

        // Assert
        dbContext.ChangeTracker.Clear();
        var release = await dbContext
            .Releases.AsSplitQuery()
            .Include(release => release.ArchiveConfigs)
            .Include(release => release.UploadConfigs)
                .ThenInclude(uploadConfig => uploadConfig.LinkCrypters)
            .SingleAsync();

        release.Id.ShouldBe(releaseId);
        release.Name.ShouldBe("Custom.Release.Name");
        release.ReleaseType.ShouldBe(ReleaseType.Managed);
        release.ReleaseFolderPath.ShouldBe(releaseFolder.FullName);
        release.ReleaseGroupId.ShouldBe(releaseTemplate.ReleaseGroupId);
        release.PrimaryLanguageCode.ShouldBe("de");
        release.MediaMetadataExtractedAt.ShouldNotBeNull();

        var archiveConfig = release.ArchiveConfigs.Single();
        archiveConfig.Name.ShouldBe("RAR Forum A");
        archiveConfig.ArchiveFilesBasePath.ShouldBe(Path.Combine(tempRootPath, "archives"));
        archiveConfig.ArchiverName.ShouldBe("rar");
        archiveConfig.ArchivePassword.ShouldBe("archive-secret");
        archiveConfig.ArchiveFileSizeMb.ShouldBe(1024);
        archiveConfig.ArchiveNamePrefix.ShouldBe("Custom.Release.Name");

        var uploadConfig = release.UploadConfigs.Single();
        uploadConfig.Name.ShouldBe("Primary hoster");
        uploadConfig.ArchiveConfigId.ShouldBe(archiveConfig.Id);
        uploadConfig.HosterRegistrationId.ShouldBe(releaseTemplate.HosterRegistrationId);
        uploadConfig
            .LinkCrypters.Single()
            .LinkCrypterRegistrationId.ShouldBe(releaseTemplate.LinkCrypterRegistrationId);

        var notification = await dbContext.Notifications.SingleAsync();
        notification.ReleaseId.ShouldBe(releaseId);
        notification.Message.ShouldBe(
            "Release 'Custom.Release.Name' was created through the API from template 'Managed template'"
        );
    }

    [Test]
    public async Task CreateAsync_UnmanagedTemplateWithoutName_CreatesReleaseNamedAfterFolderWithFixedArchive()
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

        // Act
        await service.CreateAsync(
            folderPath: releaseFolder.FullName,
            releaseTemplateId: releaseTemplate.ReleaseTemplateId,
            name: null,
            primaryLanguageCode: null,
            cancellationToken: CancellationToken.None
        );

        // Assert
        dbContext.ChangeTracker.Clear();
        var release = await dbContext
            .Releases.AsSplitQuery()
            .Include(release => release.ArchiveConfigs)
                .ThenInclude(config => config.Archives)
                    .ThenInclude(archive => archive.ArchiveFiles)
            .SingleAsync();
        var archiveConfig = release.ArchiveConfigs.Single();
        var archive = archiveConfig.Archives.Single();

        release.Name.ShouldBe("Bearcat.Release.Unmanaged");
        release.ReleaseType.ShouldBe(ReleaseType.Unmanaged);
        release.ReleaseFolderPath.ShouldBeNull();
        release.PrimaryLanguageCode.ShouldBeNull();
        archiveConfig.ArchiveFilesBasePath.ShouldBe(releaseFolder.FullName);
        archive.ArchiveFolderPath.ShouldBe(releaseFolder.FullName);
        archive.ArchiveState.ShouldBe(ArchiveState.Created);
        archive.ArchiveFiles.Count.ShouldBe(2);
    }

    [Test]
    public async Task CreateAsync_FolderDoesNotExist_ThrowsReleaseFolderNotFoundException()
    {
        // Arrange
        var releaseTemplate = await AddReleaseTemplateAsync();
        var missingFolderPath = Path.Combine(tempRootPath, "Missing.Release.1080p");

        // Act
        var exception = await Should.ThrowAsync<ReleaseFolderNotFoundException>(() =>
            service.CreateAsync(
                folderPath: missingFolderPath,
                releaseTemplateId: releaseTemplate.ReleaseTemplateId,
                name: null,
                primaryLanguageCode: null,
                cancellationToken: CancellationToken.None
            )
        );

        // Assert
        exception.FolderPath.ShouldBe(missingFolderPath);
        (await dbContext.Releases.AnyAsync()).ShouldBeFalse();
        (await dbContext.Notifications.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task CreateAsync_TemplateDoesNotExist_ThrowsReleaseTemplateNotFoundException()
    {
        // Arrange
        var releaseFolder = Directory.CreateDirectory(
            Path.Combine(tempRootPath, "Bearcat.Release.1080p")
        );

        // Act
        var exception = await Should.ThrowAsync<ReleaseTemplateNotFoundException>(() =>
            service.CreateAsync(
                folderPath: releaseFolder.FullName,
                releaseTemplateId: 4711,
                name: null,
                primaryLanguageCode: null,
                cancellationToken: CancellationToken.None
            )
        );

        // Assert
        exception.ReleaseTemplateId.ShouldBe(4711);
        (await dbContext.Releases.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task CreateAsync_FolderIsReleaseFolderOfRelease_ThrowsReleaseFolderAlreadyInUseException()
    {
        // Arrange
        var releaseTemplate = await AddReleaseTemplateAsync();
        var releaseFolder = Directory.CreateDirectory(
            Path.Combine(tempRootPath, "Existing.Release.1080p")
        );
        await AddReleaseAsync(releaseTemplate.ReleaseGroupId, releaseFolder.FullName);

        // Act
        var exception = await Should.ThrowAsync<ReleaseFolderAlreadyInUseException>(() =>
            service.CreateAsync(
                folderPath: $"{releaseFolder.FullName}{Path.DirectorySeparatorChar}",
                releaseTemplateId: releaseTemplate.ReleaseTemplateId,
                name: null,
                primaryLanguageCode: null,
                cancellationToken: CancellationToken.None
            )
        );

        // Assert
        exception.UsageKind.ShouldBe(ReleaseFolderUsageKind.ReleaseFolder);
        exception.FolderPath.ShouldBe(releaseFolder.FullName);
        (await dbContext.Releases.CountAsync()).ShouldBe(1);
        (await dbContext.Notifications.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task CreateAsync_FolderIsArchiveFolderOfUnmanagedRelease_ThrowsReleaseFolderAlreadyInUseException()
    {
        // Arrange
        var releaseTemplate = await AddReleaseTemplateAsync(ReleaseType.Unmanaged);
        var releaseFolder = Directory.CreateDirectory(
            Path.Combine(tempRootPath, "Bearcat.Release.Unmanaged")
        );
        await File.WriteAllTextAsync(Path.Combine(releaseFolder.FullName, "archive.rar"), "1");
        await service.CreateAsync(
            folderPath: releaseFolder.FullName,
            releaseTemplateId: releaseTemplate.ReleaseTemplateId,
            name: null,
            primaryLanguageCode: null,
            cancellationToken: CancellationToken.None
        );

        // Act
        var exception = await Should.ThrowAsync<ReleaseFolderAlreadyInUseException>(() =>
            service.CreateAsync(
                folderPath: releaseFolder.FullName,
                releaseTemplateId: releaseTemplate.ReleaseTemplateId,
                name: null,
                primaryLanguageCode: null,
                cancellationToken: CancellationToken.None
            )
        );

        // Assert
        exception.UsageKind.ShouldBe(ReleaseFolderUsageKind.UnmanagedArchiveFolder);
        (await dbContext.Releases.CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task CreateAsync_FolderIsTargetOfRemoteDownload_ThrowsReleaseFolderAlreadyInUseException()
    {
        // Arrange
        var releaseTemplate = await AddReleaseTemplateAsync();
        var remoteDownloadFolder = Directory.CreateDirectory(
            Path.Combine(tempRootPath, "Remote.Release.1080p")
        );
        await AddRemoteSourceDownloadAsync(remoteDownloadFolder.FullName);

        // Act
        var exception = await Should.ThrowAsync<ReleaseFolderAlreadyInUseException>(() =>
            service.CreateAsync(
                folderPath: remoteDownloadFolder.FullName,
                releaseTemplateId: releaseTemplate.ReleaseTemplateId,
                name: null,
                primaryLanguageCode: null,
                cancellationToken: CancellationToken.None
            )
        );

        // Assert
        exception.UsageKind.ShouldBe(ReleaseFolderUsageKind.RemoteDownloadFolder);
        (await dbContext.Releases.AnyAsync()).ShouldBeFalse();
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

    private async Task AddRemoteSourceDownloadAsync(string localFolderPath)
    {
        dbContext.RemoteSourceDownloads.Add(
            new RemoteSourceDownload
            {
                SourceName = "Main FTP",
                RemoteFolderPath = $"/incoming/{Path.GetFileName(localFolderPath)}",
                FolderName = Path.GetFileName(localFolderPath),
                LocalFolderPath = localFolderPath,
                State = RemoteSourceDownloadState.Downloading,
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

    private ReleaseFromFolderCreationService CreateReleaseFromFolderCreationService(
        TimeProvider timeProvider
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
        var archiverFactory = new Mock<IArchiverFactory>();
        archiverFactory
            .Setup(factory => factory.GetArchivers())
            .Returns([new ArchiverDto("RAR", "RarArchiver", ".rar")]);

        return new ReleaseFromFolderCreationService(
            releaseInfoResolutionService: new ReleaseInfoResolutionService(
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
                        new Mock<IMediaMetadataDatabaseFactory>(MockBehavior.Strict).Object,
                        NullLogger<MediaMetadataResolver>.Instance
                    ),
                    nfoDatabaseFactory.Object,
                    NullLogger<ReleaseMetadataResolver>.Instance
                ),
                classificationService,
                NullLogger<ReleaseInfoResolutionService>.Instance,
                timeProvider
            ),
            mediaMetadataService: new MediaMetadataService(
                new MediaMetadataRepository(dbContext),
                mediaMetadataExtractor.Object,
                new FileSystemService(),
                classificationService,
                timeProvider,
                NullLogger<MediaMetadataService>.Instance
            ),
            archiverFactory: archiverFactory.Object,
            releaseCollectionAssigner: new ReleaseCollectionAssignmentService(
                new ReleaseCollectionRepository(
                    dbRead: dbContext,
                    dbWrite: dbContext,
                    metadataDatabaseFactory: Mock.Of<IMediaMetadataDatabaseFactory>()
                ),
                timeProvider
            )
        );
    }

    private sealed record ReleaseTemplateSeed(
        int ReleaseTemplateId,
        int ReleaseGroupId,
        int HosterRegistrationId,
        int LinkCrypterRegistrationId
    );
}

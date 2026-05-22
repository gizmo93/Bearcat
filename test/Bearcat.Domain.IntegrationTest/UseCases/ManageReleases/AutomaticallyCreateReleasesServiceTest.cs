using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.FileSystem;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleases;

public class AutomaticallyCreateReleasesServiceTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private string tempRootPath = null!;
    private AutomaticallyCreateReleasesService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        tempRootPath = Path.Combine(Path.GetTempPath(), $"bearcat-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRootPath);

        service = new AutomaticallyCreateReleasesService(
            new ReleaseFolderAutomationRepository(dbContext, dbContext),
            new FileSystemService(),
            CreateReleaseInfoResolutionService(),
            CreateTimeProvider()
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
        var releaseTemplate = await AddReleaseTemplateAsync();
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

    private async Task<ReleaseTemplateSeed> AddReleaseTemplateAsync()
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
            ReleaseType = ReleaseType.Managed,
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

    private static ReleaseInfoResolutionService CreateReleaseInfoResolutionService()
    {
        var releaseInfoRepository = new Mock<IReleaseInfoRepository>();
        releaseInfoRepository
            .Setup(repository =>
                repository.GetActiveNfoDatabaseRegistrationsAsync(It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([]);

        return new ReleaseInfoResolutionService(
            releaseInfoRepository.Object,
            Mock.Of<INfoDatabaseFactory>(),
            NullLogger<ReleaseInfoResolutionService>.Instance
        );
    }

    private sealed record ReleaseTemplateSeed(
        int ReleaseTemplateId,
        int ReleaseGroupId,
        int HosterRegistrationId,
        int LinkCrypterRegistrationId
    );
}

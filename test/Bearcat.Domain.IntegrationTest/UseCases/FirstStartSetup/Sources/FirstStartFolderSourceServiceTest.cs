using Bearcat.Abstractions;
using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.IntegrationTest.Shared.ReleaseCreation;
using Bearcat.Domain.IntegrationTest.Shared.UnmanagedReleases;
using Bearcat.Domain.IntegrationTest.UseCases.ManageReleaseTemplates;
using Bearcat.Domain.Shared.ArchiveRetention;
using Bearcat.Domain.Shared.QualityGate;
using Bearcat.Domain.Shared.QualityGate.Checks;
using Bearcat.Domain.Shared.UnmanagedReleases;
using Bearcat.Domain.UseCases.FirstStartSetup.Exceptions;
using Bearcat.Domain.UseCases.FirstStartSetup.Sources;
using Bearcat.Domain.UseCases.ManageNotifications;
using Bearcat.Domain.UseCases.ManageReleaseFolderAutomations;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageUploads.UploadCreation;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.FileSystem;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.FirstStartSetup.Sources;

public class FirstStartFolderSourceServiceTest(DatabaseProvider databaseProvider)
    : ReleaseTemplateIntegrationTest(databaseProvider)
{
    private string basePath = null!;
    private FirstStartFolderSourceService service = null!;

    [SetUp]
    public void Setup()
    {
        basePath = Path.Combine(Path.GetTempPath(), $"bearcat-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(basePath);
        service = CreateService();
    }

    [TearDown]
    public void DeleteBasePath()
    {
        if (Directory.Exists(basePath))
        {
            Directory.Delete(basePath, recursive: true);
        }
    }

    [Test]
    public async Task CreateFolderSourceAsync_UploadTestFolderAndIgnoreOthers_CreatesTestReleaseIgnoringAutomationAndInitialUpload()
    {
        // Arrange
        var seed = await AddReleaseTemplateWithChildrenAsync();
        var testFolderPath = CreateFolderInBasePath("Bearcat.Test.Release");
        CreateFolderInBasePath("Bearcat.Other.Release");

        // Act
        var result = await service.CreateFolderSourceAsync(
            new FirstStartFolderSourceInput(
                BasePath: basePath,
                FolderNamePattern: null,
                ReleaseTemplateId: seed.ReleaseTemplateId,
                ExtractArchivesBeforeReleaseCreation: false,
                ExistingFolderHandling: FirstStartExistingFolderHandling.UploadTestFolderAndIgnoreOthers,
                TestFolderPath: testFolderPath
            ),
            CancellationToken.None
        );

        // Assert
        var verificationDbContext = CreateDbContext();
        var release = await verificationDbContext.Releases.SingleAsync();
        var automation = await verificationDbContext.ReleaseFolderAutomations.SingleAsync();
        var uploadCount = await verificationDbContext.Uploads.CountAsync(upload =>
            upload.UploadConfig.ReleaseId == release.Id
        );

        result.TestReleaseId.ShouldBe(release.Id);
        result.ReleaseFolderAutomationId.ShouldBe(automation.Id);
        result.TestReleaseInitialUploadCreationResult.ShouldBe(
            new InitialUploadCreationForReleaseResult(
                CreatedUploadCount: 1,
                IsBlockedByQualityGate: false
            )
        );
        release.ReleaseFolderPath.ShouldBe(testFolderPath);
        automation.BasePath.ShouldBe(basePath);
        automation.ReleaseTemplateId.ShouldBe(seed.ReleaseTemplateId);
        automation.IsEnabled.ShouldBeTrue();
        automation.IgnoreExistingOnFirstScan.ShouldBeTrue();
        automation.HasCompletedInitialScan.ShouldBeFalse();
        uploadCount.ShouldBe(1);
    }

    [Test]
    public async Task CreateFolderSourceAsync_ArchiveOutputFolderInsideBasePath_ThrowsAndCreatesNothing()
    {
        // Arrange
        var seed = await AddReleaseTemplateWithChildrenAsync();
        var archiveOutputFolderPath = Path.Combine(basePath, "archives");
        var archiveConfigTemplate = await DbContext.ArchiveConfigTemplates.SingleAsync(template =>
            template.Id == seed.ArchiveConfigTemplateId
        );
        archiveConfigTemplate.ArchiveFilesBasePath = archiveOutputFolderPath;
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
        var testFolderPath = CreateFolderInBasePath("Bearcat.Test.Release");

        // Act
        var exception = await Should.ThrowAsync<ArchiveOutputFolderInsideSourceFolderException>(
            () =>
                service.CreateFolderSourceAsync(
                    new FirstStartFolderSourceInput(
                        BasePath: basePath,
                        FolderNamePattern: null,
                        ReleaseTemplateId: seed.ReleaseTemplateId,
                        ExtractArchivesBeforeReleaseCreation: false,
                        ExistingFolderHandling: FirstStartExistingFolderHandling.UploadTestFolderAndIgnoreOthers,
                        TestFolderPath: testFolderPath
                    ),
                    CancellationToken.None
                )
        );

        // Assert
        var verificationDbContext = CreateDbContext();

        exception.ArchiveOutputFolderPath.ShouldBe(archiveOutputFolderPath);
        (await verificationDbContext.Releases.AnyAsync()).ShouldBeFalse();
        (await verificationDbContext.ReleaseFolderAutomations.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task CreateFolderSourceAsync_ImportExisting_CreatesAutomationThatImportsExistingFoldersWithoutRelease()
    {
        // Arrange
        var seed = await AddReleaseTemplateWithChildrenAsync();
        CreateFolderInBasePath("Bearcat.Existing.Release");

        // Act
        var result = await service.CreateFolderSourceAsync(
            new FirstStartFolderSourceInput(
                BasePath: basePath,
                FolderNamePattern: "*.Release",
                ReleaseTemplateId: seed.ReleaseTemplateId,
                ExtractArchivesBeforeReleaseCreation: true,
                ExistingFolderHandling: FirstStartExistingFolderHandling.ImportExisting,
                TestFolderPath: null
            ),
            CancellationToken.None
        );

        // Assert
        var verificationDbContext = CreateDbContext();
        var automation = await verificationDbContext.ReleaseFolderAutomations.SingleAsync();

        result.ShouldBe(
            new FirstStartFolderSourceResult(
                automation.Id,
                TestReleaseId: null,
                TestReleaseInitialUploadCreationResult: null
            )
        );
        automation.FolderNamePattern.ShouldBe("*.Release");
        automation.ExtractArchivesBeforeReleaseCreation.ShouldBeTrue();
        automation.IgnoreExistingOnFirstScan.ShouldBeFalse();
        (await verificationDbContext.Releases.AnyAsync()).ShouldBeFalse();
    }

    private string CreateFolderInBasePath(string folderName)
    {
        return Directory.CreateDirectory(Path.Combine(basePath, folderName)).FullName;
    }

    private FirstStartFolderSourceService CreateService()
    {
        var timeProvider = CreateTimeProvider();
        var unmanagedReleaseArchiveInitializer = new UnmanagedReleaseArchiveInitializationService(
            new RealArchiverFactory()
        );
        var releaseService = new ReleaseService(
            new ReleaseWriteRepository(DbContext),
            timeProvider,
            unmanagedReleaseArchiveInitializer,
            new FileSystemService(),
            new ReleaseFolderUsageRepository(DbContext),
            ReleaseFromFolderCreationServiceFactory.Create(
                DbContext,
                unmanagedReleaseArchiveInitializer,
                timeProvider
            ),
            new MirrorCoverageEvaluator(Mock.Of<IHosterFactory>()),
            new LocalArchiveDeleter(Mock.Of<IFileSystemService>())
        );
        var uploadCreationService = new UploadCreationService(
            new UploadStateRepository(DbContext),
            timeProvider,
            CreateNotificationConfigurationProvider(),
            new NotificationService(
                repository: new NotificationRepository(DbContext),
                timeProvider: timeProvider,
                configurationProvider: CreateNotificationConfigurationProvider()
            ),
            Mock.Of<ILogger<UploadCreationService>>(),
            new QualityGateEvaluator(
                [
                    new FilePatternQualityCheck(),
                    new MinimumFolderSizeQualityCheck(),
                    new RequiredReleaseInfoQualityCheck(),
                    new MediaInfoQualityCheck(),
                ],
                new FileSystemService()
            )
        );

        return new FirstStartFolderSourceService(
            new ArchiveOutputFolderInsideSourceFolderValidator(CreateRepository()),
            releaseService,
            new ReleaseFolderAutomationService(
                new ReleaseFolderAutomationRepository(DbContext, DbContext)
            ),
            uploadCreationService
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

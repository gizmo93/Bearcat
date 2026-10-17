using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageArchives.FailedArchiveCreations;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.FileSystem;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageArchives.FailedArchiveCreations;

public class FailedArchiveCreationServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private string archiveFolderPath = null!;
    private FailedArchiveCreationService service = null!;

    [SetUp]
    public void Setup()
    {
        archiveFolderPath = Directory
            .CreateDirectory(
                Path.Combine(Path.GetTempPath(), $"bearcat-tests-{Guid.NewGuid():N}", "failed")
            )
            .FullName;

        service = new FailedArchiveCreationService(
            new ArchiveCreationRepository(DbContext),
            Mock.Of<ILogger<FailedArchiveCreationService>>(),
            new FileSystemService(),
            Mock.Of<INotificationService>()
        );
    }

    [TearDown]
    public void DeleteTempFolder()
    {
        var tempRootPath = Path.GetDirectoryName(archiveFolderPath)!;

        if (Directory.Exists(tempRootPath))
        {
            Directory.Delete(tempRootPath, recursive: true);
        }
    }

    [Test]
    public async Task DeleteCreationFailedArchiveAsync_ArchiveWithLeftoverFilesAndWaitingUpload_DeletesArchiveAndFolderAndFailsUpload()
    {
        // Arrange
        await File.WriteAllTextAsync(
            Path.Combine(archiveFolderPath, "release.part1.rar"),
            "partial"
        );
        var upload = new Upload
        {
            UploadConfig = new UploadConfig
            {
                Release = new Release
                {
                    Name = "Bearcat.Release.001",
                    ReleaseType = ReleaseType.Managed,
                    ReleaseFolderPath = "/tmp/release",
                    ReleaseGroup = new ReleaseGroup
                    {
                        Name = "Managed releases",
                        EnableAutomaticReuploads = false,
                        NumberOfHoursUntilReupload = 24,
                    },
                },
                HosterRegistration = new HosterRegistration
                {
                    Name = "TestHoster",
                    SerializedConfig = "{}",
                    HosterClassName = "TestHoster",
                    IsActive = true,
                },
                Name = "Default upload",
                UploadSource = UploadSource.ArchiveFiles,
            },
            CreatedAt = DateTime.UtcNow,
            UploadState = UploadState.WaitingForArchive,
            OnlineState = OnlineState.Unknown,
            ErrorMessages = [],
        };
        var archive = new Archive
        {
            ArchiveConfig = new ArchiveConfig
            {
                Release = upload.UploadConfig.Release,
                Name = "Main archive",
                ArchiveFilesBasePath = Path.GetDirectoryName(archiveFolderPath)!,
                ArchiverName = "zip",
                ArchiveFileSizeMb = 512,
            },
            ArchiveFolderPath = archiveFolderPath,
            ArchiveState = ArchiveState.CreationFailed,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow,
            ArchiveFiles = [],
            Uploads = [upload],
            ErrorMessages = ["Disk full"],
        };
        upload.UploadConfig.ArchiveConfig = archive.ArchiveConfig;
        DbContext.Archives.Add(archive);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        await service.DeleteCreationFailedArchiveAsync(archive.Id);

        // Assert
        DbContext.ChangeTracker.Clear();
        var failedUpload = await DbContext.Uploads.SingleAsync();

        (await DbContext.Archives.AnyAsync()).ShouldBeFalse();
        Directory.Exists(archiveFolderPath).ShouldBeFalse();
        failedUpload.UploadState.ShouldBe(UploadState.Failed);
        failedUpload.ArchiveId.ShouldBeNull();
        failedUpload.ErrorMessages.ShouldBe(["Archive creation failed: Disk full"]);
    }
}

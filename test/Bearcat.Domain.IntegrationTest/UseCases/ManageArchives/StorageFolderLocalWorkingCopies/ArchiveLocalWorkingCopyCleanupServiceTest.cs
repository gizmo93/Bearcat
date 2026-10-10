using Bearcat.Abstractions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageArchives.StorageFolderLocalWorkingCopies;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.FileSystem;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageArchives.StorageFolderLocalWorkingCopies;

public class ArchiveLocalWorkingCopyCleanupServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string ArchiveFolderName = "archive-folder";
    private const string FirstFileName = "release.part1.rar";
    private const string SecondFileName = "release.part2.rar";
    private const string FirstFileHash = "11111111111111111111111111111111";
    private const string StorageSecondFileHash = "22222222222222222222222222222222";
    private const string WorkingCopySecondFileHash = "33333333333333333333333333333333";

    private string tempRootPath = null!;
    private string archiveFilesBasePath = null!;
    private string storageFolderPath = null!;

    [SetUp]
    public void Setup()
    {
        tempRootPath = Path.Combine(Path.GetTempPath(), $"bearcat-tests-{Guid.NewGuid():N}");
        archiveFilesBasePath = Directory
            .CreateDirectory(Path.Combine(tempRootPath, "archives"))
            .FullName;
        storageFolderPath = Directory
            .CreateDirectory(Path.Combine(tempRootPath, "storage"))
            .FullName;
    }

    [TearDown]
    public void DeleteTempRootPath()
    {
        if (Directory.Exists(tempRootPath))
        {
            Directory.Delete(tempRootPath, recursive: true);
        }
    }

    [Test]
    public async Task DeleteLocalWorkingCopiesAsync_ArchiveWithLocalWorkingCopy_PointsFilesBackToStorageFolderAndDeletesLocalCopy()
    {
        // Arrange
        var scenario = await AddScenarioAsync(UploadState.Completed);
        var storageBytes = await File.ReadAllBytesAsync(scenario.StorageSecondFilePath);

        // Act
        await CreateService(new FileSystemService())
            .DeleteLocalWorkingCopiesAsync(CancellationToken.None);

        // Assert
        var archiveFiles = await LoadArchiveFilesAsync(scenario.ArchiveId);
        archiveFiles
            .Select(file => (file.FullFileName, file.Md5Hash, file.Md5HashInStorageFolder))
            .ShouldBe([
                (scenario.StorageFirstFilePath, FirstFileHash, FirstFileHash),
                (scenario.StorageSecondFilePath, StorageSecondFileHash, StorageSecondFileHash),
            ]);
        File.Exists(scenario.WorkingCopyFilePath).ShouldBeFalse();
        Directory.Exists(scenario.WorkingCopyFolderPath).ShouldBeFalse();
        Directory.Exists(archiveFilesBasePath).ShouldBeTrue();
        (await File.ReadAllBytesAsync(scenario.StorageSecondFilePath)).ShouldBe(storageBytes);
    }

    [TestCase(UploadState.WaitingForArchive)]
    [TestCase(UploadState.Pending)]
    [TestCase(UploadState.Uploading)]
    public async Task DeleteLocalWorkingCopiesAsync_ArchiveConfigHasActiveUpload_KeepsLocalWorkingCopy(
        UploadState uploadState
    )
    {
        // Arrange
        var scenario = await AddScenarioAsync(uploadState);

        // Act
        await CreateService(new FileSystemService())
            .DeleteLocalWorkingCopiesAsync(CancellationToken.None);

        // Assert
        var secondArchiveFile = (await LoadArchiveFilesAsync(scenario.ArchiveId))[1];
        secondArchiveFile.FullFileName.ShouldBe(scenario.WorkingCopyFilePath);
        secondArchiveFile.Md5Hash.ShouldBe(WorkingCopySecondFileHash);
        File.Exists(scenario.WorkingCopyFilePath).ShouldBeTrue();
    }

    [Test]
    public async Task DeleteLocalWorkingCopiesAsync_ArchiveConfigHasOnlyFailedUpload_DeletesLocalWorkingCopy()
    {
        // Arrange
        var scenario = await AddScenarioAsync(UploadState.Failed);

        // Act
        await CreateService(new FileSystemService())
            .DeleteLocalWorkingCopiesAsync(CancellationToken.None);

        // Assert
        var secondArchiveFile = (await LoadArchiveFilesAsync(scenario.ArchiveId))[1];
        secondArchiveFile.FullFileName.ShouldBe(scenario.StorageSecondFilePath);
        secondArchiveFile.Md5Hash.ShouldBe(StorageSecondFileHash);
        File.Exists(scenario.WorkingCopyFilePath).ShouldBeFalse();
    }

    [Test]
    public async Task DeleteLocalWorkingCopiesAsync_LocalWorkingCopyFolderContainsOtherFiles_KeepsFolderAndOtherFiles()
    {
        // Arrange
        var scenario = await AddScenarioAsync(UploadState.Completed);
        var otherFilePath = Path.Join(scenario.WorkingCopyFolderPath, "notes.txt");
        await File.WriteAllTextAsync(otherFilePath, "other");

        // Act
        await CreateService(new FileSystemService())
            .DeleteLocalWorkingCopiesAsync(CancellationToken.None);

        // Assert
        File.Exists(scenario.WorkingCopyFilePath).ShouldBeFalse();
        File.Exists(otherFilePath).ShouldBeTrue();
    }

    [Test]
    public async Task DeleteLocalWorkingCopiesAsync_DeletingLocalFileFails_KeepsArchivePointedToStorageFolder()
    {
        // Arrange
        var scenario = await AddScenarioAsync(UploadState.Completed);
        var fileSystemServiceMock = new Mock<IFileSystemService>(MockBehavior.Strict);
        fileSystemServiceMock
            .Setup(f => f.DeleteFileIfExists(It.IsAny<string>()))
            .Throws(new IOException("Delete failed"));

        // Act
        await CreateService(fileSystemServiceMock.Object)
            .DeleteLocalWorkingCopiesAsync(CancellationToken.None);

        // Assert
        var secondArchiveFile = (await LoadArchiveFilesAsync(scenario.ArchiveId))[1];
        secondArchiveFile.FullFileName.ShouldBe(scenario.StorageSecondFilePath);
        secondArchiveFile.Md5Hash.ShouldBe(StorageSecondFileHash);
        File.Exists(scenario.WorkingCopyFilePath).ShouldBeTrue();
    }

    [Test]
    public async Task DeleteLocalWorkingCopiesAsync_ArchiveWithoutLocalWorkingCopy_LeavesArchiveUnchanged()
    {
        // Arrange
        var scenario = await AddScenarioAsync(
            UploadState.Completed,
            secondFileIsInLocalWorkingCopy: false
        );
        var fileSystemServiceMock = new Mock<IFileSystemService>(MockBehavior.Strict);

        // Act
        await CreateService(fileSystemServiceMock.Object)
            .DeleteLocalWorkingCopiesAsync(CancellationToken.None);

        // Assert
        var archiveFiles = await LoadArchiveFilesAsync(scenario.ArchiveId);
        archiveFiles
            .Select(file => (file.FullFileName, file.Md5Hash))
            .ShouldBe([
                (scenario.StorageFirstFilePath, FirstFileHash),
                (scenario.StorageSecondFilePath, StorageSecondFileHash),
            ]);
    }

    private ArchiveLocalWorkingCopyCleanupService CreateService(
        IFileSystemService fileSystemService
    )
    {
        return new ArchiveLocalWorkingCopyCleanupService(
            new ArchiveCleanupRepository(DbContext),
            fileSystemService,
            Mock.Of<ILogger<ArchiveLocalWorkingCopyCleanupService>>()
        );
    }

    private async Task<List<ArchiveFile>> LoadArchiveFilesAsync(int archiveId)
    {
        await using var readDbContext = Database.CreateDbContext();

        return await readDbContext
            .ArchiveFiles.Where(file => file.ArchiveId == archiveId)
            .OrderBy(file => file.Id)
            .ToListAsync();
    }

    private async Task<WorkingCopyScenario> AddScenarioAsync(
        UploadState uploadState,
        bool secondFileIsInLocalWorkingCopy = true
    )
    {
        var storageArchiveFolderPath = Directory
            .CreateDirectory(Path.Join(storageFolderPath, ArchiveFolderName))
            .FullName;
        var storageFirstFilePath = Path.Join(storageArchiveFolderPath, FirstFileName);
        var storageSecondFilePath = Path.Join(storageArchiveFolderPath, SecondFileName);
        await File.WriteAllTextAsync(storageFirstFilePath, "first-volume");
        await File.WriteAllTextAsync(storageSecondFilePath, "second-volume");

        var workingCopyFolderPath = Path.Join(archiveFilesBasePath, ArchiveFolderName);
        var workingCopyFilePath = Path.Join(workingCopyFolderPath, SecondFileName);

        if (secondFileIsInLocalWorkingCopy)
        {
            Directory.CreateDirectory(workingCopyFolderPath);
            await File.WriteAllTextAsync(workingCopyFilePath, "second-volume-changed");
        }

        var release = new Release
        {
            Name = "Bearcat.Release.001",
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = Path.Join(tempRootPath, "release"),
            ReleaseGroup = new ReleaseGroup
            {
                Name = "Managed releases",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
        };
        var archiveConfig = new ArchiveConfig
        {
            Release = release,
            Name = "Main archive",
            ArchiveFilesBasePath = archiveFilesBasePath,
            ArchiverName = "rar",
            ArchiveNamePrefix = "release",
            ArchiveFileSizeMb = 512,
        };
        var uploadConfig = new UploadConfig
        {
            Release = release,
            ArchiveConfig = archiveConfig,
            HosterRegistration = new HosterRegistration
            {
                Name = "TestHoster",
                SerializedConfig = "{}",
                HosterClassName = "TestHoster",
                IsActive = true,
            },
            Name = "Default upload",
        };
        var archive = new Archive
        {
            ArchiveConfig = archiveConfig,
            ArchiveFolderPath = storageArchiveFolderPath,
            ArchiveStorageFolder = new ArchiveStorageFolder
            {
                Name = "NAS",
                Path = storageFolderPath,
                IsActive = true,
                UseLocalWorkingCopyForReuploads = true,
            },
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            ArchiveFiles =
            [
                new ArchiveFile
                {
                    FullFileName = storageFirstFilePath,
                    Md5Hash = FirstFileHash,
                    Md5HashInStorageFolder = FirstFileHash,
                },
                new ArchiveFile
                {
                    FullFileName = secondFileIsInLocalWorkingCopy
                        ? workingCopyFilePath
                        : storageSecondFilePath,
                    Md5Hash = secondFileIsInLocalWorkingCopy
                        ? WorkingCopySecondFileHash
                        : StorageSecondFileHash,
                    Md5HashInStorageFolder = StorageSecondFileHash,
                },
            ],
            Uploads = [],
            ErrorMessages = [],
        };
        var upload = new Upload
        {
            UploadConfig = uploadConfig,
            Archive = archive,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UploadState = uploadState,
            OnlineState = OnlineState.Unknown,
            ErrorMessages = [],
        };

        DbContext.AddRange(archive, upload);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return new WorkingCopyScenario(
            ArchiveId: archive.Id,
            StorageFirstFilePath: storageFirstFilePath,
            StorageSecondFilePath: storageSecondFilePath,
            WorkingCopyFolderPath: workingCopyFolderPath,
            WorkingCopyFilePath: workingCopyFilePath
        );
    }

    private sealed record WorkingCopyScenario(
        int ArchiveId,
        string StorageFirstFilePath,
        string StorageSecondFilePath,
        string WorkingCopyFolderPath,
        string WorkingCopyFilePath
    );
}

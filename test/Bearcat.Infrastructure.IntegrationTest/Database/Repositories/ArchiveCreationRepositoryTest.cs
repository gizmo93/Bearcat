using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.IntegrationTest.Database.MaterializedValues;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class ArchiveCreationRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private ArchiveCreationRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        repository = new ArchiveCreationRepository(DbContext);
    }

    [Test]
    public async Task GetKnownArchiveFileHashesAsync_HashOnlyStoredOnUploadedFile_ReturnsArchiveFileAndUploadedFileHashes()
    {
        // Arrange
        var uploadConfig = await AddUploadConfigAsync("Main archive");

        await AddArchiveAsync(
            uploadConfig,
            archiveFileHash: "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            uploadedFileHashes: ["BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB"]
        );

        // Act
        var result = await repository.GetKnownArchiveFileHashesAsync(
            uploadConfig.ArchiveConfigId!.Value,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(
            ["AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB"],
            ignoreOrder: true
        );
    }

    [Test]
    public async Task GetKnownArchiveFileHashesAsync_SameHashOnArchiveFileAndUploadedFiles_ReturnsHashOnce()
    {
        // Arrange
        var uploadConfig = await AddUploadConfigAsync("Main archive");

        await AddArchiveAsync(
            uploadConfig,
            archiveFileHash: "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            uploadedFileHashes:
            [
                "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
                "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            ]
        );

        // Act
        var result = await repository.GetKnownArchiveFileHashesAsync(
            uploadConfig.ArchiveConfigId!.Value,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(["AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"]);
    }

    [Test]
    public async Task GetKnownArchiveFileHashesAsync_FilesWithoutHash_IgnoresMissingHashes()
    {
        // Arrange
        var uploadConfig = await AddUploadConfigAsync("Main archive");

        await AddArchiveAsync(
            uploadConfig,
            archiveFileHash: null,
            uploadedFileHashes: [null, "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB"]
        );

        // Act
        var result = await repository.GetKnownArchiveFileHashesAsync(
            uploadConfig.ArchiveConfigId!.Value,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(["BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB"]);
    }

    [Test]
    public async Task GetKnownArchiveFileHashesAsync_OtherArchiveConfigHasHashes_ReturnsOnlyHashesOfRequestedArchiveConfig()
    {
        // Arrange
        var uploadConfig = await AddUploadConfigAsync("Main archive");
        var otherUploadConfig = await AddUploadConfigAsync("Other archive");

        await AddArchiveAsync(
            uploadConfig,
            archiveFileHash: "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            uploadedFileHashes: ["BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB"]
        );

        await AddArchiveAsync(
            otherUploadConfig,
            archiveFileHash: "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC",
            uploadedFileHashes: ["DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD"]
        );

        // Act
        var result = await repository.GetKnownArchiveFileHashesAsync(
            uploadConfig.ArchiveConfigId!.Value,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(
            ["AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB"],
            ignoreOrder: true
        );
    }

    [Test]
    public async Task GetPossibleAssignableArchiveAsync_LatestCreatedArchiveInStorageFolder_ReturnsArchiveWithStorageFolderAndRelease()
    {
        // Arrange
        var uploadConfig = await AddUploadConfigAsync("Main archive");
        var storageFolder = new ArchiveStorageFolder
        {
            Name = "NAS",
            Path = "/mnt/nas",
            IsActive = true,
            UseLocalWorkingCopyForReuploads = true,
        };
        DbContext.Archives.Add(
            CreateArchive(
                uploadConfig.ArchiveConfigId!.Value,
                ArchiveState.Created,
                storageFolder: null
            )
        );
        DbContext.Archives.Add(
            CreateArchive(uploadConfig.ArchiveConfigId!.Value, ArchiveState.Created, storageFolder)
        );
        DbContext.Archives.Add(
            CreateArchive(
                uploadConfig.ArchiveConfigId!.Value,
                ArchiveState.Deleted,
                storageFolder: null
            )
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetPossibleAssignableArchiveAsync(
            uploadConfig.ArchiveConfigId!.Value,
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.ArchiveStorageFolderId.ShouldBe(storageFolder.Id);
        result.ArchiveStorageFolder.ShouldNotBeNull();
        result.ArchiveStorageFolder.Name.ShouldBe("NAS");
        result.ArchiveStorageFolder.UseLocalWorkingCopyForReuploads.ShouldBeTrue();
        result.ArchiveConfig.Release.Name.ShouldBe("Bearcat.Release.Main.archive-GRP");
        result.ArchiveFiles.Count.ShouldBe(1);
    }

    [Test]
    public async Task GetPossibleAssignableArchiveAsync_LatestCreatedArchiveIsLocal_ReturnsArchiveWithoutStorageFolder()
    {
        // Arrange
        var uploadConfig = await AddUploadConfigAsync("Main archive");
        DbContext.Archives.Add(
            CreateArchive(
                uploadConfig.ArchiveConfigId!.Value,
                ArchiveState.Created,
                storageFolder: null
            )
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetPossibleAssignableArchiveAsync(
            uploadConfig.ArchiveConfigId!.Value,
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.ArchiveStorageFolderId.ShouldBeNull();
        result.ArchiveStorageFolder.ShouldBeNull();
    }

    [Test]
    public async Task HasCompletedUploadForHosterAsync_CompletedUploadOnHosterRegistrationOfOtherHosterClassThanItsUploadConfig_UsesHosterClassOfUpload()
    {
        // Arrange
        var uploadConfig = await AddUploadConfigAsync("Main archive");
        var hosterRegistrationOfUpload = await AddHosterRegistrationAsync("OtherHoster");
        await AddUploadAsync(uploadConfig, hosterRegistrationOfUpload, UploadState.Completed);

        // Act
        var resultForHosterClassOfUpload = await repository.HasCompletedUploadForHosterAsync(
            uploadConfig.ArchiveConfigId!.Value,
            "OtherHoster",
            CancellationToken.None
        );
        var resultForHosterClassOfUploadConfig = await repository.HasCompletedUploadForHosterAsync(
            uploadConfig.ArchiveConfigId!.Value,
            "TestHoster",
            CancellationToken.None
        );

        // Assert
        resultForHosterClassOfUpload.ShouldBeTrue();
        resultForHosterClassOfUploadConfig.ShouldBeFalse();
    }

    [Test]
    public async Task GetUploadsWithoutArchiveAsync_WaitingUploadOnOtherHosterRegistrationThanItsUploadConfig_LoadsHosterRegistrationOfUpload()
    {
        // Arrange
        var uploadConfig = await AddUploadConfigAsync("Main archive");
        var hosterRegistrationOfUpload = await AddHosterRegistrationAsync("OtherHoster");
        var upload = await AddUploadAsync(
            uploadConfig,
            hosterRegistrationOfUpload,
            UploadState.WaitingForArchive
        );

        // Act
        var result = await repository.GetUploadsWithoutArchiveAsync(CancellationToken.None);

        // Assert
        var waitingUpload = result.ShouldHaveSingleItem();
        waitingUpload.Id.ShouldBe(upload.Id);
        waitingUpload.HosterRegistration!.HosterClassName.ShouldBe("OtherHoster");
    }

    [Test]
    public async Task GetUploadsWithoutArchiveAsync_UnassignedUploadOfHosterAccountPool_LoadsHosterAccountPool()
    {
        // Arrange
        var testData = new MaterializedValuesTestData(DbContext);
        var uploadConfig = testData.AddHosterAccountPoolUploadConfig(
            testData.AddRelease("Release"),
            testData.AddHosterAccountPool("Pool", testData.AddHosterRegistration("Member"))
        );
        testData.AddUnassignedUpload(uploadConfig, UploadState.WaitingForArchive);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetUploadsWithoutArchiveAsync(CancellationToken.None);

        // Assert
        var upload = result.ShouldHaveSingleItem();
        upload.HosterRegistration.ShouldBeNull();
        upload.GetHosterClassName().ShouldBe("PoolHoster");
    }

    [Test]
    public async Task GetCreationFailedArchivesWithWaitingUploadsAsync_MixedArchivesAndUploads_ReturnsOnlyCreationFailedArchivesWithTheirWaitingUploads()
    {
        // Arrange
        var uploadConfig = await AddUploadConfigAsync("Main archive");
        var creationFailedArchiveWithWaitingUpload = CreateArchive(
            uploadConfig.ArchiveConfigId,
            ArchiveState.CreationFailed,
            storageFolder: null
        );
        var waitingUpload = CreateUploadOfArchive(
            uploadConfig,
            creationFailedArchiveWithWaitingUpload,
            UploadState.WaitingForArchive
        );
        CreateUploadOfArchive(
            uploadConfig,
            creationFailedArchiveWithWaitingUpload,
            UploadState.Failed
        );
        var creationFailedArchiveWithoutWaitingUpload = CreateArchive(
            uploadConfig.ArchiveConfigId,
            ArchiveState.CreationFailed,
            storageFolder: null
        );
        CreateUploadOfArchive(
            uploadConfig,
            creationFailedArchiveWithoutWaitingUpload,
            UploadState.Failed
        );
        var createdArchive = CreateArchive(
            uploadConfig.ArchiveConfigId,
            ArchiveState.Created,
            storageFolder: null
        );
        CreateUploadOfArchive(uploadConfig, createdArchive, UploadState.WaitingForArchive);
        DbContext.Archives.AddRange(
            creationFailedArchiveWithWaitingUpload,
            creationFailedArchiveWithoutWaitingUpload,
            createdArchive
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetCreationFailedArchivesWithWaitingUploadsAsync(
            CancellationToken.None
        );

        // Assert
        var archive = result.ShouldHaveSingleItem();
        archive.Id.ShouldBe(creationFailedArchiveWithWaitingUpload.Id);
        archive.Uploads.ShouldHaveSingleItem().Id.ShouldBe(waitingUpload.Id);
    }

    private static Upload CreateUploadOfArchive(
        UploadConfig uploadConfig,
        Archive archive,
        UploadState uploadState
    )
    {
        var upload = new Upload
        {
            UploadConfigId = uploadConfig.Id,
            HosterRegistrationId = uploadConfig.HosterRegistrationId,
            CreatedAt = DateTime.UtcNow,
            UploadState = uploadState,
            OnlineState = OnlineState.Unknown,
            ErrorMessages = [],
            UploadedFiles = [],
        };
        archive.Uploads.Add(upload);

        return upload;
    }

    private async Task<HosterRegistration> AddHosterRegistrationAsync(string hosterClassName)
    {
        var hosterRegistration = new HosterRegistration
        {
            Name = $"Hoster {hosterClassName}",
            SerializedConfig = "{}",
            HosterClassName = hosterClassName,
            IsActive = true,
        };

        DbContext.HosterRegistrations.Add(hosterRegistration);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return hosterRegistration;
    }

    private async Task<Upload> AddUploadAsync(
        UploadConfig uploadConfig,
        HosterRegistration hosterRegistration,
        UploadState uploadState
    )
    {
        var upload = new Upload
        {
            UploadConfigId = uploadConfig.Id,
            HosterRegistrationId = hosterRegistration.Id,
            CreatedAt = DateTime.UtcNow,
            UploadState = uploadState,
            OnlineState = OnlineState.Unknown,
            ErrorMessages = [],
            UploadedFiles = [],
        };

        DbContext.Uploads.Add(upload);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return upload;
    }

    private static Archive CreateArchive(
        int archiveConfigId,
        ArchiveState archiveState,
        ArchiveStorageFolder? storageFolder
    )
    {
        return new Archive
        {
            ArchiveConfigId = archiveConfigId,
            ArchiveFolderPath = "/tmp/archives",
            ArchiveStorageFolder = storageFolder,
            ArchiveState = archiveState,
            ArchiveFileSizeMb = 100,
            CreatedAt = DateTime.UtcNow,
            ArchiveFiles = [new ArchiveFile { FullFileName = "/tmp/archives/release.part1.rar" }],
            Uploads = [],
            ErrorMessages = [],
        };
    }

    private async Task<UploadConfig> AddUploadConfigAsync(string name)
    {
        var release = new Release
        {
            Name = $"Bearcat.Release.{name.Replace(' ', '.')}-GRP",
            CreatedAt = DateTime.UtcNow,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/tmp/release",
            ReleaseGroup = new ReleaseGroup
            {
                Name = $"Release group {name}",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
        };
        var archiveConfig = new ArchiveConfig
        {
            Release = release,
            Name = name,
            ArchiveFilesBasePath = "/tmp/archives",
            ArchiverName = "RarArchiver",
            ArchiveFileSizeMb = 100,
        };
        var uploadConfig = new UploadConfig
        {
            UploadSource = UploadSource.ArchiveFiles,
            Release = release,
            ArchiveConfig = archiveConfig,
            HosterRegistration = new HosterRegistration
            {
                Name = $"Hoster {name}",
                SerializedConfig = "{}",
                HosterClassName = "TestHoster",
                IsActive = true,
            },
            Name = "Default upload",
        };

        DbContext.UploadConfigs.Add(uploadConfig);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return uploadConfig;
    }

    private async Task AddArchiveAsync(
        UploadConfig uploadConfig,
        string? archiveFileHash,
        IReadOnlyList<string?> uploadedFileHashes
    )
    {
        var archiveFile = new ArchiveFile
        {
            FullFileName = "/tmp/archives/release.part1.rar",
            Md5Hash = archiveFileHash,
            UploadedFiles = [],
        };
        var archive = new Archive
        {
            ArchiveConfigId = uploadConfig.ArchiveConfigId!.Value,
            ArchiveFolderPath = "/tmp/archives",
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 100,
            CreatedAt = DateTime.UtcNow,
            ArchiveFiles = [archiveFile],
            Uploads = [],
            ErrorMessages = [],
        };
        foreach (var uploadedFileHash in uploadedFileHashes)
        {
            var upload = new Upload
            {
                UploadConfigId = uploadConfig.Id,
                HosterRegistrationId = uploadConfig.HosterRegistrationId,
                Archive = archive,
                CreatedAt = DateTime.UtcNow,
                UploadState = UploadState.Completed,
                OnlineState = OnlineState.Online,
                ErrorMessages = [],
                UploadedFiles = [],
            };
            upload.UploadedFiles.Add(
                new UploadedFile
                {
                    Upload = upload,
                    ArchiveFile = archiveFile,
                    HosterFileLink = "https://hoster.example/file",
                    Md5Hash = uploadedFileHash,
                    OnlineState = OnlineState.Online,
                    CreatedAt = DateTime.UtcNow,
                }
            );
            archive.Uploads.Add(upload);
        }

        DbContext.Archives.Add(archive);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
    }
}

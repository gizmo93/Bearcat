using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
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
            uploadConfig.ArchiveConfigId,
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
            uploadConfig.ArchiveConfigId,
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
            uploadConfig.ArchiveConfigId,
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
            uploadConfig.ArchiveConfigId,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(
            ["AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB"],
            ignoreOrder: true
        );
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
            ArchiveConfigId = uploadConfig.ArchiveConfigId,
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

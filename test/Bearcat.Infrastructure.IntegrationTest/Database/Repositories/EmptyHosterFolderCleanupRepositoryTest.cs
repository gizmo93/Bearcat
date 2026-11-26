using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class EmptyHosterFolderCleanupRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private static readonly DateTime LocalNow = new(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);

    private EmptyHosterFolderCleanupRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        repository = new EmptyHosterFolderCleanupRepository(ReadDbContext, DbContext);
    }

    [Test]
    public async Task GetFolderIdsWithoutOnlineFilesAsync_OnlineFileOfCurrentCompletedUpload_ExcludesFolder()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync("Hoster");
        await AddUploadAsync(
            uploadConfig: await AddUploadConfigAsync(registration),
            uploadState: UploadState.Completed,
            hosterFolderId: "folder",
            files: [("folder", OnlineState.Offline), ("folder", OnlineState.Online)]
        );

        // Act
        var result = await repository.GetFolderIdsWithoutOnlineFilesAsync(
            registration.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetFolderIdsWithoutOnlineFilesAsync_AllFilesOffline_ReturnsFolder()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync("Hoster");
        await AddUploadAsync(
            uploadConfig: await AddUploadConfigAsync(registration),
            uploadState: UploadState.Completed,
            hosterFolderId: "folder",
            files: [("folder", OnlineState.Offline), ("folder", OnlineState.Offline)]
        );

        // Act
        var result = await repository.GetFolderIdsWithoutOnlineFilesAsync(
            registration.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(["folder"]);
    }

    [Test]
    public async Task GetFolderIdsWithoutOnlineFilesAsync_OnlineFilesOfSupersededUpload_DoNotBlockFolder()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync("Hoster");
        var uploadConfig = await AddUploadConfigAsync(registration);
        await AddUploadAsync(
            uploadConfig: uploadConfig,
            uploadState: UploadState.Completed,
            hosterFolderId: "superseded",
            files: [("superseded", OnlineState.Online)]
        );
        await AddUploadAsync(
            uploadConfig: uploadConfig,
            uploadState: UploadState.Completed,
            hosterFolderId: "current",
            files: [("current", OnlineState.Online)]
        );

        // Act
        var result = await repository.GetFolderIdsWithoutOnlineFilesAsync(
            registration.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(["superseded"]);
    }

    [TestCase(UploadState.Pending)]
    [TestCase(UploadState.Uploading)]
    [TestCase(UploadState.CancellationRequested)]
    public async Task GetFolderIdsWithoutOnlineFilesAsync_RunningUploadReferencesFolderAsUploadFolder_ExcludesFolder(
        UploadState runningUploadState
    )
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync("Hoster");
        await AddUploadAsync(
            uploadConfig: await AddUploadConfigAsync(registration),
            uploadState: UploadState.Completed,
            hosterFolderId: "folder",
            files: [("folder", OnlineState.Offline)]
        );
        await AddUploadAsync(
            uploadConfig: await AddUploadConfigAsync(registration),
            uploadState: runningUploadState,
            hosterFolderId: "folder",
            files: []
        );

        // Act
        var result = await repository.GetFolderIdsWithoutOnlineFilesAsync(
            registration.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBeEmpty();
    }

    [TestCase(UploadState.Pending)]
    [TestCase(UploadState.Uploading)]
    [TestCase(UploadState.CancellationRequested)]
    public async Task GetFolderIdsWithoutOnlineFilesAsync_RunningUploadHasFileInFolder_ExcludesFolder(
        UploadState runningUploadState
    )
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync("Hoster");
        await AddUploadAsync(
            uploadConfig: await AddUploadConfigAsync(registration),
            uploadState: UploadState.Completed,
            hosterFolderId: "folder",
            files: [("folder", OnlineState.Offline)]
        );
        await AddUploadAsync(
            uploadConfig: await AddUploadConfigAsync(registration),
            uploadState: runningUploadState,
            hosterFolderId: null,
            files: [("folder", OnlineState.Offline)]
        );

        // Act
        var result = await repository.GetFolderIdsWithoutOnlineFilesAsync(
            registration.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetFolderIdsWithoutOnlineFilesAsync_SameFolderIdOnOtherHosterRegistration_DoesNotAffectFolder()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync("Hoster");
        var otherRegistration = await AddHosterRegistrationAsync("Other");
        await AddUploadAsync(
            uploadConfig: await AddUploadConfigAsync(registration),
            uploadState: UploadState.Completed,
            hosterFolderId: "folder",
            files: [("folder", OnlineState.Offline)]
        );
        await AddUploadAsync(
            uploadConfig: await AddUploadConfigAsync(otherRegistration),
            uploadState: UploadState.Completed,
            hosterFolderId: "folder",
            files: [("folder", OnlineState.Online)]
        );
        await AddUploadAsync(
            uploadConfig: await AddUploadConfigAsync(otherRegistration),
            uploadState: UploadState.Uploading,
            hosterFolderId: "folder",
            files: [("folder", OnlineState.Online)]
        );

        // Act
        var result = await repository.GetFolderIdsWithoutOnlineFilesAsync(
            registration.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(["folder"]);
    }

    [Test]
    public async Task GetFolderIdsWithoutOnlineFilesAsync_FolderSharedByTwoUploads_ReturnsFolderOnce()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync("Hoster");
        await AddUploadAsync(
            uploadConfig: await AddUploadConfigAsync(registration),
            uploadState: UploadState.Completed,
            hosterFolderId: "folder",
            files: [("folder", OnlineState.Offline)]
        );
        await AddUploadAsync(
            uploadConfig: await AddUploadConfigAsync(registration),
            uploadState: UploadState.Completed,
            hosterFolderId: "folder",
            files: [("folder", OnlineState.Offline)]
        );

        // Act
        var result = await repository.GetFolderIdsWithoutOnlineFilesAsync(
            registration.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(["folder"]);
    }

    [TestCase(UploadState.Pending)]
    [TestCase(UploadState.Uploading)]
    [TestCase(UploadState.CancellationRequested)]
    public async Task GetFolderIdsWithoutOnlineFilesAsync_RunningUploadOfSameReleaseWithoutFolder_ExcludesFolder(
        UploadState runningUploadState
    )
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync("Hoster");
        var uploadConfig = await AddUploadConfigAsync(registration);
        await AddUploadAsync(
            uploadConfig: uploadConfig,
            uploadState: UploadState.Completed,
            hosterFolderId: "folder",
            files: [("folder", OnlineState.Offline)]
        );
        await AddUploadAsync(
            uploadConfig: uploadConfig,
            uploadState: runningUploadState,
            hosterFolderId: null,
            files: []
        );

        // Act
        var result = await repository.GetFolderIdsWithoutOnlineFilesAsync(
            registration.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetFolderIdsWithoutOnlineFilesAsync_RunningUploadOfOtherReleaseWithoutFolder_ReturnsFolder()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync("Hoster");
        var uploadConfig = await AddUploadConfigAsync(registration);
        var uploadConfigOfOtherRelease = await AddUploadConfigAsync(registration);
        await AddUploadAsync(
            uploadConfig: uploadConfig,
            uploadState: UploadState.Completed,
            hosterFolderId: "folder",
            files: [("folder", OnlineState.Offline)]
        );
        await AddUploadAsync(
            uploadConfig: uploadConfigOfOtherRelease,
            uploadState: UploadState.Pending,
            hosterFolderId: null,
            files: []
        );

        // Act
        var result = await repository.GetFolderIdsWithoutOnlineFilesAsync(
            registration.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(["folder"]);
    }

    [Test]
    public async Task IsFolderWithoutOnlineFilesAsync_FolderWithOfflineFilesAndFolderWithOnlineFile_ReturnsTrueOnlyForOfflineFolder()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync("Hoster");
        await AddUploadAsync(
            uploadConfig: await AddUploadConfigAsync(registration),
            uploadState: UploadState.Completed,
            hosterFolderId: "offline-folder",
            files: [("offline-folder", OnlineState.Offline)]
        );
        await AddUploadAsync(
            uploadConfig: await AddUploadConfigAsync(registration),
            uploadState: UploadState.Completed,
            hosterFolderId: "online-folder",
            files: [("online-folder", OnlineState.Online)]
        );

        // Act
        var isOfflineFolderWithoutOnlineFiles = await repository.IsFolderWithoutOnlineFilesAsync(
            registration.Id,
            "offline-folder",
            CancellationToken.None
        );
        var isOnlineFolderWithoutOnlineFiles = await repository.IsFolderWithoutOnlineFilesAsync(
            registration.Id,
            "online-folder",
            CancellationToken.None
        );

        // Assert
        isOfflineFolderWithoutOnlineFiles.ShouldBeTrue();
        isOnlineFolderWithoutOnlineFiles.ShouldBeFalse();
    }

    [Test]
    public async Task ClearFolderIdAsync_FolderUsedOnSeveralHosterRegistrations_ClearsFolderIdOnlyOnUploadsAndFilesOfGivenRegistration()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync("Hoster");
        var otherRegistration = await AddHosterRegistrationAsync("Other");
        var upload = await AddUploadAsync(
            uploadConfig: await AddUploadConfigAsync(registration),
            uploadState: UploadState.Completed,
            hosterFolderId: "folder",
            files: [("folder", OnlineState.Offline), ("other-folder", OnlineState.Offline)]
        );
        var uploadWithCarriedOverFile = await AddUploadAsync(
            uploadConfig: await AddUploadConfigAsync(registration),
            uploadState: UploadState.Completed,
            hosterFolderId: "other-folder",
            files: [("folder", OnlineState.Offline)]
        );
        var uploadOfOtherRegistration = await AddUploadAsync(
            uploadConfig: await AddUploadConfigAsync(otherRegistration),
            uploadState: UploadState.Completed,
            hosterFolderId: "folder",
            files: [("folder", OnlineState.Offline)]
        );

        // Act
        await repository.ClearFolderIdAsync(registration.Id, "folder", CancellationToken.None);

        // Assert
        var uploadFolderIds = await ReadDbContext.Uploads.ToDictionaryAsync(
            u => u.Id,
            u => u.HosterFolderId
        );
        var fileFolderIds = await ReadDbContext
            .UploadedFiles.OrderBy(file => file.Id)
            .Select(file => new { file.UploadId, file.HosterFolderId })
            .ToListAsync();

        uploadFolderIds[upload.Id].ShouldBeNull();
        uploadFolderIds[uploadWithCarriedOverFile.Id].ShouldBe("other-folder");
        uploadFolderIds[uploadOfOtherRegistration.Id].ShouldBe("folder");
        fileFolderIds
            .Select(file => (file.UploadId, file.HosterFolderId))
            .ShouldBe([
                (upload.Id, null),
                (upload.Id, "other-folder"),
                (uploadWithCarriedOverFile.Id, null),
                (uploadOfOtherRegistration.Id, "folder"),
            ]);
    }

    private async Task<HosterRegistration> AddHosterRegistrationAsync(string name)
    {
        var registration = new HosterRegistration
        {
            Name = name,
            SerializedConfig = "{}",
            HosterClassName = "Hoster",
            IsActive = true,
        };

        DbContext.HosterRegistrations.Add(registration);
        await DbContext.SaveChangesAsync();

        return registration;
    }

    private async Task<UploadConfig> AddUploadConfigAsync(HosterRegistration hosterRegistration)
    {
        var releaseNumber = await DbContext.Releases.CountAsync() + 1;
        var release = new Release
        {
            Name = $"Bearcat.Release.{releaseNumber:000}",
            CreatedAt = LocalNow.AddDays(-10),
            ReleaseType = ReleaseType.Unmanaged,
            ReleaseGroup = new ReleaseGroup { Name = "Release group" },
        };
        var uploadConfig = new UploadConfig
        {
            UploadSource = UploadSource.ArchiveFiles,
            Release = release,
            ArchiveConfig = new ArchiveConfig
            {
                Release = release,
                Name = "Main archive",
                ArchiveFilesBasePath = "/archives",
                ArchiverName = "zip",
                ArchiveNamePrefix = "bearcat-release",
                ArchivePassword = "secret",
                ArchiveFileSizeMb = 512,
            },
            HosterRegistrationId = hosterRegistration.Id,
            Name = "Default upload",
        };

        DbContext.UploadConfigs.Add(uploadConfig);
        await DbContext.SaveChangesAsync();

        return uploadConfig;
    }

    private async Task<Upload> AddUploadAsync(
        UploadConfig uploadConfig,
        UploadState uploadState,
        string? hosterFolderId,
        IReadOnlyList<(string? HosterFolderId, OnlineState OnlineState)> files
    )
    {
        var upload = new Upload
        {
            UploadConfigId = uploadConfig.Id,
            HosterRegistrationId = uploadConfig.HosterRegistrationId,
            HosterFolderId = hosterFolderId,
            CreatedAt = LocalNow.AddDays(-5),
            UploadState = uploadState,
            OnlineState = OnlineState.Unknown,
            ErrorMessages = [],
            UploadedFiles = files
                .Select(
                    (file, index) =>
                        new UploadedFile
                        {
                            VideoFileRelativePath = $"video-{index + 1}.mkv",
                            HosterFileLink = $"https://hoster.test/{index + 1}",
                            HosterFolderId = file.HosterFolderId,
                            OnlineState = file.OnlineState,
                            CreatedAt = LocalNow.AddDays(-5),
                            ErrorMessages = [],
                        }
                )
                .ToList(),
        };

        DbContext.Uploads.Add(upload);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return upload;
    }
}

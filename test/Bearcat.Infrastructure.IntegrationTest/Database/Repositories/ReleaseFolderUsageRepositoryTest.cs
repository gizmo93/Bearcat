using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class ReleaseFolderUsageRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private ReleaseFolderUsageRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        repository = new ReleaseFolderUsageRepository(DbContext);
    }

    [Test]
    public async Task GetUsedReleaseArchiveOrRemoteDownloadFolderPathsAsync_PathsUsedByReleaseArchiveAndRemoteDownload_ReturnsOnlyRequestedUsedPaths()
    {
        // Arrange
        AddRelease(
            name: "Managed",
            releaseType: ReleaseType.Managed,
            releaseFolderPath: "/releases/Managed",
            archiveFilesBasePath: "/archives/Managed"
        );
        AddRelease(
            name: "Other",
            releaseType: ReleaseType.Managed,
            releaseFolderPath: "/releases/Other",
            archiveFilesBasePath: "/archives/Other"
        );
        AddRelease(
            name: "Unmanaged",
            releaseType: ReleaseType.Unmanaged,
            releaseFolderPath: null,
            archiveFilesBasePath: "/archives/Unmanaged"
        );
        AddRemoteDownload("/downloads/Download");
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetUsedReleaseArchiveOrRemoteDownloadFolderPathsAsync(
            ["/releases/Managed", "/archives/Unmanaged", "/downloads/Download", "/unused/Folder"],
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(
            ["/releases/Managed", "/archives/Unmanaged", "/downloads/Download"],
            ignoreOrder: true
        );
    }

    [Test]
    public async Task GetUsedReleaseArchiveOrRemoteDownloadFolderPathsAsync_PathIsArchiveFilesBasePathOfManagedRelease_ReturnsNoPath()
    {
        // Arrange
        AddRelease(
            name: "Managed",
            releaseType: ReleaseType.Managed,
            releaseFolderPath: "/releases/Managed",
            archiveFilesBasePath: "/archives/Managed"
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetUsedReleaseArchiveOrRemoteDownloadFolderPathsAsync(
            ["/archives/Managed"],
            CancellationToken.None
        );

        // Assert
        result.ShouldBeEmpty();
    }

    private void AddRelease(
        string name,
        ReleaseType releaseType,
        string? releaseFolderPath,
        string archiveFilesBasePath
    )
    {
        var release = new Release
        {
            Name = name,
            ReleaseType = releaseType,
            ReleaseFolderPath = releaseFolderPath,
            ReleaseGroup = new ReleaseGroup
            {
                Name = $"{name} group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
        };

        DbContext.ArchiveConfigs.Add(
            new ArchiveConfig
            {
                Release = release,
                Name = "Main archive",
                ArchiveFilesBasePath = archiveFilesBasePath,
                ArchiverName = "zip",
                ArchiveNamePrefix = name,
                ArchivePassword = "secret",
                ArchiveFileSizeMb = 512,
                Archives = [],
                UploadConfigs = [],
            }
        );
    }

    private void AddRemoteDownload(string localFolderPath)
    {
        DbContext.RemoteSourceDownloads.Add(
            new RemoteSourceDownload
            {
                SourceName = "Main FTP",
                RemoteFolderPath = "/incoming/Download",
                FolderName = "Download",
                LocalFolderPath = localFolderPath,
                State = RemoteSourceDownloadState.ReleaseCreated,
                FileCount = 1,
                TotalBytes = 100,
                LastChangedAt = new DateTime(2026, 9, 23, 12, 0, 0, DateTimeKind.Unspecified),
                DiscoveredAt = new DateTime(2026, 9, 23, 12, 0, 0, DateTimeKind.Unspecified),
            }
        );
    }
}

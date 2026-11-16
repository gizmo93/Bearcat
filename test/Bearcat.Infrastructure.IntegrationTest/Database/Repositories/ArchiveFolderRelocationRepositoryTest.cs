using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class ArchiveFolderRelocationRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    [Test]
    public async Task GetArchiveIdsThatAreNewestOfTheirArchiveConfigAsync_NewerArchiveNotRequested_ReturnsOnlyArchivesWithoutNewerArchive()
    {
        // Arrange
        var firstArchiveConfig = await AddArchiveConfigWithArchivesAsync(
            releaseName: "First",
            archives:
            [
                (ArchiveState.Deleted, "/archives/first/1"),
                (ArchiveState.Deleted, "/archives/first/2"),
            ]
        );

        var secondArchiveConfig = await AddArchiveConfigWithArchivesAsync(
            releaseName: "Second",
            archives: [(ArchiveState.MissingFiles, "/archives/second/1")]
        );

        var olderArchiveOfFirstConfig = firstArchiveConfig.Archives.MinBy(archive => archive.Id)!;
        var onlyArchiveOfSecondConfig = secondArchiveConfig.Archives.Single();

        // Act
        var result = await CreateRepository()
            .GetArchiveIdsThatAreNewestOfTheirArchiveConfigAsync(
                [olderArchiveOfFirstConfig.Id, onlyArchiveOfSecondConfig.Id],
                CancellationToken.None
            );

        // Assert
        result.ShouldBe([onlyArchiveOfSecondConfig.Id]);
    }

    [Test]
    public async Task GetFolderPathsUsedByCreatedArchivesAsync_ArchivesInDifferentStates_ReturnsOnlyFolderPathsOfCreatedArchives()
    {
        // Arrange
        await AddArchiveConfigWithArchivesAsync(
            releaseName: "Release",
            archives:
            [
                (ArchiveState.Deleted, "/archives/deleted"),
                (ArchiveState.MissingFiles, "/archives/missing"),
                (ArchiveState.Created, "/archives/created"),
                (ArchiveState.Created, "/archives/created-not-requested"),
            ]
        );

        // Act
        var result = await CreateRepository()
            .GetFolderPathsUsedByCreatedArchivesAsync(
                ["/archives/deleted", "/archives/missing", "/archives/created", "/archives/empty"],
                CancellationToken.None
            );

        // Assert
        result.ShouldBe(["/archives/created"]);
    }

    private async Task<ArchiveConfig> AddArchiveConfigWithArchivesAsync(
        string releaseName,
        IReadOnlyList<(ArchiveState ArchiveState, string ArchiveFolderPath)> archives
    )
    {
        var setupDbContext = CreateDbContext();

        var archiveConfig = new ArchiveConfig
        {
            Release = new Release
            {
                Name = releaseName,
                ReleaseType = ReleaseType.Unmanaged,
                ReleaseGroup = new ReleaseGroup
                {
                    Name = $"{releaseName} group",
                    EnableAutomaticReuploads = false,
                    NumberOfHoursUntilReupload = 24,
                },
            },
            Name = "RAR",
            ArchiveFilesBasePath = "/archives",
            ArchiverName = "rar",
            UploadConfigs = [],
            Archives = [],
        };

        setupDbContext.ArchiveConfigs.Add(archiveConfig);

        foreach (var (archiveState, archiveFolderPath) in archives)
        {
            archiveConfig.Archives.Add(
                new Archive
                {
                    ArchiveFolderPath = archiveFolderPath,
                    ArchiveState = archiveState,
                    CreatedAt = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc),
                    ArchiveFiles = [],
                    Uploads = [],
                    ErrorMessages = [],
                    Notifications = [],
                }
            );

            await setupDbContext.SaveChangesAsync();
        }

        return archiveConfig;
    }

    private ArchiveFolderRelocationRepository CreateRepository()
    {
        return new ArchiveFolderRelocationRepository(CreateDbContext());
    }
}

using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageArchiveStorageFolders.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class ArchiveStorageFolderRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    [Test]
    public async Task GetAllAsync_StorageFoldersWithStoredArchives_ReturnsFoldersOrderedByPriorityThenNameWithArchiveCounts()
    {
        // Arrange
        var nasFolder = await AddStorageFolderAsync(
            CreateStorageFolder("NAS", "/mnt/nas", priority: 2)
        );
        var secondDiskFolder = await AddStorageFolderAsync(
            CreateStorageFolder("Second disk", "/mnt/disk2", priority: 1)
        );
        var cloudFolder = await AddStorageFolderAsync(
            new ArchiveStorageFolder
            {
                Name = "Cloud",
                Path = "/mnt/cloud",
                IsActive = false,
                MinimumFreeSpaceGb = 0,
                MaxParallelFileCopies = 4,
                Priority = 2,
                UseLocalWorkingCopyForReuploads = true,
            }
        );
        await AddArchiveAsync(nasFolder.Id);
        await AddArchiveAsync(nasFolder.Id);
        await AddArchiveAsync(cloudFolder.Id);
        await AddArchiveAsync(archiveStorageFolderId: null);

        // Act
        var result = await CreateRepository().GetAllAsync();

        // Assert
        result.ShouldBe([
            new ArchiveStorageFolderReadModel(
                secondDiskFolder.Id,
                "Second disk",
                "/mnt/disk2",
                IsActive: true,
                MinimumFreeSpaceGb: 25,
                MaxParallelFileCopies: 1,
                Priority: 1,
                UseLocalWorkingCopyForReuploads: false,
                StoredArchiveCount: 0
            ),
            new ArchiveStorageFolderReadModel(
                cloudFolder.Id,
                "Cloud",
                "/mnt/cloud",
                IsActive: false,
                MinimumFreeSpaceGb: 0,
                MaxParallelFileCopies: 4,
                Priority: 2,
                UseLocalWorkingCopyForReuploads: true,
                StoredArchiveCount: 1
            ),
            new ArchiveStorageFolderReadModel(
                nasFolder.Id,
                "NAS",
                "/mnt/nas",
                IsActive: true,
                MinimumFreeSpaceGb: 25,
                MaxParallelFileCopies: 1,
                Priority: 2,
                UseLocalWorkingCopyForReuploads: false,
                StoredArchiveCount: 2
            ),
        ]);
    }

    [Test]
    public async Task GetAllAsync_ArchivesInSeveralStates_CountsOnlyCreatedArchives()
    {
        // Arrange
        var storageFolder = await AddStorageFolderAsync(CreateStorageFolder("NAS", "/mnt/nas"));
        await AddArchiveAsync(storageFolder.Id);
        await AddArchiveAsync(storageFolder.Id, ArchiveState.MissingFiles);
        await AddArchiveAsync(storageFolder.Id, ArchiveState.Deleted);

        // Act
        var result = await CreateRepository().GetAllAsync();

        // Assert
        result.Single().StoredArchiveCount.ShouldBe(1);
    }

    private ArchiveStorageFolderRepository CreateRepository()
    {
        var dbContext = CreateDbContext();

        return new ArchiveStorageFolderRepository(dbContext, dbContext);
    }

    private static ArchiveStorageFolder CreateStorageFolder(
        string name,
        string path,
        int priority = 1
    )
    {
        return new ArchiveStorageFolder
        {
            Name = name,
            Path = path,
            IsActive = true,
            MinimumFreeSpaceGb = 25,
            Priority = priority,
            UseLocalWorkingCopyForReuploads = false,
        };
    }

    private async Task<ArchiveStorageFolder> AddStorageFolderAsync(
        ArchiveStorageFolder storageFolder
    )
    {
        var dbContext = CreateDbContext();
        dbContext.ArchiveStorageFolders.Add(storageFolder);
        await dbContext.SaveChangesAsync();

        return storageFolder;
    }

    private async Task<Archive> AddArchiveAsync(
        int? archiveStorageFolderId,
        ArchiveState archiveState = ArchiveState.Created
    )
    {
        var dbContext = CreateDbContext();
        var archive = new Archive
        {
            ArchiveConfig = new ArchiveConfig
            {
                Name = "RAR Forum A",
                ArchiveFilesBasePath = "/tmp/archives",
                ArchiverName = "rar",
                ArchiveFileSizeMb = 1024,
                Release = new Release
                {
                    Name = $"Bearcat.Release.{Guid.NewGuid():N}",
                    CreatedAt = DateTime.UtcNow,
                    ReleaseType = ReleaseType.Managed,
                    ReleaseFolderPath = "/tmp/releases/Bearcat.Release",
                    ReleaseGroup = new ReleaseGroup
                    {
                        Name = $"Group {Guid.NewGuid():N}",
                        EnableAutomaticReuploads = false,
                        NumberOfHoursUntilReupload = 24,
                    },
                },
            },
            ArchiveFolderPath = "/mnt/nas/Bearcat.Release",
            ArchiveStorageFolderId = archiveStorageFolderId,
            CreatedAt = DateTime.UtcNow,
            ArchiveState = archiveState,
            ArchiveFileSizeMb = 1024,
            ArchiveFiles =
            [
                new ArchiveFile
                {
                    FullFileName = "/mnt/nas/Bearcat.Release/release.part1.rar",
                    Md5Hash = "11111111111111111111111111111111",
                    Md5HashInStorageFolder = "11111111111111111111111111111111",
                },
            ],
        };
        dbContext.Archives.Add(archive);
        await dbContext.SaveChangesAsync();

        return archive;
    }
}

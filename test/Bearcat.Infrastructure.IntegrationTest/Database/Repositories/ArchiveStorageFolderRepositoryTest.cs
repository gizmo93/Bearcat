using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageArchiveStorageFolders.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using EntityFramework.Exceptions.Common;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class ArchiveStorageFolderRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    [Test]
    public async Task GetAllAsync_NoStorageFolders_ReturnsEmptyList()
    {
        // Act
        var result = await CreateRepository().GetAllAsync();

        // Assert
        result.ShouldBeEmpty();
    }

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
                Priority = 2,
                RetrieveArchivesBeforeReupload = true,
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
                Priority: 1,
                RetrieveArchivesBeforeReupload: false,
                StoredArchiveCount: 0
            ),
            new ArchiveStorageFolderReadModel(
                cloudFolder.Id,
                "Cloud",
                "/mnt/cloud",
                IsActive: false,
                MinimumFreeSpaceGb: 0,
                Priority: 2,
                RetrieveArchivesBeforeReupload: true,
                StoredArchiveCount: 1
            ),
            new ArchiveStorageFolderReadModel(
                nasFolder.Id,
                "NAS",
                "/mnt/nas",
                IsActive: true,
                MinimumFreeSpaceGb: 25,
                Priority: 2,
                RetrieveArchivesBeforeReupload: false,
                StoredArchiveCount: 2
            ),
        ]);
    }

    [Test]
    public async Task GetByIdAsync_ChangedStorageFolder_PersistsChangesOnSave()
    {
        // Arrange
        var storageFolder = await AddStorageFolderAsync(CreateStorageFolder("NAS", "/mnt/nas"));
        var repository = CreateRepository();

        // Act
        var loadedStorageFolder = await repository.GetByIdAsync(
            storageFolder.Id,
            CancellationToken.None
        );
        loadedStorageFolder.IsActive = false;
        await repository.SaveChangesAsync(CancellationToken.None);

        // Assert
        (await CreateRepository().GetAllAsync())
            .Single()
            .IsActive.ShouldBeFalse();
    }

    [Test]
    public async Task NameExistsAsync_NameOfExistingStorageFolder_ReturnsTrue()
    {
        // Arrange
        await AddStorageFolderAsync(CreateStorageFolder("NAS", "/mnt/nas"));

        // Act
        var result = await CreateRepository()
            .NameExistsAsync("NAS", excludedArchiveStorageFolderId: null, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public async Task NameExistsAsync_NameOnlyUsedByExcludedStorageFolder_ReturnsFalse()
    {
        // Arrange
        var storageFolder = await AddStorageFolderAsync(CreateStorageFolder("NAS", "/mnt/nas"));

        // Act
        var result = await CreateRepository()
            .NameExistsAsync("NAS", storageFolder.Id, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public async Task NameExistsAsync_UnusedName_ReturnsFalse()
    {
        // Arrange
        await AddStorageFolderAsync(CreateStorageFolder("NAS", "/mnt/nas"));

        // Act
        var result = await CreateRepository()
            .NameExistsAsync(
                "Second disk",
                excludedArchiveStorageFolderId: null,
                CancellationToken.None
            );

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public async Task PathExistsAsync_PathOfExistingStorageFolder_ReturnsTrue()
    {
        // Arrange
        await AddStorageFolderAsync(CreateStorageFolder("NAS", "/mnt/nas"));

        // Act
        var result = await CreateRepository()
            .PathExistsAsync(
                "/mnt/nas",
                excludedArchiveStorageFolderId: null,
                CancellationToken.None
            );

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public async Task PathExistsAsync_PathOnlyUsedByExcludedStorageFolder_ReturnsFalse()
    {
        // Arrange
        var storageFolder = await AddStorageFolderAsync(CreateStorageFolder("NAS", "/mnt/nas"));

        // Act
        var result = await CreateRepository()
            .PathExistsAsync("/mnt/nas", storageFolder.Id, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public async Task PathExistsAsync_UnusedPath_ReturnsFalse()
    {
        // Arrange
        await AddStorageFolderAsync(CreateStorageFolder("NAS", "/mnt/nas"));

        // Act
        var result = await CreateRepository()
            .PathExistsAsync(
                "/mnt/nas/archives",
                excludedArchiveStorageFolderId: null,
                CancellationToken.None
            );

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public async Task GetStoredArchiveCountAsync_ArchivesInSeveralStorageFolders_CountsOnlyArchivesOfRequestedFolder()
    {
        // Arrange
        var nasFolder = await AddStorageFolderAsync(CreateStorageFolder("NAS", "/mnt/nas"));
        var otherFolder = await AddStorageFolderAsync(
            CreateStorageFolder("Second disk", "/mnt/disk2")
        );
        await AddArchiveAsync(nasFolder.Id);
        await AddArchiveAsync(nasFolder.Id);
        await AddArchiveAsync(otherFolder.Id);
        await AddArchiveAsync(archiveStorageFolderId: null);

        // Act
        var result = await CreateRepository()
            .GetStoredArchiveCountAsync(nasFolder.Id, CancellationToken.None);

        // Assert
        result.ShouldBe(2);
    }

    [Test]
    public async Task Remove_StorageFolderWithoutStoredArchives_DeletesStorageFolder()
    {
        // Arrange
        var storageFolder = await AddStorageFolderAsync(CreateStorageFolder("NAS", "/mnt/nas"));
        var repository = CreateRepository();
        repository.Remove(await repository.GetByIdAsync(storageFolder.Id, CancellationToken.None));

        // Act
        await repository.SaveChangesAsync(CancellationToken.None);

        // Assert
        (await CreateRepository().GetAllAsync()).ShouldBeEmpty();
    }

    [Test]
    public async Task Remove_StorageFolderWithStoredArchive_ThrowsDbUpdateExceptionAndKeepsStorageFolder()
    {
        // Arrange
        var storageFolder = await AddStorageFolderAsync(CreateStorageFolder("NAS", "/mnt/nas"));
        var archive = await AddArchiveAsync(storageFolder.Id);
        var repository = CreateRepository();
        repository.Remove(await repository.GetByIdAsync(storageFolder.Id, CancellationToken.None));

        // Act
        var result = await Should.ThrowAsync<DbUpdateException>(() =>
            repository.SaveChangesAsync(CancellationToken.None)
        );

        // Assert
        result.ShouldNotBeNull();
        (await CreateRepository().GetAllAsync()).Single().StoredArchiveCount.ShouldBe(1);
        await using var readDbContext = Database.CreateDbContext();
        (
            await readDbContext.Archives.SingleAsync(entity => entity.Id == archive.Id)
        ).ArchiveStorageFolderId.ShouldBe(storageFolder.Id);
    }

    [Test]
    public async Task SaveChangesAsync_DuplicateName_ThrowsUniqueConstraintException()
    {
        // Arrange
        await AddStorageFolderAsync(CreateStorageFolder("NAS", "/mnt/nas"));
        var repository = CreateRepository();
        repository.Add(CreateStorageFolder("NAS", "/mnt/disk2"));

        // Act
        var result = await Should.ThrowAsync<UniqueConstraintException>(() =>
            repository.SaveChangesAsync(CancellationToken.None)
        );

        // Assert
        result.ShouldNotBeNull();
    }

    [Test]
    public async Task SaveChangesAsync_DuplicatePath_ThrowsUniqueConstraintException()
    {
        // Arrange
        await AddStorageFolderAsync(CreateStorageFolder("NAS", "/mnt/nas"));
        var repository = CreateRepository();
        repository.Add(CreateStorageFolder("Second disk", "/mnt/nas"));

        // Act
        var result = await Should.ThrowAsync<UniqueConstraintException>(() =>
            repository.SaveChangesAsync(CancellationToken.None)
        );

        // Assert
        result.ShouldNotBeNull();
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
            RetrieveArchivesBeforeReupload = false,
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

    private async Task<Archive> AddArchiveAsync(int? archiveStorageFolderId)
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
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 1024,
        };
        dbContext.Archives.Add(archive);
        await dbContext.SaveChangesAsync();

        return archive;
    }
}

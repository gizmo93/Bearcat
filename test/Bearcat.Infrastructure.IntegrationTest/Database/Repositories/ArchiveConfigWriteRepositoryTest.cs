using Bearcat.Domain.Entities;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class ArchiveConfigWriteRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    [Test]
    public async Task GetArchiveStorageFoldersAsync_NoStorageFolders_ReturnsEmptyList()
    {
        // Act
        var result = await CreateRepository().GetArchiveStorageFoldersAsync();

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetArchiveStorageFoldersAsync_ActiveAndInactiveStorageFolders_ReturnsAllStorageFoldersOrderedById()
    {
        // Arrange
        var setupDbContext = CreateDbContext();
        var nasFolder = new ArchiveStorageFolder
        {
            Name = "NAS",
            Path = "/mnt/nas",
            IsActive = true,
            Priority = 2,
        };
        var inactiveFolder = new ArchiveStorageFolder
        {
            Name = "Old disk",
            Path = "/mnt/old",
            IsActive = false,
            Priority = 1,
        };
        setupDbContext.ArchiveStorageFolders.AddRange(nasFolder, inactiveFolder);
        await setupDbContext.SaveChangesAsync();

        // Act
        var result = await CreateRepository().GetArchiveStorageFoldersAsync();

        // Assert
        result
            .Select(storageFolder => (storageFolder.Id, storageFolder.Path))
            .ShouldBe([(nasFolder.Id, "/mnt/nas"), (inactiveFolder.Id, "/mnt/old")]);
    }

    private ArchiveConfigWriteRepository CreateRepository()
    {
        return new ArchiveConfigWriteRepository(CreateDbContext());
    }
}

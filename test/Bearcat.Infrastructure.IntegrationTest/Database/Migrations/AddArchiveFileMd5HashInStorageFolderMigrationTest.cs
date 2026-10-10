using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Migrations;

public class AddArchiveFileMd5HashInStorageFolderMigrationTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string PreviousMigrationName =
        "ReplaceAutoDeleteArchivesWithArchiveRetentionAction";
    private const string MigrationName = "AddArchiveFileMd5HashInStorageFolder";
    private const string StorageFileHash = "11111111111111111111111111111111";
    private const string LocalFileHash = "22222222222222222222222222222222";

    [TearDown]
    public async Task MigrateToLatestMigrationAsync()
    {
        await DbContext.Database.MigrateAsync();
    }

    [Test]
    public async Task Up_ArchiveFilesInStorageFolderAndLocal_CopiesHashOnlyForArchivesInStorageFolder()
    {
        // Arrange
        var storageArchive = CreateArchive(
            "Stored",
            StorageFileHash,
            new ArchiveStorageFolder
            {
                Name = "NAS",
                Path = "/mnt/nas",
                IsActive = true,
            }
        );
        var localArchive = CreateArchive("Local", LocalFileHash, archiveStorageFolder: null);
        DbContext.Archives.AddRange(storageArchive, localArchive);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
        await MigrateAsync(PreviousMigrationName);

        // Act
        await MigrateAsync(MigrationName);

        // Assert
        var archiveFiles = await DbContext
            .ArchiveFiles.OrderBy(file => file.Id)
            .Select(file => new { file.ArchiveId, file.Md5HashInStorageFolder })
            .ToListAsync();
        archiveFiles
            .Select(file => (file.ArchiveId, file.Md5HashInStorageFolder))
            .ShouldBe([(storageArchive.Id, StorageFileHash), (localArchive.Id, null)]);
    }

    private async Task MigrateAsync(string targetMigration)
    {
        await DbContext.GetService<IMigrator>().MigrateAsync(targetMigration);
        DbContext.ChangeTracker.Clear();
    }

    private static Archive CreateArchive(
        string name,
        string md5Hash,
        ArchiveStorageFolder? archiveStorageFolder
    )
    {
        return new Archive
        {
            ArchiveConfig = new ArchiveConfig
            {
                Name = "Main archive",
                ArchiveFilesBasePath = "/archives",
                ArchiverName = "rar",
                ArchiveFileSizeMb = 512,
                Release = new Release
                {
                    Name = name,
                    ReleaseType = ReleaseType.Unmanaged,
                    ReleaseGroup = new ReleaseGroup
                    {
                        Name = $"{name} group",
                        EnableAutomaticReuploads = false,
                        NumberOfHoursUntilReupload = 24,
                    },
                },
            },
            ArchiveFolderPath = $"/archives/{name}",
            ArchiveStorageFolder = archiveStorageFolder,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 512,
            CreatedAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
            ArchiveFiles =
            [
                new ArchiveFile
                {
                    FullFileName = $"/archives/{name}/{name}.rar",
                    Md5Hash = md5Hash,
                },
            ],
            Uploads = [],
            ErrorMessages = [],
        };
    }
}

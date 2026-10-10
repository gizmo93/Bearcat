using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageArchiveStorageFolders;
using Bearcat.Domain.UseCases.ManageArchiveStorageFolders.Dto;
using Bearcat.Domain.UseCases.ManageArchiveStorageFolders.Validation;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.FileSystem;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageArchiveStorageFolders;

public class ArchiveStorageFolderServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private string tempRootPath = null!;
    private string workingDirectoryPath = null!;
    private string storageFolderPath = null!;
    private string otherStorageFolderPath = null!;
    private ArchiveStorageFolderService service = null!;

    [SetUp]
    public void Setup()
    {
        tempRootPath = Path.Combine(Path.GetTempPath(), $"bearcat-tests-{Guid.NewGuid():N}");
        workingDirectoryPath = Directory
            .CreateDirectory(Path.Combine(tempRootPath, "working"))
            .FullName;
        storageFolderPath = Directory
            .CreateDirectory(Path.Combine(tempRootPath, "storage-a"))
            .FullName;
        otherStorageFolderPath = Directory
            .CreateDirectory(Path.Combine(tempRootPath, "storage-b"))
            .FullName;
        var repository = new ArchiveStorageFolderRepository(DbContext, DbContext);
        service = new ArchiveStorageFolderService(
            repository,
            repository,
            new FileSystemService(),
            Options.Create(
                new WorkingDirectoriesConfig { WorkingDirectories = [workingDirectoryPath] }
            )
        );
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
    public async Task CreateAsync_ValidInput_PersistsTrimmedActiveStorageFolder()
    {
        // Act
        var result = await service.CreateAsync(
            new ArchiveStorageFolderInput(
                "  NAS archive  ",
                $"  {storageFolderPath}  ",
                MinimumFreeSpaceGb: 50,
                Priority: 2,
                UseLocalWorkingCopyForReuploads: true
            )
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var storageFolder = await LoadStorageFolderAsync();
        storageFolder.Id.ShouldBe(result.ArchiveStorageFolderId!.Value);
        storageFolder.Name.ShouldBe("NAS archive");
        storageFolder.Path.ShouldBe(storageFolderPath);
        storageFolder.IsActive.ShouldBeTrue();
        storageFolder.MinimumFreeSpaceGb.ShouldBe(50);
        storageFolder.Priority.ShouldBe(2);
        storageFolder.UseLocalWorkingCopyForReuploads.ShouldBeTrue();
    }

    [Test]
    public async Task CreateAsync_PathWithTrailingSeparator_PersistsPathWithoutTrailingSeparator()
    {
        // Act
        var result = await service.CreateAsync(
            CreateInput("NAS archive", storageFolderPath + Path.DirectorySeparatorChar)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await LoadStorageFolderAsync()).Path.ShouldBe(storageFolderPath);
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task CreateAsync_BlankName_ReturnsNameRequired(string name)
    {
        // Act
        var result = await service.CreateAsync(CreateInput(name, storageFolderPath));

        // Assert
        result.ValidationErrors.ShouldBe([ArchiveStorageFolderValidationError.NameRequired]);
        result.ArchiveStorageFolderId.ShouldBeNull();
        (await DbContext.ArchiveStorageFolders.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task CreateAsync_NameLongerThanMaximum_ReturnsNameTooLong()
    {
        // Act
        var result = await service.CreateAsync(
            CreateInput(new string('a', 101), storageFolderPath)
        );

        // Assert
        result.ValidationErrors.ShouldBe([ArchiveStorageFolderValidationError.NameTooLong]);
    }

    [Test]
    public async Task CreateAsync_NameOfExistingStorageFolder_ReturnsNameAlreadyExists()
    {
        // Arrange
        await service.CreateAsync(CreateInput("NAS archive", storageFolderPath));

        // Act
        var result = await service.CreateAsync(
            CreateInput(" NAS archive ", otherStorageFolderPath)
        );

        // Assert
        result.ValidationErrors.ShouldBe([ArchiveStorageFolderValidationError.NameAlreadyExists]);
        (await DbContext.ArchiveStorageFolders.CountAsync()).ShouldBe(1);
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task CreateAsync_BlankPath_ReturnsPathRequired(string path)
    {
        // Act
        var result = await service.CreateAsync(CreateInput("NAS archive", path));

        // Assert
        result.ValidationErrors.ShouldBe([ArchiveStorageFolderValidationError.PathRequired]);
    }

    [Test]
    public async Task CreateAsync_PathLongerThanMaximum_ReturnsPathTooLong()
    {
        // Act
        var result = await service.CreateAsync(
            CreateInput("NAS archive", Path.Combine(tempRootPath, new string('a', 500)))
        );

        // Assert
        result.ValidationErrors.ShouldBe([ArchiveStorageFolderValidationError.PathTooLong]);
    }

    [Test]
    public async Task CreateAsync_RelativePath_ReturnsPathNotAbsolute()
    {
        // Act
        var result = await service.CreateAsync(CreateInput("NAS archive", "archives/storage"));

        // Assert
        result.ValidationErrors.ShouldBe([ArchiveStorageFolderValidationError.PathNotAbsolute]);
    }

    [Test]
    public async Task CreateAsync_MissingPath_ReturnsPathNotFoundAndDoesNotCreateFolder()
    {
        // Arrange
        var missingPath = Path.Combine(tempRootPath, "missing");

        // Act
        var result = await service.CreateAsync(CreateInput("NAS archive", missingPath));

        // Assert
        result.ValidationErrors.ShouldBe([ArchiveStorageFolderValidationError.PathNotFound]);
        Directory.Exists(missingPath).ShouldBeFalse();
    }

    [Test]
    public async Task CreateAsync_PathOfExistingStorageFolder_ReturnsPathAlreadyExists()
    {
        // Arrange
        await service.CreateAsync(CreateInput("NAS archive", storageFolderPath));

        // Act
        var result = await service.CreateAsync(
            CreateInput("Second disk", storageFolderPath + Path.DirectorySeparatorChar)
        );

        // Assert
        result.ValidationErrors.ShouldBe([ArchiveStorageFolderValidationError.PathAlreadyExists]);
    }

    [TestCase("")]
    [TestCase("archives")]
    [TestCase("..")]
    public async Task CreateAsync_PathSameAsInsideOrParentOfWorkingDirectory_ReturnsPathOverlapsWorkingDirectory(
        string relativePathToWorkingDirectory
    )
    {
        // Arrange
        var path = Path.GetFullPath(
            Path.Combine(workingDirectoryPath, relativePathToWorkingDirectory)
        );
        Directory.CreateDirectory(path);

        // Act
        var result = await service.CreateAsync(CreateInput("NAS archive", path));

        // Assert
        result.ValidationErrors.ShouldBe([
            ArchiveStorageFolderValidationError.PathOverlapsWorkingDirectory,
        ]);
    }

    [Test]
    public async Task CreateAsync_PathSharingPrefixWithWorkingDirectory_PersistsStorageFolder()
    {
        // Arrange
        var path = Directory.CreateDirectory(workingDirectoryPath + "-archives").FullName;

        // Act
        var result = await service.CreateAsync(CreateInput("NAS archive", path));

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Test]
    public async Task CreateAsync_NegativeMinimumFreeSpace_ReturnsMinimumFreeSpaceNegative()
    {
        // Act
        var result = await service.CreateAsync(
            CreateInput("NAS archive", storageFolderPath) with
            {
                MinimumFreeSpaceGb = -1,
            }
        );

        // Assert
        result.ValidationErrors.ShouldBe([
            ArchiveStorageFolderValidationError.MinimumFreeSpaceNegative,
        ]);
    }

    [Test]
    public async Task CreateAsync_SeveralInvalidFields_ReturnsAllValidationErrors()
    {
        // Act
        var result = await service.CreateAsync(
            new ArchiveStorageFolderInput(
                "",
                "",
                MinimumFreeSpaceGb: -5,
                Priority: 0,
                UseLocalWorkingCopyForReuploads: false
            )
        );

        // Assert
        result.ValidationErrors.ShouldBe([
            ArchiveStorageFolderValidationError.NameRequired,
            ArchiveStorageFolderValidationError.PathRequired,
            ArchiveStorageFolderValidationError.MinimumFreeSpaceNegative,
        ]);
    }

    [Test]
    public async Task UpdateAsync_ValidInput_UpdatesFieldsAndKeepsActiveState()
    {
        // Arrange
        var created = await service.CreateAsync(CreateInput("NAS archive", storageFolderPath));
        await service.ToggleIsActiveAsync(created.ArchiveStorageFolderId!.Value);

        // Act
        var result = await service.UpdateAsync(
            created.ArchiveStorageFolderId.Value,
            new ArchiveStorageFolderInput(
                "Second disk",
                otherStorageFolderPath,
                MinimumFreeSpaceGb: 20,
                Priority: 5,
                UseLocalWorkingCopyForReuploads: true
            )
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var storageFolder = await LoadStorageFolderAsync();
        storageFolder.Name.ShouldBe("Second disk");
        storageFolder.Path.ShouldBe(otherStorageFolderPath);
        storageFolder.IsActive.ShouldBeFalse();
        storageFolder.MinimumFreeSpaceGb.ShouldBe(20);
        storageFolder.Priority.ShouldBe(5);
        storageFolder.UseLocalWorkingCopyForReuploads.ShouldBeTrue();
    }

    [Test]
    public async Task UpdateAsync_UnchangedNameAndPathOfSameStorageFolder_UpdatesStorageFolder()
    {
        // Arrange
        var created = await service.CreateAsync(CreateInput("NAS archive", storageFolderPath));

        // Act
        var result = await service.UpdateAsync(
            created.ArchiveStorageFolderId!.Value,
            CreateInput("NAS archive", storageFolderPath) with
            {
                Priority = 3,
            }
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await LoadStorageFolderAsync()).Priority.ShouldBe(3);
    }

    [Test]
    public async Task UpdateAsync_NameAndPathOfOtherStorageFolder_ReturnsErrorsAndKeepsStorageFolder()
    {
        // Arrange
        await service.CreateAsync(CreateInput("NAS archive", storageFolderPath));
        var other = await service.CreateAsync(CreateInput("Second disk", otherStorageFolderPath));

        // Act
        var result = await service.UpdateAsync(
            other.ArchiveStorageFolderId!.Value,
            CreateInput("NAS archive", storageFolderPath)
        );

        // Assert
        result.ValidationErrors.ShouldBe([
            ArchiveStorageFolderValidationError.NameAlreadyExists,
            ArchiveStorageFolderValidationError.PathAlreadyExists,
        ]);
        await using var readDbContext = Database.CreateDbContext();
        var storedOther = await readDbContext.ArchiveStorageFolders.SingleAsync(storageFolder =>
            storageFolder.Id == other.ArchiveStorageFolderId
        );
        storedOther.Name.ShouldBe("Second disk");
        storedOther.Path.ShouldBe(otherStorageFolderPath);
    }

    [Test]
    public async Task UpdateAsync_ChangedPathWithStoredArchives_ReturnsPathChangeWithStoredArchives()
    {
        // Arrange
        var created = await service.CreateAsync(CreateInput("NAS archive", storageFolderPath));
        await AddArchiveStoredInAsync(created.ArchiveStorageFolderId!.Value);

        // Act
        var result = await service.UpdateAsync(
            created.ArchiveStorageFolderId.Value,
            CreateInput("NAS archive", otherStorageFolderPath)
        );

        // Assert
        result.ValidationErrors.ShouldBe([
            ArchiveStorageFolderValidationError.PathChangeWithStoredArchives,
        ]);
        (await LoadStorageFolderAsync()).Path.ShouldBe(storageFolderPath);
    }

    [Test]
    public async Task UpdateAsync_UnchangedPathWithStoredArchives_UpdatesStorageFolder()
    {
        // Arrange
        var created = await service.CreateAsync(CreateInput("NAS archive", storageFolderPath));
        await AddArchiveStoredInAsync(created.ArchiveStorageFolderId!.Value);

        // Act
        var result = await service.UpdateAsync(
            created.ArchiveStorageFolderId.Value,
            CreateInput("Renamed NAS archive", storageFolderPath)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await LoadStorageFolderAsync()).Name.ShouldBe("Renamed NAS archive");
    }

    [Test]
    public async Task ToggleIsActiveAsync_CalledTwice_DeactivatesAndReactivatesStorageFolder()
    {
        // Arrange
        var created = await service.CreateAsync(CreateInput("NAS archive", storageFolderPath));

        // Act
        await service.ToggleIsActiveAsync(created.ArchiveStorageFolderId!.Value);
        var isActiveAfterFirstToggle = (await LoadStorageFolderAsync()).IsActive;
        await service.ToggleIsActiveAsync(created.ArchiveStorageFolderId.Value);

        // Assert
        isActiveAfterFirstToggle.ShouldBeFalse();
        (await LoadStorageFolderAsync()).IsActive.ShouldBeTrue();
    }

    [Test]
    public async Task DeleteAsync_StorageFolderWithoutStoredArchives_DeletesStorageFolder()
    {
        // Arrange
        var created = await service.CreateAsync(CreateInput("NAS archive", storageFolderPath));

        // Act
        var result = await service.DeleteAsync(created.ArchiveStorageFolderId!.Value);

        // Assert
        result.IsDeleted.ShouldBeTrue();
        result.StoredArchiveCount.ShouldBe(0);
        await using var readDbContext = Database.CreateDbContext();
        (await readDbContext.ArchiveStorageFolders.AnyAsync()).ShouldBeFalse();
        Directory.Exists(storageFolderPath).ShouldBeTrue();
    }

    [Test]
    public async Task DeleteAsync_StorageFolderWithStoredArchives_KeepsStorageFolderAndReturnsStoredArchiveCount()
    {
        // Arrange
        var created = await service.CreateAsync(CreateInput("NAS archive", storageFolderPath));
        await AddArchiveStoredInAsync(created.ArchiveStorageFolderId!.Value);
        await AddArchiveStoredInAsync(created.ArchiveStorageFolderId.Value);

        // Act
        var result = await service.DeleteAsync(created.ArchiveStorageFolderId.Value);

        // Assert
        result.IsDeleted.ShouldBeFalse();
        result.StoredArchiveCount.ShouldBe(2);
        await using var readDbContext = Database.CreateDbContext();
        (await readDbContext.ArchiveStorageFolders.CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task UpdateAsync_ChangedPathWithOnlyArchivesNotInCreatedState_UpdatesPath()
    {
        // Arrange
        var created = await service.CreateAsync(CreateInput("NAS archive", storageFolderPath));
        await AddArchiveStoredInAsync(
            created.ArchiveStorageFolderId!.Value,
            ArchiveState.MissingFiles
        );
        await AddArchiveStoredInAsync(created.ArchiveStorageFolderId.Value, ArchiveState.Deleted);

        // Act
        var result = await service.UpdateAsync(
            created.ArchiveStorageFolderId.Value,
            CreateInput("NAS archive", otherStorageFolderPath)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await LoadStorageFolderAsync()).Path.ShouldBe(otherStorageFolderPath);
    }

    [Test]
    public async Task DeleteAsync_StorageFolderReferencedOnlyByArchivesNotInCreatedState_ClearsReferencesAndStorageFolderHashesAndDeletesStorageFolder()
    {
        // Arrange
        var created = await service.CreateAsync(CreateInput("NAS archive", storageFolderPath));
        var missingFilesArchive = await AddArchiveStoredInAsync(
            created.ArchiveStorageFolderId!.Value,
            ArchiveState.MissingFiles
        );
        var deletedArchive = await AddArchiveStoredInAsync(
            created.ArchiveStorageFolderId.Value,
            ArchiveState.Deleted
        );

        // Act
        var result = await service.DeleteAsync(created.ArchiveStorageFolderId.Value);

        // Assert
        result.IsDeleted.ShouldBeTrue();
        result.StoredArchiveCount.ShouldBe(0);
        await using var readDbContext = Database.CreateDbContext();
        (await readDbContext.ArchiveStorageFolders.AnyAsync()).ShouldBeFalse();
        var archives = await readDbContext.Archives.OrderBy(archive => archive.Id).ToListAsync();
        archives
            .Select(archive => archive.Id)
            .ShouldBe([missingFilesArchive.Id, deletedArchive.Id]);
        archives.ShouldAllBe(archive => archive.ArchiveStorageFolderId == null);
        (await readDbContext.ArchiveFiles.ToListAsync()).ShouldAllBe(archiveFile =>
            archiveFile.Md5HashInStorageFolder == null
        );
    }

    [Test]
    public async Task DeleteAsync_StorageFolderWithCreatedAndNotCreatedArchives_KeepsStorageFolderAndReferences()
    {
        // Arrange
        var created = await service.CreateAsync(CreateInput("NAS archive", storageFolderPath));
        await AddArchiveStoredInAsync(created.ArchiveStorageFolderId!.Value);
        await AddArchiveStoredInAsync(created.ArchiveStorageFolderId.Value, ArchiveState.Deleted);

        // Act
        var result = await service.DeleteAsync(created.ArchiveStorageFolderId.Value);

        // Assert
        result.IsDeleted.ShouldBeFalse();
        result.StoredArchiveCount.ShouldBe(1);
        await using var readDbContext = Database.CreateDbContext();
        (await readDbContext.ArchiveStorageFolders.CountAsync()).ShouldBe(1);
        (
            await readDbContext.Archives.CountAsync(archive =>
                archive.ArchiveStorageFolderId == created.ArchiveStorageFolderId.Value
            )
        ).ShouldBe(2);
    }

    [Test]
    public async Task GetAllWithAvailableFreeSpaceAsync_ExistingAndMissingPath_ReturnsFreeSpaceOnlyForExistingPath()
    {
        // Arrange
        await service.CreateAsync(CreateInput("NAS archive", storageFolderPath));
        await service.CreateAsync(CreateInput("Second disk", otherStorageFolderPath));
        Directory.Delete(otherStorageFolderPath);

        // Act
        var result = await service.GetAllWithAvailableFreeSpaceAsync();

        // Assert
        result.Count.ShouldBe(2);
        var existing = result.Single(item => item.StorageFolder.Name == "NAS archive");
        existing.AvailableFreeSpaceBytes.ShouldNotBeNull();
        existing.AvailableFreeSpaceBytes.Value.ShouldBeGreaterThan(0);
        result
            .Single(item => item.StorageFolder.Name == "Second disk")
            .AvailableFreeSpaceBytes.ShouldBeNull();
    }

    private static ArchiveStorageFolderInput CreateInput(string name, string path)
    {
        return new ArchiveStorageFolderInput(
            name,
            path,
            MinimumFreeSpaceGb: 10,
            Priority: 1,
            UseLocalWorkingCopyForReuploads: false
        );
    }

    private async Task<ArchiveStorageFolder> LoadStorageFolderAsync()
    {
        await using var readDbContext = Database.CreateDbContext();

        return await readDbContext.ArchiveStorageFolders.SingleAsync();
    }

    private async Task<Archive> AddArchiveStoredInAsync(
        int archiveStorageFolderId,
        ArchiveState archiveState = ArchiveState.Created
    )
    {
        await using var setupDbContext = Database.CreateDbContext();
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
            ArchiveFolderPath = Path.Combine(storageFolderPath, "Bearcat.Release"),
            ArchiveStorageFolderId = archiveStorageFolderId,
            CreatedAt = DateTime.UtcNow,
            ArchiveState = archiveState,
            ArchiveFileSizeMb = 1024,
            ArchiveFiles =
            [
                new ArchiveFile
                {
                    FullFileName = Path.Combine(
                        storageFolderPath,
                        "Bearcat.Release",
                        "release.rar"
                    ),
                    Md5Hash = "11111111111111111111111111111111",
                    Md5HashInStorageFolder = "11111111111111111111111111111111",
                },
            ],
        };
        setupDbContext.Archives.Add(archive);
        await setupDbContext.SaveChangesAsync();

        return archive;
    }
}

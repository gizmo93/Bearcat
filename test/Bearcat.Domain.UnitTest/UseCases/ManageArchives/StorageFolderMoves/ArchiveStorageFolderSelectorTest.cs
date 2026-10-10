using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageArchives.StorageFolderMoves;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ManageArchives.StorageFolderMoves;

public class ArchiveStorageFolderSelectorTest
{
    private const long Gigabyte = ArchiveStorageFolderSelector.BytesPerGigabyte;

    [Test]
    public void SelectStorageFolder_LowerPriorityHasEnoughSpace_SelectsLowerPriority()
    {
        // Arrange
        var preferredFolder = CreateFolder(1, priority: 1);
        var fallbackFolder = CreateFolder(2, priority: 2);

        // Act
        var result = ArchiveStorageFolderSelector.SelectStorageFolder(
            [
                WithFreeSpace(fallbackFolder, 100 * Gigabyte),
                WithFreeSpace(preferredFolder, 10 * Gigabyte),
            ],
            archiveSizeBytes: 5 * Gigabyte
        );

        // Assert
        result.ShouldBe(preferredFolder);
    }

    [Test]
    public void SelectStorageFolder_LowerPriorityIsTooSmall_SelectsNextPriority()
    {
        // Arrange
        var preferredFolder = CreateFolder(1, priority: 1);
        var fallbackFolder = CreateFolder(2, priority: 2);

        // Act
        var result = ArchiveStorageFolderSelector.SelectStorageFolder(
            [
                WithFreeSpace(preferredFolder, 4 * Gigabyte),
                WithFreeSpace(fallbackFolder, 10 * Gigabyte),
            ],
            archiveSizeBytes: 5 * Gigabyte
        );

        // Assert
        result.ShouldBe(fallbackFolder);
    }

    [Test]
    public void SelectStorageFolder_SamePriority_SelectsFolderWithMostFreeSpace()
    {
        // Arrange
        var smallFolder = CreateFolder(1, priority: 1);
        var largeFolder = CreateFolder(2, priority: 1);

        // Act
        var result = ArchiveStorageFolderSelector.SelectStorageFolder(
            [WithFreeSpace(smallFolder, 20 * Gigabyte), WithFreeSpace(largeFolder, 30 * Gigabyte)],
            archiveSizeBytes: 5 * Gigabyte
        );

        // Assert
        result.ShouldBe(largeFolder);
    }

    [Test]
    public void SelectStorageFolder_FreeSpaceEqualsArchiveSizePlusReserve_SelectsFolder()
    {
        // Arrange
        var folder = CreateFolder(1, priority: 1, minimumFreeSpaceGb: 10);

        // Act
        var result = ArchiveStorageFolderSelector.SelectStorageFolder(
            [WithFreeSpace(folder, 15 * Gigabyte)],
            archiveSizeBytes: 5 * Gigabyte
        );

        // Assert
        result.ShouldBe(folder);
    }

    [Test]
    public void SelectStorageFolder_FreeSpaceBelowArchiveSizePlusReserve_ReturnsNull()
    {
        // Arrange
        var folder = CreateFolder(1, priority: 1, minimumFreeSpaceGb: 10);

        // Act
        var result = ArchiveStorageFolderSelector.SelectStorageFolder(
            [WithFreeSpace(folder, 15 * Gigabyte - 1)],
            archiveSizeBytes: 5 * Gigabyte
        );

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public void SelectStorageFolder_InactiveOrMissingFolders_AreSkipped()
    {
        // Arrange
        var inactiveFolder = CreateFolder(1, priority: 1, isActive: false);
        var missingFolder = CreateFolder(2, priority: 1);
        var availableFolder = CreateFolder(3, priority: 2);

        // Act
        var result = ArchiveStorageFolderSelector.SelectStorageFolder(
            [
                WithFreeSpace(inactiveFolder, 100 * Gigabyte),
                WithFreeSpace(missingFolder, null),
                WithFreeSpace(availableFolder, 10 * Gigabyte),
            ],
            archiveSizeBytes: 5 * Gigabyte
        );

        // Assert
        result.ShouldBe(availableFolder);
    }

    [Test]
    public void SelectStorageFolder_NoFolders_ReturnsNull()
    {
        // Act
        var result = ArchiveStorageFolderSelector.SelectStorageFolder([], archiveSizeBytes: 1);

        // Assert
        result.ShouldBeNull();
    }

    private static ArchiveStorageFolderWithAvailableFreeSpace WithFreeSpace(
        ArchiveStorageFolder storageFolder,
        long? availableFreeSpaceBytes
    )
    {
        return new ArchiveStorageFolderWithAvailableFreeSpace(
            storageFolder,
            availableFreeSpaceBytes
        );
    }

    private static ArchiveStorageFolder CreateFolder(
        int id,
        int priority,
        int minimumFreeSpaceGb = 0,
        bool isActive = true
    )
    {
        return new ArchiveStorageFolder
        {
            Id = id,
            Name = $"Folder {id}",
            Path = $"/mnt/folder{id}",
            IsActive = isActive,
            MinimumFreeSpaceGb = minimumFreeSpaceGb,
            Priority = priority,
        };
    }
}

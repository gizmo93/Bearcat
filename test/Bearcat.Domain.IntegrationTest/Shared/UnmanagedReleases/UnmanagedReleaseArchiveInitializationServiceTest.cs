using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.UnmanagedReleases;
using Bearcat.Domain.ValueObjects;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.Shared.UnmanagedReleases;

public class UnmanagedReleaseArchiveInitializationServiceTest
{
    private static readonly DateTime CreatedAt = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Local);

    private UnmanagedReleaseArchiveInitializationService initializationService = null!;
    private string tempRootPath = null!;

    [SetUp]
    public void Setup()
    {
        initializationService = new UnmanagedReleaseArchiveInitializationService(
            new RealArchiverFactory()
        );
        tempRootPath = Path.Combine(
            Path.GetTempPath(),
            $"bearcat-unmanaged-archive-initializer-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(tempRootPath);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(tempRootPath, recursive: true);
    }

    [Test]
    public void CreateArchiveConfig_OldRarVolumeNaming_ImportsAllVolumes()
    {
        // Arrange
        var archiveFolderPath = CreateFolderWithFiles(
            "release",
            "release.rar",
            "release.r00",
            "release.r01",
            "release.s00",
            "release.nfo",
            "release.sfv"
        );

        // Act
        var archiveConfig = initializationService.CreateArchiveConfig(
            CreateUnmanagedRelease(),
            archiveFolderPath,
            CreatedAt
        );

        // Assert
        archiveConfig.ArchiverName.ShouldBe("RarArchiver");
        archiveConfig.Name.ShouldBe("RAR");
        archiveConfig
            .Archives.Single()
            .ArchiveFiles.Select(file => file.FullFileName)
            .ShouldBe([
                Path.Combine(archiveFolderPath, "release.r00"),
                Path.Combine(archiveFolderPath, "release.r01"),
                Path.Combine(archiveFolderPath, "release.rar"),
                Path.Combine(archiveFolderPath, "release.s00"),
            ]);
    }

    [Test]
    public void CreateArchiveConfig_NewRarVolumeNaming_ImportsAllVolumes()
    {
        // Arrange
        var archiveFolderPath = CreateFolderWithFiles(
            "release",
            "release.part1.rar",
            "release.part2.rar",
            "release.nfo"
        );

        // Act
        var archiveConfig = initializationService.CreateArchiveConfig(
            CreateUnmanagedRelease(),
            archiveFolderPath,
            CreatedAt
        );

        // Assert
        archiveConfig
            .Archives.Single()
            .ArchiveFiles.Select(file => file.FullFileName)
            .ShouldBe([
                Path.Combine(archiveFolderPath, "release.part1.rar"),
                Path.Combine(archiveFolderPath, "release.part2.rar"),
            ]);
    }

    [Test]
    public void CreateArchiveConfig_SevenZipVolumes_ImportsAllVolumes()
    {
        // Arrange
        var archiveFolderPath = CreateFolderWithFiles(
            "release",
            "release.7z.001",
            "release.7z.002",
            "release.nfo"
        );

        // Act
        var archiveConfig = initializationService.CreateArchiveConfig(
            CreateUnmanagedRelease(),
            archiveFolderPath,
            CreatedAt
        );

        // Assert
        archiveConfig.ArchiverName.ShouldBe("SevenZipArchiver");
        archiveConfig
            .Archives.Single()
            .ArchiveFiles.Select(file => file.FullFileName)
            .ShouldBe([
                Path.Combine(archiveFolderPath, "release.7z.001"),
                Path.Combine(archiveFolderPath, "release.7z.002"),
            ]);
    }

    [Test]
    public void CreateArchiveConfig_ArchiveFilesInSubfolder_ThrowsInvalidOperationException()
    {
        // Arrange
        var archiveFolderPath = CreateFolderWithFiles("release", "release.nfo");
        CreateFolderWithFiles(Path.Combine("release", "Subs"), "subs.rar", "subs.r00");

        // Act
        var exception = Should.Throw<InvalidOperationException>(() =>
            initializationService.CreateArchiveConfig(
                CreateUnmanagedRelease(),
                archiveFolderPath,
                CreatedAt
            )
        );

        // Assert
        exception.Message.ShouldBe(
            $"Archive folder path {archiveFolderPath} does not contain supported archive files."
        );
    }

    [Test]
    public void CreateArchiveConfig_RarAndSevenZipArchives_ThrowsInvalidOperationException()
    {
        // Arrange
        var archiveFolderPath = CreateFolderWithFiles(
            "release",
            "release.rar",
            "release.r00",
            "extras.7z"
        );

        // Act
        var exception = Should.Throw<InvalidOperationException>(() =>
            initializationService.CreateArchiveConfig(
                CreateUnmanagedRelease(),
                archiveFolderPath,
                CreatedAt
            )
        );

        // Assert
        exception.Message.ShouldBe(
            $"Archive folder path {archiveFolderPath} contains archive files for multiple archivers."
        );
    }

    [Test]
    public void CreateArchiveConfig_OldRarContinuationVolumesWithoutFirstVolume_ThrowsInvalidOperationException()
    {
        // Arrange
        var archiveFolderPath = CreateFolderWithFiles("release", "release.r00", "release.r01");

        // Act
        var exception = Should.Throw<InvalidOperationException>(() =>
            initializationService.CreateArchiveConfig(
                CreateUnmanagedRelease(),
                archiveFolderPath,
                CreatedAt
            )
        );

        // Assert
        exception.Message.ShouldContain("first volume is missing");
    }

    [Test]
    public void ApplyArchiveFolder_OldRarVolumesMovedToOtherFolder_RelocatesArchive()
    {
        // Arrange
        var currentFolderPath = CreateFolderWithFiles(
            "current",
            "release.rar",
            "release.r00",
            "release.r01"
        );
        var archiveConfig = initializationService.CreateArchiveConfig(
            CreateUnmanagedRelease(),
            currentFolderPath,
            CreatedAt
        );
        var targetFolderPath = CreateFolderWithFiles(
            "target",
            "release.rar",
            "release.r00",
            "release.r01"
        );

        // Act
        var result = initializationService.ApplyArchiveFolder(
            archiveConfig,
            targetFolderPath,
            CreatedAt.AddHours(1),
            confirmContentChange: false,
            archiveStorageFolders: []
        );

        // Assert
        result.ShouldBe(ArchiveFolderChangeResult.Relocated);
        archiveConfig.ArchiveFilesBasePath.ShouldBe(targetFolderPath);
        var archive = archiveConfig.Archives.Single();
        archive.ArchiveFolderPath.ShouldBe(targetFolderPath);
        archive
            .ArchiveFiles.Select(file => file.FullFileName)
            .ShouldBe([
                Path.Combine(targetFolderPath, "release.r00"),
                Path.Combine(targetFolderPath, "release.r01"),
                Path.Combine(targetFolderPath, "release.rar"),
            ]);
    }

    [Test]
    public void ApplyArchiveFolder_ArchiveMissesOldRarContinuationVolumes_RequiresConfirmationThenReimports()
    {
        // Arrange
        var archiveFolderPath = CreateFolderWithFiles(
            "release",
            "release.rar",
            "release.r00",
            "release.r01"
        );
        var archiveConfig = CreateArchiveConfigWithArchiveFiles(archiveFolderPath, "release.rar");

        // Act
        var unconfirmedResult = initializationService.ApplyArchiveFolder(
            archiveConfig,
            archiveFolderPath,
            CreatedAt.AddHours(1),
            confirmContentChange: false,
            archiveStorageFolders: []
        );
        var confirmedResult = initializationService.ApplyArchiveFolder(
            archiveConfig,
            archiveFolderPath,
            CreatedAt.AddHours(1),
            confirmContentChange: true,
            archiveStorageFolders: []
        );

        // Assert
        unconfirmedResult.ShouldBe(ArchiveFolderChangeResult.ConfirmationRequired);
        confirmedResult.ShouldBe(ArchiveFolderChangeResult.Reimported);
        archiveConfig.Archives.Count.ShouldBe(2);
        archiveConfig.Archives[0].ArchiveState.ShouldBe(ArchiveState.Deleted);
        archiveConfig.Archives[1].ArchiveState.ShouldBe(ArchiveState.Created);
        archiveConfig
            .Archives[1]
            .ArchiveFiles.Select(file => file.FullFileName)
            .ShouldBe([
                Path.Combine(archiveFolderPath, "release.r00"),
                Path.Combine(archiveFolderPath, "release.r01"),
                Path.Combine(archiveFolderPath, "release.rar"),
            ]);
    }

    [Test]
    public void ApplyArchiveFolder_TargetContainsOnlySevenZipArchive_ThrowsInvalidOperationException()
    {
        // Arrange
        var currentFolderPath = CreateFolderWithFiles("current", "release.rar", "release.r00");
        var archiveConfig = initializationService.CreateArchiveConfig(
            CreateUnmanagedRelease(),
            currentFolderPath,
            CreatedAt
        );
        var targetFolderPath = CreateFolderWithFiles("target", "release.7z");

        // Act
        var exception = Should.Throw<InvalidOperationException>(() =>
            initializationService.ApplyArchiveFolder(
                archiveConfig,
                targetFolderPath,
                CreatedAt.AddHours(1),
                confirmContentChange: true,
                archiveStorageFolders: []
            )
        );

        // Assert
        exception.Message.ShouldBe(
            $"Archive folder path {targetFolderPath} does not contain archive files for archiver RAR."
        );
    }

    [Test]
    public void ApplyArchiveFolder_ArchiveRelocatedIntoStorageFolder_AssignsStorageFolderAndStorageFolderHashes()
    {
        // Arrange
        var currentFolderPath = CreateFolderWithFiles("current", "release.rar", "release.r00");
        var archiveConfig = initializationService.CreateArchiveConfig(
            CreateUnmanagedRelease(),
            currentFolderPath,
            CreatedAt
        );
        var storageFolder = CreateStorageFolder(7, Path.Combine(tempRootPath, "storage"));
        var nestedStorageFolder = CreateStorageFolder(
            8,
            Path.Combine(tempRootPath, "storage", "nested")
        );
        var targetFolderPath = CreateFolderWithFiles(
            Path.Combine("storage", "nested", "release"),
            "release.rar",
            "release.r00"
        );
        foreach (var archiveFile in archiveConfig.Archives.Single().ArchiveFiles)
        {
            archiveFile.Md5Hash = "11111111111111111111111111111111";
        }

        // Act
        var result = initializationService.ApplyArchiveFolder(
            archiveConfig,
            targetFolderPath,
            CreatedAt.AddHours(1),
            confirmContentChange: false,
            archiveStorageFolders: [storageFolder, nestedStorageFolder]
        );

        // Assert
        result.ShouldBe(ArchiveFolderChangeResult.Relocated);
        var archive = archiveConfig.Archives.Single();
        archive.ArchiveStorageFolderId.ShouldBe(nestedStorageFolder.Id);
        archive.ArchiveStorageFolder.ShouldBe(nestedStorageFolder);
        archive.ArchiveFiles.ShouldAllBe(archiveFile =>
            archiveFile.Md5HashInStorageFolder == "11111111111111111111111111111111"
        );
    }

    [Test]
    public void ApplyArchiveFolder_ArchiveRelocatedOutOfStorageFolder_ClearsStorageFolderAndStorageFolderHashes()
    {
        // Arrange
        var storageFolder = CreateStorageFolder(7, Path.Combine(tempRootPath, "storage"));
        var currentFolderPath = CreateFolderWithFiles(
            Path.Combine("storage", "release"),
            "release.rar",
            "release.r00"
        );
        var archiveConfig = CreateArchiveConfigWithArchiveFiles(
            currentFolderPath,
            "release.r00",
            "release.rar"
        );
        var archive = archiveConfig.Archives.Single();
        archive.ArchiveStorageFolder = storageFolder;
        archive.ArchiveStorageFolderId = storageFolder.Id;
        foreach (var archiveFile in archive.ArchiveFiles)
        {
            archiveFile.Md5Hash = "11111111111111111111111111111111";
            archiveFile.Md5HashInStorageFolder = "11111111111111111111111111111111";
        }
        var targetFolderPath = CreateFolderWithFiles("target", "release.rar", "release.r00");

        // Act
        var result = initializationService.ApplyArchiveFolder(
            archiveConfig,
            targetFolderPath,
            CreatedAt.AddHours(1),
            confirmContentChange: false,
            archiveStorageFolders: [storageFolder]
        );

        // Assert
        result.ShouldBe(ArchiveFolderChangeResult.Relocated);
        archive.ArchiveFolderPath.ShouldBe(targetFolderPath);
        archive.ArchiveStorageFolderId.ShouldBeNull();
        archive.ArchiveStorageFolder.ShouldBeNull();
        archive.ArchiveFiles.ShouldAllBe(archiveFile => archiveFile.Md5HashInStorageFolder == null);
        archive.ArchiveFiles.ShouldAllBe(archiveFile =>
            archiveFile.Md5Hash == "11111111111111111111111111111111"
        );
    }

    [Test]
    public void ApplyArchiveFolder_ArchiveWithLocalWorkingCopyFileRelocated_ResetsHashOfWorkingCopyFileToStorageFolderHash()
    {
        // Arrange
        var storageFolder = CreateStorageFolder(7, Path.Combine(tempRootPath, "storage"));
        var currentFolderPath = CreateFolderWithFiles(
            Path.Combine("storage", "release"),
            "release.rar",
            "release.r00"
        );
        var archiveConfig = CreateArchiveConfigWithArchiveFiles(
            currentFolderPath,
            "release.r00",
            "release.rar"
        );
        var archive = archiveConfig.Archives.Single();
        archive.ArchiveStorageFolder = storageFolder;
        archive.ArchiveStorageFolderId = storageFolder.Id;
        var workingCopyFile = archive.ArchiveFiles[0];
        workingCopyFile.FullFileName = Path.Combine(
            tempRootPath,
            "archives",
            "release",
            "release.r00"
        );
        workingCopyFile.Md5Hash = "22222222222222222222222222222222";
        workingCopyFile.Md5HashInStorageFolder = "11111111111111111111111111111111";
        var targetFolderPath = CreateFolderWithFiles(
            Path.Combine("storage", "moved"),
            "release.rar",
            "release.r00"
        );

        // Act
        var result = initializationService.ApplyArchiveFolder(
            archiveConfig,
            targetFolderPath,
            CreatedAt.AddHours(1),
            confirmContentChange: false,
            archiveStorageFolders: [storageFolder]
        );

        // Assert
        result.ShouldBe(ArchiveFolderChangeResult.Relocated);
        workingCopyFile.FullFileName.ShouldBe(Path.Combine(targetFolderPath, "release.r00"));
        workingCopyFile.Md5Hash.ShouldBe("11111111111111111111111111111111");
        workingCopyFile.Md5HashInStorageFolder.ShouldBe("11111111111111111111111111111111");
    }

    [Test]
    public void ApplyArchiveFolder_ReimportFromStorageFolder_AssignsStorageFolderToNewArchiveOnly()
    {
        // Arrange
        var storageFolder = CreateStorageFolder(7, Path.Combine(tempRootPath, "storage"));
        var currentFolderPath = CreateFolderWithFiles("current", "release.rar");
        var archiveConfig = CreateArchiveConfigWithArchiveFiles(currentFolderPath, "release.rar");
        var targetFolderPath = CreateFolderWithFiles(
            Path.Combine("storage", "release"),
            "other.rar"
        );

        // Act
        var result = initializationService.ApplyArchiveFolder(
            archiveConfig,
            targetFolderPath,
            CreatedAt.AddHours(1),
            confirmContentChange: true,
            archiveStorageFolders: [storageFolder]
        );

        // Assert
        result.ShouldBe(ArchiveFolderChangeResult.Reimported);
        archiveConfig.Archives[0].ArchiveState.ShouldBe(ArchiveState.Deleted);
        archiveConfig.Archives[0].ArchiveStorageFolderId.ShouldBeNull();
        archiveConfig.Archives[1].ArchiveStorageFolderId.ShouldBe(storageFolder.Id);
        archiveConfig.Archives[1].ArchiveStorageFolder.ShouldBe(storageFolder);
    }

    private static ArchiveStorageFolder CreateStorageFolder(int id, string path)
    {
        return new ArchiveStorageFolder
        {
            Id = id,
            Name = $"Storage {id}",
            Path = path,
            IsActive = true,
        };
    }

    private static Release CreateUnmanagedRelease()
    {
        return new Release
        {
            Name = "Bearcat.Release.Unmanaged",
            ReleaseType = ReleaseType.Unmanaged,
            ReleaseFolderPath = null,
            ArchiveConfigs = [],
            UploadConfigs = [],
            ImageUploadConfigs = [],
        };
    }

    private static ArchiveConfig CreateArchiveConfigWithArchiveFiles(
        string archiveFolderPath,
        params string[] archiveFileNames
    )
    {
        return new ArchiveConfig
        {
            Release = CreateUnmanagedRelease(),
            Name = "RAR",
            ArchiveFilesBasePath = archiveFolderPath,
            ArchiverName = "RarArchiver",
            UploadConfigs = [],
            Archives =
            [
                new Archive
                {
                    ArchiveFolderPath = archiveFolderPath,
                    CreatedAt = CreatedAt,
                    ArchiveState = ArchiveState.Created,
                    ArchiveFiles = archiveFileNames
                        .Select(fileName => new ArchiveFile
                        {
                            FullFileName = Path.Combine(archiveFolderPath, fileName),
                        })
                        .ToList(),
                    Uploads = [],
                    ErrorMessages = [],
                    Notifications = [],
                },
            ],
        };
    }

    private string CreateFolderWithFiles(string relativeFolderPath, params string[] fileNames)
    {
        var folderPath = Directory
            .CreateDirectory(Path.Combine(tempRootPath, relativeFolderPath))
            .FullName;

        foreach (var fileName in fileNames)
        {
            File.WriteAllText(Path.Combine(folderPath, fileName), fileName);
        }

        return folderPath;
    }
}

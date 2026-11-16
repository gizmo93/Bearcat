using Bearcat.Abstractions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageArchives.ArchiveFolderRelocations;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ManageArchives.ArchiveFolderRelocations;

public class ArchiveFolderRelocationServiceTest
{
    private const string BasePath = "/data/found";
    private const string ImportBasePath = "/data/import";

    private Mock<IArchiveFolderRelocationRepository> repositoryMock = null!;
    private Mock<IFileSystemService> fileSystemServiceMock = null!;
    private ArchiveFolderRelocationService service = null!;

    [SetUp]
    public void Setup()
    {
        repositoryMock = new Mock<IArchiveFolderRelocationRepository>();

        repositoryMock
            .Setup(r =>
                r.GetArchiveIdsThatAreNewestOfTheirArchiveConfigAsync(
                    It.IsAny<IReadOnlyList<int>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((IReadOnlyList<int> archiveIds, CancellationToken _) => [.. archiveIds]);

        repositoryMock
            .Setup(r =>
                r.GetFolderPathsUsedByCreatedArchivesAsync(
                    It.IsAny<IReadOnlyList<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);

        repositoryMock
            .Setup(r => r.GetArchiveStorageFoldersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        fileSystemServiceMock = new Mock<IFileSystemService>();
        fileSystemServiceMock.Setup(f => f.DirectoryExists(BasePath)).Returns(true);

        service = new ArchiveFolderRelocationService(
            repositoryMock.Object,
            fileSystemServiceMock.Object,
            Mock.Of<ILogger<ArchiveFolderRelocationService>>()
        );
    }

    [Test]
    public async Task GetRelocationPreviewAsync_ArchivePartsInNestedFolderWithOtherFiles_FindsFolder()
    {
        // Arrange
        SetupArchives(CreateArchive(1, "Release.A"));

        SetupFiles(
            $"{BasePath}/Movies/2026/Release.A/release.a.PART1.rar",
            $"{BasePath}/Movies/2026/Release.A/Release.A.part2.rar",
            $"{BasePath}/Movies/2026/Release.A/Release.A.nfo",
            $"{BasePath}/Movies/2026/Release.A/Release.A.sfv"
        );

        // Act
        var result = await service.GetRelocationPreviewAsync([1], BasePath, CancellationToken.None);

        // Assert
        var previewItem = result.ShouldHaveSingleItem();
        previewItem.IsRelocatable.ShouldBeTrue();
        previewItem.FoundArchiveFolderPath.ShouldBe($"{BasePath}/Movies/2026/Release.A");
        previewItem.ReleaseName.ShouldBe("Release.A");
        previewItem.CurrentArchiveFolderPath.ShouldBe(ImportBasePath);
    }

    [TestCase("Release.A.part1.rar")]
    [TestCase("Release.A.part1.rar", "Release.A.part2.rar", "Release.A.part3.rar")]
    public async Task GetRelocationPreviewAsync_FolderWithMissingOrExtraPart_FindsNoMatchingFolder(
        params string[] fileNamesInFolder
    )
    {
        // Arrange
        SetupArchives(CreateArchive(1, "Release.A"));

        SetupFiles(
            fileNamesInFolder.Select(fileName => $"{BasePath}/Release.A/{fileName}").ToArray()
        );

        // Act
        var result = await service.GetRelocationPreviewAsync([1], BasePath, CancellationToken.None);

        // Assert
        var previewItem = result.ShouldHaveSingleItem();

        previewItem.NotRelocatableReason.ShouldBe(
            ArchiveFolderNotRelocatableReason.NoMatchingFolderFound
        );

        previewItem.FoundArchiveFolderPath.ShouldBeNull();
    }

    [Test]
    public async Task GetRelocationPreviewAsync_MatchingFolderContainsPartsOfOtherArchive_IsNotRelocatable()
    {
        // Arrange
        SetupArchives(CreateArchive(1, "Release.A"));

        SetupFiles(
            $"{BasePath}/Mixed/Release.A.part1.rar",
            $"{BasePath}/Mixed/Release.A.part2.rar",
            $"{BasePath}/Mixed/Release.B.7z.001"
        );

        // Act
        var result = await service.GetRelocationPreviewAsync([1], BasePath, CancellationToken.None);

        // Assert
        var previewItem = result.ShouldHaveSingleItem();

        previewItem.NotRelocatableReason.ShouldBe(
            ArchiveFolderNotRelocatableReason.MatchingFolderContainsOtherArchive
        );

        previewItem.FoundArchiveFolderPath.ShouldBe($"{BasePath}/Mixed");
    }

    [Test]
    public async Task GetRelocationPreviewAsync_MatchingFolderInsideReleaseFolderOfManagedRelease_IsNotRelocatable()
    {
        // Arrange
        var archive = CreateArchive(1, "Release.A", releaseType: ReleaseType.Managed);
        archive.ArchiveConfig.Release.ReleaseFolderPath = $"{BasePath}/Release.A";
        SetupArchives(archive);

        SetupFiles(
            $"{BasePath}/Release.A/Archives/Release.A.part1.rar",
            $"{BasePath}/Release.A/Archives/Release.A.part2.rar"
        );

        // Act
        var result = await service.GetRelocationPreviewAsync([1], BasePath, CancellationToken.None);

        // Assert
        result
            .ShouldHaveSingleItem()
            .NotRelocatableReason.ShouldBe(
                ArchiveFolderNotRelocatableReason.MatchingFolderInsideReleaseFolderOfManagedRelease
            );
    }

    [Test]
    public async Task GetRelocationPreviewAsync_SeveralFoldersMatch_IsNotRelocatable()
    {
        // Arrange
        SetupArchives(CreateArchive(1, "Release.A"));

        SetupFiles(
            $"{BasePath}/First/Release.A.part1.rar",
            $"{BasePath}/First/Release.A.part2.rar",
            $"{BasePath}/Second/Release.A.part1.rar",
            $"{BasePath}/Second/Release.A.part2.rar"
        );

        // Act
        var result = await service.GetRelocationPreviewAsync([1], BasePath, CancellationToken.None);

        // Assert
        result
            .ShouldHaveSingleItem()
            .NotRelocatableReason.ShouldBe(
                ArchiveFolderNotRelocatableReason.SeveralMatchingFoldersFound
            );
    }

    [Test]
    public async Task GetRelocationPreviewAsync_TwoSelectedArchivesMatchSameFolder_RejectsBoth()
    {
        // Arrange
        SetupArchives(CreateArchive(1, "Release.A"), CreateArchive(2, "Release.A"));

        SetupFiles(
            $"{BasePath}/Release.A/Release.A.part1.rar",
            $"{BasePath}/Release.A/Release.A.part2.rar"
        );

        // Act
        var result = await service.GetRelocationPreviewAsync(
            [1, 2],
            BasePath,
            CancellationToken.None
        );

        // Assert
        result.Count.ShouldBe(2);

        result.ShouldAllBe(previewItem =>
            previewItem.NotRelocatableReason
            == ArchiveFolderNotRelocatableReason.MatchingFolderFoundForSeveralSelectedArchives
        );
    }

    [Test]
    public async Task GetRelocationPreviewAsync_MatchingFolderUsedByCreatedArchive_IsNotRelocatable()
    {
        // Arrange
        SetupArchives(CreateArchive(1, "Release.A"));

        SetupFiles(
            $"{BasePath}/Release.A/Release.A.part1.rar",
            $"{BasePath}/Release.A/Release.A.part2.rar"
        );

        repositoryMock
            .Setup(r =>
                r.GetFolderPathsUsedByCreatedArchivesAsync(
                    It.Is<IReadOnlyList<string>>(folderPaths =>
                        folderPaths.SequenceEqual(new[] { $"{BasePath}/Release.A" })
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([$"{BasePath}/Release.A"]);

        // Act
        var result = await service.GetRelocationPreviewAsync([1], BasePath, CancellationToken.None);

        // Assert
        result
            .ShouldHaveSingleItem()
            .NotRelocatableReason.ShouldBe(
                ArchiveFolderNotRelocatableReason.MatchingFolderUsedByCreatedArchive
            );
    }

    [TestCase(
        ArchiveState.Created,
        true,
        ArchiveFolderNotRelocatableReason.ArchiveIsNotDeletedOrMissingFiles
    )]
    [TestCase(
        ArchiveState.Restoring,
        true,
        ArchiveFolderNotRelocatableReason.ArchiveIsNotDeletedOrMissingFiles
    )]
    [TestCase(
        ArchiveState.MissingFiles,
        false,
        ArchiveFolderNotRelocatableReason.NewerArchiveExistsInArchiveConfig
    )]
    public async Task GetRelocationPreviewAsync_IneligibleArchive_IsNotRelocatable(
        ArchiveState archiveState,
        bool isNewestArchiveOfArchiveConfig,
        ArchiveFolderNotRelocatableReason expectedReason
    )
    {
        // Arrange
        SetupArchives(CreateArchive(1, "Release.A", archiveState));

        SetupFiles(
            $"{BasePath}/Release.A/Release.A.part1.rar",
            $"{BasePath}/Release.A/Release.A.part2.rar"
        );

        repositoryMock
            .Setup(r =>
                r.GetArchiveIdsThatAreNewestOfTheirArchiveConfigAsync(
                    It.IsAny<IReadOnlyList<int>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(isNewestArchiveOfArchiveConfig ? [1] : []);

        // Act
        var result = await service.GetRelocationPreviewAsync([1], BasePath, CancellationToken.None);

        // Assert
        result.ShouldHaveSingleItem().NotRelocatableReason.ShouldBe(expectedReason);
    }

    [TestCase(ReleaseType.Unmanaged, $"{BasePath}/Release.A")]
    [TestCase(ReleaseType.Managed, ImportBasePath)]
    public async Task RelocateAsync_RelocatableArchive_RepointsFilesAndMarksArchiveCreated(
        ReleaseType releaseType,
        string expectedArchiveFilesBasePath
    )
    {
        // Arrange
        var archive = CreateArchive(1, "Release.A", releaseType: releaseType);
        var waitingUpload = new Upload { UploadState = UploadState.WaitingForArchive };
        archive.Uploads.Add(waitingUpload);

        var storageFolder = new ArchiveStorageFolder
        {
            Id = 3,
            Name = "NAS",
            Path = BasePath,
        };

        repositoryMock
            .Setup(r => r.GetArchiveStorageFoldersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([storageFolder]);

        SetupArchives(archive);

        SetupFiles(
            $"{BasePath}/Release.A/release.a.part1.rar",
            $"{BasePath}/Release.A/release.a.part2.rar"
        );

        // Act
        var result = await service.RelocateAsync([1], BasePath, CancellationToken.None);

        // Assert
        result.RelocatedArchiveCount.ShouldBe(1);
        result.NotRelocatedArchives.ShouldBeEmpty();
        archive.ArchiveState.ShouldBe(ArchiveState.Created);
        archive.ArchiveFolderPath.ShouldBe($"{BasePath}/Release.A");

        archive
            .ArchiveFiles.Select(archiveFile => archiveFile.FullFileName)
            .ShouldBe([
                $"{BasePath}/Release.A/release.a.part1.rar",
                $"{BasePath}/Release.A/release.a.part2.rar",
            ]);

        archive.ArchiveStorageFolderId.ShouldBe(storageFolder.Id);
        archive.ArchiveConfig.ArchiveFilesBasePath.ShouldBe(expectedArchiveFilesBasePath);
        waitingUpload.UploadState.ShouldBe(UploadState.Pending);
        repositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private void SetupArchives(params Archive[] archives)
    {
        repositoryMock
            .Setup(r =>
                r.GetArchivesWithFilesUploadsAndReleaseAsync(
                    It.IsAny<IReadOnlyList<int>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(archives);
    }

    private void SetupFiles(params string[] filePaths)
    {
        fileSystemServiceMock
            .Setup(f => f.GetFilesInPath(BasePath, true))
            .Returns(filePaths.ToList());
    }

    private static Archive CreateArchive(
        int id,
        string releaseName,
        ArchiveState archiveState = ArchiveState.Deleted,
        ReleaseType releaseType = ReleaseType.Unmanaged
    )
    {
        return new Archive
        {
            Id = id,
            ArchiveConfigId = id,
            ArchiveFolderPath = ImportBasePath,
            ArchiveState = archiveState,
            ArchiveConfig = new ArchiveConfig
            {
                Id = id,
                Name = "RAR",
                ArchiveFilesBasePath = ImportBasePath,
                Release = new Release { Name = releaseName, ReleaseType = releaseType },
            },
            ArchiveFiles =
            [
                new ArchiveFile { FullFileName = $"{ImportBasePath}/{releaseName}.part1.rar" },
                new ArchiveFile { FullFileName = $"{ImportBasePath}/{releaseName}.part2.rar" },
            ],
            Uploads = [],
            ErrorMessages = [],
            Notifications = [],
        };
    }
}

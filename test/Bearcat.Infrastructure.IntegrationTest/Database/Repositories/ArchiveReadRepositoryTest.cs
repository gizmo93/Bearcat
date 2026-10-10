using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageArchives.ReadModels;
using Bearcat.Domain.UseCases.ManageArchives.Search;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class ArchiveReadRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private static readonly DateTime SearchStart = new(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);

    private ArchiveReadRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        repository = new ArchiveReadRepository(DbContext);
    }

    [Test]
    public async Task GetByIdAsync_SeveralArchivesWithFiles_ReturnsRequestedArchiveWithItsFiles()
    {
        // Arrange
        var archives = await AddArchivesAsync();

        // Act
        var result = await repository.GetByIdAsync(
            archives.SecondArchive.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.ArchiveId.ShouldBe(archives.SecondArchive.Id);
        result.ArchiveFolderPath.ShouldBe("/tmp/archives/second");
        result.CreatedAt.ShouldBe(new DateTime(2026, 9, 30, 0, 0, 0, 500, DateTimeKind.Utc));
        result.Files.ShouldBe(
            archives
                .SecondArchive.ArchiveFiles.Select(
                    file => new ArchiveReadModel.ArchiveFileReadModel(file.Id, file.FullFileName)
                )
                .ToList(),
            ignoreOrder: true
        );
        result.Files.Count.ShouldBe(3);
    }

    [Test]
    public async Task GetByIdAsync_ArchiveWithoutFiles_ReturnsEmptyFiles()
    {
        // Arrange
        var archives = await AddArchivesAsync();

        // Act
        var result = await repository.GetByIdAsync(
            archives.EmptyArchive.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.ArchiveId.ShouldBe(archives.EmptyArchive.Id);
        result.ArchiveFolderPath.ShouldBe("/tmp/archives/empty");
        result.Files.ShouldBeEmpty();
    }

    [Test]
    public async Task GetByIdAsync_ArchiveDoesNotExist_ReturnsNull()
    {
        // Arrange
        var archives = await AddArchivesAsync();

        // Act
        var result = await repository.GetByIdAsync(
            archives.EmptyArchive.Id + 1,
            CancellationToken.None
        );

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task GetByIdAsync_ArchiveInStorageFolder_ReturnsStorageFolderNameWithoutLocalWorkingCopy()
    {
        // Arrange
        var archive = CreateArchive(
            CreateArchiveConfig(),
            "/mnt/nas/first",
            new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
            ["/mnt/nas/first/first.part1.rar"]
        );
        archive.ArchiveStorageFolder = CreateStorageFolder();
        DbContext.Add(archive);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetByIdAsync(archive.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.ArchiveStorageFolderName.ShouldBe("NAS");
        result.HasLocalWorkingCopyFiles.ShouldBeFalse();
    }

    [Test]
    public async Task GetByIdAsync_ArchiveInStorageFolderWithFileInLocalWorkingCopy_ReturnsLocalWorkingCopy()
    {
        // Arrange
        var archive = CreateArchive(
            CreateArchiveConfig(),
            "/mnt/nas/first",
            new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
            ["/tmp/archives/first/first.part1.rar", "/mnt/nas/first/first.part2.rar"]
        );
        archive.ArchiveStorageFolder = CreateStorageFolder();
        DbContext.Add(archive);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetByIdAsync(archive.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.HasLocalWorkingCopyFiles.ShouldBeTrue();
    }

    [Test]
    public async Task GetByIdAsync_ArchiveInStorageFolderWithFileInSiblingFolderWithSamePrefix_ReturnsLocalWorkingCopy()
    {
        // Arrange
        var archive = CreateArchive(
            CreateArchiveConfig(),
            "/mnt/nas/Rel",
            new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
            ["/mnt/nas/Rel.5/Rel.part1.rar"]
        );
        archive.ArchiveStorageFolder = CreateStorageFolder();
        DbContext.Add(archive);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetByIdAsync(archive.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.HasLocalWorkingCopyFiles.ShouldBeTrue();
    }

    [Test]
    public async Task GetByIdAsync_LocalArchive_ReturnsNoStorageFolderName()
    {
        // Arrange
        var archives = await AddArchivesAsync();

        // Act
        var result = await repository.GetByIdAsync(
            archives.SecondArchive.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.ArchiveStorageFolderName.ShouldBeNull();
        result.HasLocalWorkingCopyFiles.ShouldBeFalse();
    }

    [Test]
    public async Task SearchArchivesAsync_ArchivesInStorageFolder_ReturnsStorageFolderNameAndLocalWorkingCopy()
    {
        // Arrange
        var archiveConfig = CreateArchiveConfig();
        var workingCopyArchive = CreateArchive(
            archiveConfig,
            "/mnt/nas/third",
            new DateTime(2026, 9, 30, 0, 0, 2, DateTimeKind.Utc),
            ["/tmp/archives/third/third.part1.rar"]
        );
        workingCopyArchive.ArchiveStorageFolder = CreateStorageFolder();
        var storageArchive = CreateArchive(
            archiveConfig,
            "/mnt/nas/first",
            new DateTime(2026, 9, 30, 0, 0, 1, DateTimeKind.Utc),
            ["/mnt/nas/first/first.part1.rar"]
        );
        storageArchive.ArchiveStorageFolder = workingCopyArchive.ArchiveStorageFolder;
        var localArchive = CreateArchive(
            archiveConfig,
            "/tmp/archives/second",
            new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
            ["/tmp/other/second.part1.rar"]
        );
        DbContext.AddRange(workingCopyArchive, storageArchive, localArchive);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.SearchArchivesAsync(
            new ArchiveSearchQuery(PageSize: 10),
            CancellationToken.None
        );

        // Assert
        result
            .Items.Select(item =>
                (item.ArchiveId, item.ArchiveStorageFolderName, item.HasLocalWorkingCopyFiles)
            )
            .ShouldBe([
                (workingCopyArchive.Id, "NAS", true),
                (storageArchive.Id, "NAS", false),
                (localArchive.Id, null, false),
            ]);
    }

    [Test]
    public async Task SearchArchivesAsync_NoFilter_ReturnsAllArchivesOrderedByCreatedAtAndIdDescending()
    {
        // Arrange
        var archives = await AddSearchArchivesAsync();

        // Act
        var result = await repository.SearchArchivesAsync(
            new ArchiveSearchQuery(PageSize: 10),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(6);
        result
            .Items.Select(item => item.ArchiveId)
            .ShouldBe([
                archives.Restoring.Id,
                archives.Creating.Id,
                archives.CreationFailed.Id,
                archives.MissingFiles.Id,
                archives.Deleted.Id,
                archives.Created.Id,
            ]);
    }

    [Test]
    public async Task SearchArchivesAsync_ArchiveStateFilter_ReturnsMatchingArchives()
    {
        // Arrange
        var archives = await AddSearchArchivesAsync();

        // Act
        var result = await repository.SearchArchivesAsync(
            new ArchiveSearchQuery(ArchiveState: ArchiveState.MissingFiles),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items.Select(item => item.ArchiveId).ShouldBe([archives.MissingFiles.Id]);
    }

    [Test]
    public async Task SearchArchivesAsync_ArchiverFilter_ReturnsArchivesOfArchiver()
    {
        // Arrange
        var archives = await AddSearchArchivesAsync();

        // Act
        var result = await repository.SearchArchivesAsync(
            new ArchiveSearchQuery(ArchiverName: "SevenZipArchiver"),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(3);
        result
            .Items.Select(item => item.ArchiveId)
            .ShouldBe([archives.Creating.Id, archives.CreationFailed.Id, archives.MissingFiles.Id]);
    }

    [Test]
    public async Task SearchArchivesAsync_ReleaseGroupFilter_ReturnsArchivesOfReleasesInGroup()
    {
        // Arrange
        var archives = await AddSearchArchivesAsync();

        // Act
        var result = await repository.SearchArchivesAsync(
            new ArchiveSearchQuery(ReleaseGroupId: archives.FirstReleaseGroupId),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(3);
        result
            .Items.Select(item => item.ArchiveId)
            .ShouldBe([archives.Restoring.Id, archives.Deleted.Id, archives.Created.Id]);
    }

    [Test]
    public async Task SearchArchivesAsync_OnDiskFilter_ReturnsCreatedArchivesOnly()
    {
        // Arrange
        var archives = await AddSearchArchivesAsync();

        // Act
        var result = await repository.SearchArchivesAsync(
            new ArchiveSearchQuery(OnDiskFilter: ArchiveOnDiskFilter.OnDisk),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items.Select(item => item.ArchiveId).ShouldBe([archives.Created.Id]);
    }

    [Test]
    public async Task SearchArchivesAsync_NotOnDiskFilter_ReturnsDeletedMissingAndFailedArchives()
    {
        // Arrange
        var archives = await AddSearchArchivesAsync();

        // Act
        var result = await repository.SearchArchivesAsync(
            new ArchiveSearchQuery(OnDiskFilter: ArchiveOnDiskFilter.NotOnDisk),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(3);
        result
            .Items.Select(item => item.ArchiveId)
            .ShouldBe([archives.CreationFailed.Id, archives.MissingFiles.Id, archives.Deleted.Id]);
    }

    [Test]
    public async Task SearchArchivesAsync_SearchTermMatchesReleaseNameWithNonAsciiCharacterInDifferentCase_ReturnsArchivesOfRelease()
    {
        // Arrange
        var archives = await AddSearchArchivesAsync();

        // Act
        var result = await repository.SearchArchivesAsync(
            new ArchiveSearchQuery(SearchTerm: "  bearcat.über.RELEASE  "),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(3);
        result
            .Items.Select(item => item.ArchiveId)
            .ShouldBe([archives.Restoring.Id, archives.Deleted.Id, archives.Created.Id]);
    }

    [Test]
    public async Task SearchArchivesAsync_SearchTermMatchesArchiveFolderPathInDifferentCase_ReturnsArchive()
    {
        // Arrange
        var archives = await AddSearchArchivesAsync();

        // Act
        var result = await repository.SearchArchivesAsync(
            new ArchiveSearchQuery(SearchTerm: "ÜBERSICHT-FOLDER"),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items.Select(item => item.ArchiveId).ShouldBe([archives.Created.Id]);
    }

    [Test]
    public async Task SearchArchivesAsync_SearchTermMatchesArchiveFileNameInDifferentCase_ReturnsArchive()
    {
        // Arrange
        var archives = await AddSearchArchivesAsync();

        // Act
        var result = await repository.SearchArchivesAsync(
            new ArchiveSearchQuery(SearchTerm: "MISSING.PART2"),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items.Select(item => item.ArchiveId).ShouldBe([archives.MissingFiles.Id]);
    }

    [Test]
    public async Task SearchArchivesAsync_SearchTermMatchesMd5HashInDifferentCase_ReturnsArchive()
    {
        // Arrange
        var archives = await AddSearchArchivesAsync();

        // Act
        var result = await repository.SearchArchivesAsync(
            new ArchiveSearchQuery(SearchTerm: "abcdef01"),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items.Select(item => item.ArchiveId).ShouldBe([archives.Created.Id]);
    }

    [Test]
    public async Task SearchArchivesAsync_SearchTermIsArchiveIdWithHashPrefix_ReturnsArchive()
    {
        // Arrange
        var archives = await AddSearchArchivesAsync();

        // Act
        var result = await repository.SearchArchivesAsync(
            new ArchiveSearchQuery(SearchTerm: $"#{archives.Deleted.Id}"),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items.Select(item => item.ArchiveId).ShouldBe([archives.Deleted.Id]);
    }

    [Test]
    public async Task SearchArchivesAsync_SearchTermMatchesNothing_ReturnsEmptyResult()
    {
        // Arrange
        await AddSearchArchivesAsync();

        // Act
        var result = await repository.SearchArchivesAsync(
            new ArchiveSearchQuery(SearchTerm: "does-not-exist"),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(0);
        result.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task SearchArchivesAsync_SearchTermAndOnDiskFilter_ReturnsArchivesMatchingBoth()
    {
        // Arrange
        var archives = await AddSearchArchivesAsync();

        // Act
        var result = await repository.SearchArchivesAsync(
            new ArchiveSearchQuery(
                SearchTerm: "bearcat.über",
                OnDiskFilter: ArchiveOnDiskFilter.NotOnDisk
            ),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items.Select(item => item.ArchiveId).ShouldBe([archives.Deleted.Id]);
    }

    [Test]
    public async Task SearchArchivesAsync_SecondPage_ReturnsRemainingArchiveAndTotalCount()
    {
        // Arrange
        var archives = await AddSearchArchivesAsync();

        // Act
        var result = await repository.SearchArchivesAsync(
            new ArchiveSearchQuery(PageIndex: 1, PageSize: 5),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(6);
        result.PageIndex.ShouldBe(1);
        result.PageSize.ShouldBe(5);
        result.Items.Select(item => item.ArchiveId).ShouldBe([archives.Created.Id]);
    }

    [Test]
    public async Task SearchArchivesAsync_ArchiveWithFilesAndUploads_ProjectsAllFields()
    {
        // Arrange
        var archives = await AddSearchArchivesAsync();

        // Act
        var result = await repository.SearchArchivesAsync(
            new ArchiveSearchQuery(SearchTerm: $"#{archives.Created.Id}"),
            CancellationToken.None
        );

        // Assert
        var item = result.Items.ShouldHaveSingleItem();
        item.ArchiveId.ShouldBe(archives.Created.Id);
        item.ReleaseId.ShouldBe(archives.FirstReleaseId);
        item.ReleaseName.ShouldBe("Bearcat.Über.Release.2026-GRP");
        item.ArchiveConfigId.ShouldBe(archives.Created.ArchiveConfigId);
        item.ArchiveConfigName.ShouldBe("Rar archive");
        item.ArchiverName.ShouldBe("RarArchiver");
        item.ArchiveState.ShouldBe(ArchiveState.Created);
        item.ArchiveFolderPath.ShouldBe("/data/archives/Übersicht-folder");
        item.CreatedAt.ShouldBe(SearchStart.AddSeconds(1).AddMilliseconds(250));
        item.ArchiveFileCount.ShouldBe(2);
        item.UploadCount.ShouldBe(2);
        item.ErrorMessages.ShouldBeEmpty();
    }

    [Test]
    public async Task SearchArchivesAsync_ArchiveWithErrorsAndWithoutFiles_ProjectsErrorsAndZeroCounts()
    {
        // Arrange
        var archives = await AddSearchArchivesAsync();

        // Act
        var result = await repository.SearchArchivesAsync(
            new ArchiveSearchQuery(SearchTerm: $"#{archives.CreationFailed.Id}"),
            CancellationToken.None
        );

        // Assert
        var item = result.Items.ShouldHaveSingleItem();
        item.ArchiveFileCount.ShouldBe(0);
        item.UploadCount.ShouldBe(0);
        item.ErrorMessages.ShouldBe(["Packing of Façade failed"]);
    }

    [Test]
    public async Task GetCreatingOrRestoringArchivesOrArchivesWithIdsAsync_EmptyArchiveIds_ReturnsCreatingAndRestoringArchives()
    {
        // Arrange
        var archives = await AddArchivesInAllStatesAsync();

        // Act
        var result = await repository.GetCreatingOrRestoringArchivesOrArchivesWithIdsAsync(
            [],
            CancellationToken.None
        );

        // Assert
        result
            .Select(archive => archive.ArchiveId)
            .ShouldBe(
                [archives.CreatingArchive.Id, archives.RestoringArchive.Id],
                ignoreOrder: true
            );
    }

    [Test]
    public async Task GetCreatingOrRestoringArchivesOrArchivesWithIdsAsync_CreatedArchiveIdPassed_ReturnsThisCreatedArchiveAdditionally()
    {
        // Arrange
        var archives = await AddArchivesInAllStatesAsync();

        // Act
        var result = await repository.GetCreatingOrRestoringArchivesOrArchivesWithIdsAsync(
            [archives.CreatedArchiveWithPassedId.Id],
            CancellationToken.None
        );

        // Assert
        result
            .Select(archive => archive.ArchiveId)
            .ShouldBe(
                [
                    archives.CreatingArchive.Id,
                    archives.RestoringArchive.Id,
                    archives.CreatedArchiveWithPassedId.Id,
                ],
                ignoreOrder: true
            );
    }

    [Test]
    public async Task GetCreatingOrRestoringArchivesOrArchivesWithIdsAsync_CreatingArchive_ReturnsProjectedReleaseAndArchiveConfigFields()
    {
        // Arrange
        var archives = await AddArchivesInAllStatesAsync();

        // Act
        var result = await repository.GetCreatingOrRestoringArchivesOrArchivesWithIdsAsync(
            [archives.CreatedArchiveWithPassedId.Id],
            CancellationToken.None
        );

        // Assert
        result
            .Single(archive => archive.ArchiveId == archives.CreatingArchive.Id)
            .ShouldBe(
                new RunningArchiveReadModel(
                    archives.CreatingArchive.Id,
                    archives.ReleaseId,
                    "Bearcat.Release.2026-GRP",
                    "Main archive",
                    "RarArchiver",
                    ArchiveState.Creating
                )
            );
        result
            .Single(archive => archive.ArchiveId == archives.CreatedArchiveWithPassedId.Id)
            .ArchiveState.ShouldBe(ArchiveState.Created);
    }

    private async Task<ArchivesInAllStates> AddArchivesInAllStatesAsync()
    {
        var archiveConfig = CreateArchiveConfig();
        var createdAt = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);
        var creatingArchive = CreateArchive(archiveConfig, "/tmp/archives/creating", createdAt, []);
        creatingArchive.ArchiveState = ArchiveState.Creating;
        var restoringArchive = CreateArchive(
            archiveConfig,
            "/tmp/archives/restoring",
            createdAt,
            []
        );
        restoringArchive.ArchiveState = ArchiveState.Restoring;
        var createdArchiveWithPassedId = CreateArchive(
            archiveConfig,
            "/tmp/archives/created-passed",
            createdAt,
            []
        );
        var createdArchive = CreateArchive(archiveConfig, "/tmp/archives/created", createdAt, []);
        var creationFailedArchive = CreateArchive(
            archiveConfig,
            "/tmp/archives/creation-failed",
            createdAt,
            []
        );
        creationFailedArchive.ArchiveState = ArchiveState.CreationFailed;
        var missingFilesArchive = CreateArchive(
            archiveConfig,
            "/tmp/archives/missing-files",
            createdAt,
            []
        );
        missingFilesArchive.ArchiveState = ArchiveState.MissingFiles;
        var deletedArchive = CreateArchive(archiveConfig, "/tmp/archives/deleted", createdAt, []);
        deletedArchive.ArchiveState = ArchiveState.Deleted;

        DbContext.AddRange(
            creatingArchive,
            restoringArchive,
            createdArchiveWithPassedId,
            createdArchive,
            creationFailedArchive,
            missingFilesArchive,
            deletedArchive
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return new ArchivesInAllStates(
            archiveConfig.ReleaseId,
            creatingArchive,
            restoringArchive,
            createdArchiveWithPassedId
        );
    }

    private static ArchiveStorageFolder CreateStorageFolder()
    {
        return new ArchiveStorageFolder
        {
            Name = "NAS",
            Path = "/mnt/nas",
            IsActive = true,
        };
    }

    private static ArchiveConfig CreateArchiveConfig()
    {
        return new ArchiveConfig
        {
            Release = new Release
            {
                Name = "Bearcat.Release.2026-GRP",
                CreatedAt = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
                ReleaseType = ReleaseType.Managed,
                ReleaseFolderPath = "/tmp/release",
                ReleaseGroup = new ReleaseGroup
                {
                    Name = "Release group",
                    EnableAutomaticReuploads = false,
                    NumberOfHoursUntilReupload = 24,
                },
            },
            Name = "Main archive",
            ArchiveFilesBasePath = "/tmp/archives",
            ArchiverName = "RarArchiver",
            ArchiveFileSizeMb = 100,
        };
    }

    private async Task<ArchiveSeed> AddArchivesAsync()
    {
        var archiveConfig = CreateArchiveConfig();
        var firstArchive = CreateArchive(
            archiveConfig,
            "/tmp/archives/first",
            new DateTime(2026, 9, 29, 23, 59, 59, 750, DateTimeKind.Utc),
            ["first.part1.rar", "first.part2.rar"]
        );
        var secondArchive = CreateArchive(
            archiveConfig,
            "/tmp/archives/second",
            new DateTime(2026, 9, 30, 0, 0, 0, 500, DateTimeKind.Utc),
            ["second.part1.rar", "second.part2.rar", "second.part3.rar"]
        );
        var emptyArchive = CreateArchive(
            archiveConfig,
            "/tmp/archives/empty",
            new DateTime(2026, 9, 30, 0, 0, 1, DateTimeKind.Utc),
            []
        );

        DbContext.AddRange(firstArchive, secondArchive, emptyArchive);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return new ArchiveSeed(secondArchive, emptyArchive);
    }

    private static Archive CreateArchive(
        ArchiveConfig archiveConfig,
        string archiveFolderPath,
        DateTime createdAt,
        IReadOnlyList<string> fileNames
    )
    {
        return new Archive
        {
            ArchiveConfig = archiveConfig,
            ArchiveFolderPath = archiveFolderPath,
            CreatedAt = createdAt,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 100,
            ArchiveFiles = fileNames
                .Select(fileName => new ArchiveFile { FullFileName = fileName })
                .ToList(),
        };
    }

    private async Task<SearchArchives> AddSearchArchivesAsync()
    {
        var firstRelease = CreateSearchRelease("Bearcat.Über.Release.2026-GRP");
        var secondRelease = CreateSearchRelease("Bearcat.Other.2026-GRP");
        var rarArchiveConfig = CreateSearchArchiveConfig(
            firstRelease,
            "Rar archive",
            "RarArchiver"
        );
        var sevenZipArchiveConfig = CreateSearchArchiveConfig(
            secondRelease,
            "Seven zip archive",
            "SevenZipArchiver"
        );
        var created = CreateSearchArchive(
            rarArchiveConfig,
            ArchiveState.Created,
            "/data/archives/Übersicht-folder",
            SearchStart.AddSeconds(1).AddMilliseconds(250),
            [
                new ArchiveFile
                {
                    FullFileName = "created.part1.rar",
                    Md5Hash = "ABCDEF0123456789",
                },
                new ArchiveFile { FullFileName = "created.part2.rar", Md5Hash = null },
            ]
        );
        var deleted = CreateSearchArchive(
            rarArchiveConfig,
            ArchiveState.Deleted,
            "/data/archives/deleted",
            SearchStart.AddSeconds(2),
            [new ArchiveFile { FullFileName = "deleted.rar" }]
        );
        var missingFiles = CreateSearchArchive(
            sevenZipArchiveConfig,
            ArchiveState.MissingFiles,
            "/data/archives/missing",
            SearchStart.AddSeconds(3),
            [
                new ArchiveFile { FullFileName = "missing.part1.7z" },
                new ArchiveFile { FullFileName = "missing.part2.7z" },
            ]
        );
        var creationFailed = CreateSearchArchive(
            sevenZipArchiveConfig,
            ArchiveState.CreationFailed,
            "/data/archives/failed",
            SearchStart.AddSeconds(4),
            []
        );
        creationFailed.ErrorMessages = ["Packing of Façade failed"];
        var creating = CreateSearchArchive(
            sevenZipArchiveConfig,
            ArchiveState.Creating,
            "/data/archives/creating",
            SearchStart.AddSeconds(5),
            []
        );

        DbContext.AddRange(created, deleted, missingFiles, creationFailed, creating);
        await DbContext.SaveChangesAsync();

        var restoring = CreateSearchArchive(
            rarArchiveConfig,
            ArchiveState.Restoring,
            "/data/archives/restoring",
            SearchStart.AddSeconds(5),
            []
        );
        var uploadConfig = new UploadConfig
        {
            Release = firstRelease,
            ArchiveConfig = rarArchiveConfig,
            HosterRegistration = new HosterRegistration
            {
                Name = "Hoster",
                SerializedConfig = "{}",
                HosterClassName = "TestHoster",
                IsActive = true,
            },
            Name = "Upload",
        };

        DbContext.Add(restoring);
        DbContext.AddRange(
            CreateUploadOfArchive(uploadConfig, created),
            CreateUploadOfArchive(uploadConfig, created)
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return new SearchArchives(
            firstRelease.Id,
            firstRelease.ReleaseGroupId,
            created,
            deleted,
            missingFiles,
            creationFailed,
            creating,
            restoring
        );
    }

    private static Release CreateSearchRelease(string name)
    {
        return new Release
        {
            Name = name,
            CreatedAt = SearchStart,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = $"/tmp/{name}",
            ReleaseGroup = new ReleaseGroup
            {
                Name = $"{name} group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
        };
    }

    private static ArchiveConfig CreateSearchArchiveConfig(
        Release release,
        string name,
        string archiverName
    )
    {
        return new ArchiveConfig
        {
            Release = release,
            Name = name,
            ArchiveFilesBasePath = "/data/archives",
            ArchiverName = archiverName,
            ArchiveFileSizeMb = 100,
        };
    }

    private static Archive CreateSearchArchive(
        ArchiveConfig archiveConfig,
        ArchiveState archiveState,
        string archiveFolderPath,
        DateTime createdAt,
        List<ArchiveFile> archiveFiles
    )
    {
        return new Archive
        {
            ArchiveConfig = archiveConfig,
            ArchiveFolderPath = archiveFolderPath,
            CreatedAt = createdAt,
            ArchiveState = archiveState,
            ArchiveFileSizeMb = 100,
            ArchiveFiles = archiveFiles,
        };
    }

    private static Upload CreateUploadOfArchive(UploadConfig uploadConfig, Archive archive)
    {
        return new Upload
        {
            UploadConfig = uploadConfig,
            Archive = archive,
            CreatedAt = SearchStart,
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Online,
            UploadedFiles = [],
            ErrorMessages = [],
        };
    }

    private sealed record ArchiveSeed(Archive SecondArchive, Archive EmptyArchive);

    private sealed record ArchivesInAllStates(
        int ReleaseId,
        Archive CreatingArchive,
        Archive RestoringArchive,
        Archive CreatedArchiveWithPassedId
    );

    private sealed record SearchArchives(
        int FirstReleaseId,
        int FirstReleaseGroupId,
        Archive Created,
        Archive Deleted,
        Archive MissingFiles,
        Archive CreationFailed,
        Archive Creating,
        Archive Restoring
    );
}

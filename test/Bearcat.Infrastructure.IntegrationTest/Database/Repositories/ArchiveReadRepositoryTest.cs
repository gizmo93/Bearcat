using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageArchives.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class ArchiveReadRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
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

    private async Task<ArchiveSeed> AddArchivesAsync()
    {
        var archiveConfig = new ArchiveConfig
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

    private sealed record ArchiveSeed(Archive SecondArchive, Archive EmptyArchive);
}

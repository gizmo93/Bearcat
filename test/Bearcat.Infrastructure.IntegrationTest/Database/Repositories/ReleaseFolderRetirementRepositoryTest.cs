using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class ReleaseFolderRetirementRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private static readonly DateTime Cutoff = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

    private ReleaseFolderRetirementRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        repository = new ReleaseFolderRetirementRepository(DbContext);
    }

    [Test]
    public async Task GetConversionCandidatesAsync_OverdueManagedRelease_LoadsArchiveFiles()
    {
        // Arrange
        var release = await AddReleaseAsync(
            "Overdue",
            ReleaseType.Managed,
            uploadsPostedAt: Cutoff.AddDays(-1)
        );

        // Act
        var result = await repository.GetConversionCandidatesAsync(Cutoff, CancellationToken.None);

        // Assert
        var candidate = result.ShouldHaveSingleItem();
        candidate.Id.ShouldBe(release.Id);
        candidate
            .ArchiveConfigs.ShouldHaveSingleItem()
            .Archives.ShouldHaveSingleItem()
            .ArchiveFiles.Select(f => f.FullFileName)
            .ShouldBe(
                ["/archives/overdue.part1.rar", "/archives/overdue.part2.rar"],
                ignoreOrder: true
            );
    }

    [Test]
    public async Task GetConversionCandidatesAsync_ReleaseIsNotOverdueOrNotManaged_ReturnsNoCandidates()
    {
        // Arrange
        await AddReleaseAsync("Recent", ReleaseType.Managed, uploadsPostedAt: Cutoff.AddDays(1));
        await AddReleaseAsync(
            "Unmanaged",
            ReleaseType.Unmanaged,
            uploadsPostedAt: Cutoff.AddDays(-1)
        );

        // Act
        var result = await repository.GetConversionCandidatesAsync(Cutoff, CancellationToken.None);

        // Assert
        result.ShouldBeEmpty();
    }

    private async Task<Release> AddReleaseAsync(
        string name,
        ReleaseType releaseType,
        DateTime uploadsPostedAt
    )
    {
        var release = new Release
        {
            Name = name,
            ReleaseType = releaseType,
            ReleaseFolderPath = releaseType is ReleaseType.Managed ? $"/releases/{name}" : null,
            UploadsPostedAt = uploadsPostedAt,
            ReleaseGroup = new ReleaseGroup
            {
                Name = $"{name} group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
        };
        var archiveConfig = new ArchiveConfig
        {
            Release = release,
            Name = "Main archive",
            ArchiveFilesBasePath = "/archives",
            ArchiverName = "zip",
            ArchiveNamePrefix = name,
            ArchivePassword = "secret",
            ArchiveFileSizeMb = 512,
            Archives =
            [
                new Archive
                {
                    ArchiveFolderPath = "/archives",
                    ArchiveState = ArchiveState.Created,
                    ArchiveFileSizeMb = 512,
                    CreatedAt = Cutoff.AddDays(-30),
                    ArchiveFiles =
                    [
                        new ArchiveFile
                        {
                            FullFileName = $"/archives/{name.ToLowerInvariant()}.part1.rar",
                        },
                        new ArchiveFile
                        {
                            FullFileName = $"/archives/{name.ToLowerInvariant()}.part2.rar",
                        },
                    ],
                    Uploads = [],
                    ErrorMessages = [],
                },
            ],
        };

        DbContext.ArchiveConfigs.Add(archiveConfig);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return release;
    }
}

using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class ReleaseInfoRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private static readonly DateTime LocalNow = new(2026, 5, 17, 12, 0, 0, DateTimeKind.Utc);

    private ReleaseInfoRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        repository = new ReleaseInfoRepository(
            ReadDbContext,
            DbContext,
            NoOpSecretProtector.Instance
        );
    }

    [Test]
    public async Task GetReleasesWithoutInfoAsync_NoPosition_ReturnsOldestReleasesOrderedByCreatedAtAndId()
    {
        // Arrange
        var oldestRelease = await AddReleaseAsync("Oldest", LocalNow.AddDays(-3));
        var firstReleaseWithSameCreatedAt = await AddReleaseAsync("Second", LocalNow.AddDays(-2));
        await AddReleaseAsync("Third", LocalNow.AddDays(-2));

        // Act
        var result = await repository.GetReleasesWithoutInfoAsync(
            count: 2,
            lastCheckedThreshold: LocalNow.AddDays(-1),
            afterCreatedAt: null,
            afterReleaseId: 0,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result
            .Select(release => release.Id)
            .ShouldBe([oldestRelease.Id, firstReleaseWithSameCreatedAt.Id]);
    }

    [Test]
    public async Task GetReleasesWithoutInfoAsync_AfterPosition_ReturnsReleasesAfterCreatedAtAndIdOfPosition()
    {
        // Arrange
        await AddReleaseAsync("Oldest", LocalNow.AddDays(-3));
        var firstReleaseWithSameCreatedAt = await AddReleaseAsync("Second", LocalNow.AddDays(-2));
        var secondReleaseWithSameCreatedAt = await AddReleaseAsync("Third", LocalNow.AddDays(-2));
        var newestRelease = await AddReleaseAsync("Newest", LocalNow.AddDays(-1));

        // Act
        var result = await repository.GetReleasesWithoutInfoAsync(
            count: 100,
            lastCheckedThreshold: LocalNow.AddDays(-1),
            afterCreatedAt: firstReleaseWithSameCreatedAt.CreatedAt,
            afterReleaseId: firstReleaseWithSameCreatedAt.Id,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result
            .Select(release => release.Id)
            .ShouldBe([secondReleaseWithSameCreatedAt.Id, newestRelease.Id]);
    }

    [Test]
    public async Task GetReleasesWithoutInfoAsync_ReleaseCheckedAfterThreshold_IsExcluded()
    {
        // Arrange
        var uncheckedRelease = await AddReleaseAsync("Unchecked", LocalNow.AddDays(-3));
        await AddReleaseAsync("Checked", LocalNow.AddDays(-2), checkedAt: LocalNow.AddHours(-1));

        // Act
        var result = await repository.GetReleasesWithoutInfoAsync(
            count: 100,
            lastCheckedThreshold: LocalNow.AddDays(-1),
            afterCreatedAt: null,
            afterReleaseId: 0,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ShouldHaveSingleItem().Id.ShouldBe(uncheckedRelease.Id);
    }

    private async Task<Release> AddReleaseAsync(
        string name,
        DateTime createdAt,
        DateTime? checkedAt = null
    )
    {
        var release = new Release
        {
            Name = name,
            ReleaseType = ReleaseType.Unmanaged,
            CreatedAt = createdAt,
            ReleaseInfoCheckedAt = checkedAt,
            MetadataCheckedAt = checkedAt,
            ReleaseGroup = new ReleaseGroup
            {
                Name = $"{name} group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
        };

        DbContext.Releases.Add(release);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return release;
    }
}

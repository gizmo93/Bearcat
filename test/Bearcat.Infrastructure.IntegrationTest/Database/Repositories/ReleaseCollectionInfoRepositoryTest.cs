using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class ReleaseCollectionInfoRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private static readonly DateTime LocalNow = new(2026, 5, 17, 12, 0, 0, DateTimeKind.Utc);

    private ReleaseCollectionInfoRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        repository = new ReleaseCollectionInfoRepository(DbContext);
    }

    [Test]
    public async Task GetCollectionsWithoutMetadataAsync_NoPosition_ReturnsOldestCollectionsOrderedByCreatedAtAndId()
    {
        // Arrange
        var oldestCollection = await AddCollectionAsync("Oldest", LocalNow.AddDays(-3));
        var firstCollectionWithSameCreatedAt = await AddCollectionAsync(
            "Second",
            LocalNow.AddDays(-2)
        );
        await AddCollectionAsync("Third", LocalNow.AddDays(-2));

        // Act
        var result = await repository.GetCollectionsWithoutMetadataAsync(
            count: 2,
            lastCheckedThreshold: LocalNow.AddDays(-7),
            afterCreatedAt: null,
            afterCollectionId: 0,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result
            .Select(collection => collection.Id)
            .ShouldBe([oldestCollection.Id, firstCollectionWithSameCreatedAt.Id]);
    }

    [Test]
    public async Task GetCollectionsWithoutMetadataAsync_AfterPosition_ReturnsCollectionsAfterCreatedAtAndIdOfPosition()
    {
        // Arrange
        await AddCollectionAsync("Oldest", LocalNow.AddDays(-3));
        var firstCollectionWithSameCreatedAt = await AddCollectionAsync(
            "Second",
            LocalNow.AddDays(-2)
        );
        var secondCollectionWithSameCreatedAt = await AddCollectionAsync(
            "Third",
            LocalNow.AddDays(-2)
        );
        var newestCollection = await AddCollectionAsync("Newest", LocalNow.AddDays(-1));

        // Act
        var result = await repository.GetCollectionsWithoutMetadataAsync(
            count: 100,
            lastCheckedThreshold: LocalNow.AddDays(-7),
            afterCreatedAt: firstCollectionWithSameCreatedAt.CreatedAt,
            afterCollectionId: firstCollectionWithSameCreatedAt.Id,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result
            .Select(collection => collection.Id)
            .ShouldBe([secondCollectionWithSameCreatedAt.Id, newestCollection.Id]);
    }

    [Test]
    public async Task GetCollectionsWithoutMetadataAsync_CollectionCheckedAfterThreshold_IsExcluded()
    {
        // Arrange
        var uncheckedCollection = await AddCollectionAsync("Unchecked", LocalNow.AddDays(-3));
        await AddCollectionAsync(
            "Checked",
            LocalNow.AddDays(-2),
            metadataCheckedAt: LocalNow.AddDays(-1)
        );

        // Act
        var result = await repository.GetCollectionsWithoutMetadataAsync(
            count: 100,
            lastCheckedThreshold: LocalNow.AddDays(-7),
            afterCreatedAt: null,
            afterCollectionId: 0,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ShouldHaveSingleItem().Id.ShouldBe(uncheckedCollection.Id);
    }

    private async Task<ReleaseCollection> AddCollectionAsync(
        string name,
        DateTime createdAt,
        DateTime? metadataCheckedAt = null
    )
    {
        var collection = new ReleaseCollection
        {
            Name = name,
            Key = name.ToLowerInvariant(),
            ReleaseContentType = ReleaseContentType.TvShowEpisode,
            CreatedAt = createdAt,
            MetadataCheckedAt = metadataCheckedAt,
            ReleaseGroup = new ReleaseGroup
            {
                Name = $"{name} group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
        };

        DbContext.ReleaseCollections.Add(collection);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return collection;
    }
}

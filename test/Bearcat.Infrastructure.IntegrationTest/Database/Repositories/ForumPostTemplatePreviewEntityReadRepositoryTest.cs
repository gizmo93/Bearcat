using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class ForumPostTemplatePreviewEntityReadRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private static readonly DateTime BaseTime = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private ForumPostTemplatePreviewEntityReadRepository repository = null!;
    private ReleaseGroup releaseGroup = null!;

    [SetUp]
    public async Task Setup()
    {
        repository = new ForumPostTemplatePreviewEntityReadRepository(ReadDbContext);
        releaseGroup = new ReleaseGroup
        {
            Name = "Preview group",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
            Releases = [],
        };
        DbContext.ReleaseGroups.Add(releaseGroup);
        await DbContext.SaveChangesAsync();
    }

    [Test]
    public async Task SearchAsync_ReleaseWithSearchTerm_MatchesNameCaseInsensitively()
    {
        // Arrange
        var matchingRelease = AddRelease("Bearcat.Movie.2026-GRP", BaseTime);
        var otherMatchingRelease = AddRelease("BEARCAT.Show.S01E01-GRP", BaseTime.AddHours(1));
        AddRelease("Other.Movie.2026-GRP", BaseTime.AddHours(2));
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchAsync(
            ForumPostTemplateType.Release,
            searchTerm: "  bEaRcAt ",
            limit: 20,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe([
            new ForumPostTemplatePreviewEntityReadModel(
                otherMatchingRelease.Id,
                "BEARCAT.Show.S01E01-GRP"
            ),
            new ForumPostTemplatePreviewEntityReadModel(
                matchingRelease.Id,
                "Bearcat.Movie.2026-GRP"
            ),
        ]);
    }

    [Test]
    public async Task SearchAsync_ReleaseWithLimit_ReturnsOnlyNewestReleasesUpToLimit()
    {
        // Arrange
        AddRelease("First.Release-GRP", BaseTime);
        var secondRelease = AddRelease("Second.Release-GRP", BaseTime.AddHours(1));
        var thirdRelease = AddRelease("Third.Release-GRP", BaseTime.AddHours(2));
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchAsync(
            ForumPostTemplateType.Release,
            searchTerm: null,
            limit: 2,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe([
            new ForumPostTemplatePreviewEntityReadModel(thirdRelease.Id, "Third.Release-GRP"),
            new ForumPostTemplatePreviewEntityReadModel(secondRelease.Id, "Second.Release-GRP"),
        ]);
    }

    [Test]
    public async Task SearchAsync_ReleaseCollectionWithSearchTerm_MatchesNameCaseInsensitively()
    {
        // Arrange
        var matchingCollection = AddCollection("Bodies.2023.S01.German", BaseTime);
        AddCollection("Other.2023.S01.German", BaseTime.AddHours(1));
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchAsync(
            ForumPostTemplateType.ReleaseCollection,
            searchTerm: "BODIES",
            limit: 20,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe([
            new ForumPostTemplatePreviewEntityReadModel(
                matchingCollection.Id,
                "Bodies.2023.S01.German"
            ),
        ]);
    }

    private Release AddRelease(string name, DateTime createdAt)
    {
        var release = new Release
        {
            Name = name,
            CreatedAt = createdAt,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = $"/tmp/{name}",
            ReleaseGroupId = releaseGroup.Id,
        };
        DbContext.Releases.Add(release);

        return release;
    }

    private ReleaseCollection AddCollection(string name, DateTime createdAt)
    {
        var collection = new ReleaseCollection
        {
            ReleaseGroupId = releaseGroup.Id,
            Key = $"key-{Guid.NewGuid():N}",
            Name = name,
            CreatedAt = createdAt,
        };
        DbContext.ReleaseCollections.Add(collection);

        return collection;
    }
}

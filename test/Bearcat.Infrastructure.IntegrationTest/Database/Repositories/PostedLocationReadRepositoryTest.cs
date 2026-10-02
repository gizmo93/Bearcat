using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManagePostedLocations.Dto;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class PostedLocationReadRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private PostedLocationReadRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        repository = new PostedLocationReadRepository(DbContext);
    }

    [Test]
    public async Task SearchForReleaseAsync_ReleaseHasPostedLocations_ReturnsOnlyPostedLocationsOfRelease()
    {
        // Arrange
        var registration = new DistributionSiteRegistration
        {
            Name = "Example forum",
            DistributionSiteClassName = "ExampleForum",
            SerializedConfig = "{}",
        };
        var release = AddRelease("Bearcat.Release.001");
        var otherRelease = AddRelease("Bearcat.Release.002");
        var firstPostedLocation = AddPostedLocation(
            release,
            "https://forum.example/threads/1",
            registration
        );
        var secondPostedLocation = AddPostedLocation(release, "https://blog.example/post/1");
        AddPostedLocation(otherRelease, "https://forum.example/threads/2");
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.SearchForReleaseAsync(
            new ReleasePostedLocationSearchQuery(release.Id),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(2);
        result
            .Items.Select(item => item.PostedLocationId)
            .ShouldBe([firstPostedLocation.Id, secondPostedLocation.Id]);
        result.Items[0].DistributionSiteRegistrationId.ShouldBe(registration.Id);
        result.Items[0].DistributionSiteRegistrationName.ShouldBe("Example forum");
        result.Items[1].DistributionSiteRegistrationName.ShouldBeNull();
    }

    [Test]
    public async Task GetByIdForReleaseAsync_PostedLocationBelongsToOtherRelease_ReturnsNull()
    {
        // Arrange
        var release = AddRelease("Bearcat.Release.001");
        var otherRelease = AddRelease("Bearcat.Release.002");
        var otherPostedLocation = AddPostedLocation(
            otherRelease,
            "https://forum.example/threads/2"
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetByIdForReleaseAsync(
            release.Id,
            otherPostedLocation.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task GetForReleaseAsync_ReleaseAndCollectionPostedLocations_ReturnsOnlyPostedLocationsOfReleaseOrderedById()
    {
        // Arrange
        var registration = new DistributionSiteRegistration
        {
            Name = "Example forum",
            DistributionSiteClassName = "ExampleForum",
            SerializedConfig = "{}",
        };
        var release = AddRelease("Bearcat.Release.001");
        var otherRelease = AddRelease("Bearcat.Release.002");
        var collection = AddCollection("Bearcat.Collection");
        var firstPostedLocation = AddPostedLocation(release, "https://blog.example/post/1");
        AddPostedLocation(otherRelease, "https://forum.example/threads/2");
        AddCollectionPostedLocation(collection, "https://forum.example/threads/3");
        var secondPostedLocation = AddPostedLocation(
            release,
            "https://forum.example/threads/1",
            registration
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetForReleaseAsync(release.Id, CancellationToken.None);

        // Assert
        result
            .Select(item => item.PostedLocationId)
            .ShouldBe([firstPostedLocation.Id, secondPostedLocation.Id]);
        result[0].DistributionSiteRegistrationId.ShouldBeNull();
        result[0].DistributionSiteRegistrationName.ShouldBeNull();
        result[0].Url.ShouldBe("https://blog.example/post/1");
        result[1].DistributionSiteRegistrationId.ShouldBe(registration.Id);
        result[1].DistributionSiteRegistrationName.ShouldBe("Example forum");
    }

    [Test]
    public async Task GetForCollectionAsync_ReleaseAndCollectionPostedLocations_ReturnsOnlyPostedLocationsOfCollectionOrderedById()
    {
        // Arrange
        var registration = new DistributionSiteRegistration
        {
            Name = "Example forum",
            DistributionSiteClassName = "ExampleForum",
            SerializedConfig = "{}",
        };
        var release = AddRelease("Bearcat.Release.001");
        var collection = AddCollection("Bearcat.Collection.001");
        var otherCollection = AddCollection("Bearcat.Collection.002");
        var firstPostedLocation = AddCollectionPostedLocation(
            collection,
            "https://forum.example/threads/1",
            registration
        );
        AddPostedLocation(release, "https://forum.example/threads/2");
        AddCollectionPostedLocation(otherCollection, "https://forum.example/threads/3");
        var secondPostedLocation = AddCollectionPostedLocation(
            collection,
            "https://blog.example/post/1"
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetForCollectionAsync(collection.Id, CancellationToken.None);

        // Assert
        result
            .Select(item => item.PostedLocationId)
            .ShouldBe([firstPostedLocation.Id, secondPostedLocation.Id]);
        result[0].DistributionSiteRegistrationName.ShouldBe("Example forum");
        result[0].Url.ShouldBe("https://forum.example/threads/1");
        result[1].DistributionSiteRegistrationName.ShouldBeNull();
        result[1].Url.ShouldBe("https://blog.example/post/1");
    }

    private Release AddRelease(string name)
    {
        var release = new Release
        {
            Name = name,
            CreatedAt = DateTime.UtcNow,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = $"/tmp/{name}",
            ReleaseGroup = new ReleaseGroup
            {
                Name = $"{name} group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
        };

        DbContext.Releases.Add(release);

        return release;
    }

    private PostedLocation AddPostedLocation(
        Release release,
        string url,
        DistributionSiteRegistration? distributionSiteRegistration = null
    )
    {
        var postedLocation = new PostedLocation
        {
            Release = release,
            DistributionSiteRegistration = distributionSiteRegistration,
            Url = url,
            CreatedAt = DateTime.UtcNow,
        };

        DbContext.PostedLocations.Add(postedLocation);

        return postedLocation;
    }

    private ReleaseCollection AddCollection(string name)
    {
        var collection = new ReleaseCollection
        {
            ReleaseGroup = new ReleaseGroup
            {
                Name = $"{name} group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
            Key = name.ToLowerInvariant(),
            Name = name,
            CreatedAt = DateTime.UtcNow,
        };

        DbContext.ReleaseCollections.Add(collection);

        return collection;
    }

    private PostedLocation AddCollectionPostedLocation(
        ReleaseCollection collection,
        string url,
        DistributionSiteRegistration? distributionSiteRegistration = null
    )
    {
        var postedLocation = new PostedLocation
        {
            ReleaseCollection = collection,
            DistributionSiteRegistration = distributionSiteRegistration,
            Url = url,
            CreatedAt = DateTime.UtcNow,
        };

        DbContext.PostedLocations.Add(postedLocation);

        return postedLocation;
    }
}

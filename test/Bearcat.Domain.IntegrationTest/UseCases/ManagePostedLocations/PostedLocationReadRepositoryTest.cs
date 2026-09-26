using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManagePostedLocations.Dto;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManagePostedLocations;

public class PostedLocationReadRepositoryTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private PostedLocationReadRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        repository = new PostedLocationReadRepository(dbContext);
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
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
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

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
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetByIdForReleaseAsync(
            release.Id,
            otherPostedLocation.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBeNull();
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

        dbContext.Releases.Add(release);

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

        dbContext.PostedLocations.Add(postedLocation);

        return postedLocation;
    }
}

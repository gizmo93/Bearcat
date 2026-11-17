using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManagePostedLocations;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManagePostedLocations;

public class PostedLocationServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private static readonly DateTime Now = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);

    private PostedLocationService service = null!;

    [SetUp]
    public void Setup()
    {
        service = new PostedLocationService(
            new PostedLocationWriteRepository(DbContext),
            new ControllableTimeProvider(Now)
        );
    }

    [Test]
    public async Task AddForReleaseIfUrlIsNewAsync_UrlIsNew_CreatesPostedLocation()
    {
        // Arrange
        var release = await AddReleaseAsync("Bearcat.Release.001");

        // Act
        var result = await service.AddForReleaseIfUrlIsNewAsync(
            release.Id,
            " https://forum.example/threads/1 ",
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.WasCreated.ShouldBeTrue();
        DbContext.ChangeTracker.Clear();
        var postedLocation = await DbContext.PostedLocations.SingleAsync();
        postedLocation.Id.ShouldBe(result.PostedLocationId);
        postedLocation.ReleaseId.ShouldBe(release.Id);
        postedLocation.Url.ShouldBe("https://forum.example/threads/1");
        postedLocation.CreatedAt.ShouldBe(Now);
        postedLocation.ContentUpdatedAt.ShouldBe(Now);
    }

    [Test]
    public async Task AddForReleaseIfUrlIsNewAsync_UrlAlreadyPostedWithSurroundingWhitespace_ReturnsExistingPostedLocation()
    {
        // Arrange
        var release = await AddReleaseAsync("Bearcat.Release.001");
        var existingPostedLocationId = await service.AddForReleaseAsync(
            release.Id,
            "https://forum.example/threads/1",
            cancellationToken: CancellationToken.None
        );

        // Act
        var result = await service.AddForReleaseIfUrlIsNewAsync(
            release.Id,
            "  https://forum.example/threads/1\t",
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.WasCreated.ShouldBeFalse();
        result.PostedLocationId.ShouldBe(existingPostedLocationId);
        DbContext.ChangeTracker.Clear();
        (await DbContext.PostedLocations.CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task AddForReleaseIfUrlIsNewAsync_UrlDiffersOnlyInCase_CreatesPostedLocation()
    {
        // Arrange
        var release = await AddReleaseAsync("Bearcat.Release.001");
        var existingPostedLocationId = await service.AddForReleaseAsync(
            release.Id,
            "https://forum.example/threads/abc",
            cancellationToken: CancellationToken.None
        );

        // Act
        var result = await service.AddForReleaseIfUrlIsNewAsync(
            release.Id,
            "https://forum.example/threads/ABC",
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.WasCreated.ShouldBeTrue();
        result.PostedLocationId.ShouldNotBe(existingPostedLocationId);
    }

    [Test]
    public async Task AddForReleaseIfUrlIsNewAsync_SameUrlPostedForOtherRelease_CreatesPostedLocation()
    {
        // Arrange
        var otherRelease = await AddReleaseAsync("Bearcat.Release.001");
        var release = await AddReleaseAsync("Bearcat.Release.002");
        var otherPostedLocationId = await service.AddForReleaseAsync(
            otherRelease.Id,
            "https://forum.example/threads/1",
            cancellationToken: CancellationToken.None
        );

        // Act
        var result = await service.AddForReleaseIfUrlIsNewAsync(
            release.Id,
            "https://forum.example/threads/1",
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.WasCreated.ShouldBeTrue();
        result.PostedLocationId.ShouldNotBe(otherPostedLocationId);
        DbContext.ChangeTracker.Clear();
        var postedLocation = await DbContext.PostedLocations.SingleAsync(location =>
            location.Id == result.PostedLocationId
        );
        postedLocation.ReleaseId.ShouldBe(release.Id);
    }

    [Test]
    public async Task AddForReleaseAsync_ForumPostingFailuresExist_DeletesOnlyFailureOfSameReleaseAndForum()
    {
        // Arrange
        var release = await AddReleaseAsync("Bearcat.Release.001");
        var otherRelease = await AddReleaseAsync("Bearcat.Release.002");
        var registration = await AddRegistrationAsync("Forum A");
        var otherRegistration = await AddRegistrationAsync("Forum B");

        DbContext.ForumPostingFailures.AddRange(
            CreateGaveUpFailure(release.Id, registration.Id),
            CreateGaveUpFailure(release.Id, otherRegistration.Id),
            CreateGaveUpFailure(otherRelease.Id, registration.Id)
        );

        await DbContext.SaveChangesAsync();

        // Act
        await service.AddForReleaseAsync(
            releaseId: release.Id,
            url: "https://forum.example/threads/1",
            distributionSiteRegistrationId: registration.Id,
            cancellationToken: CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var remainingFailures = await DbContext
            .ForumPostingFailures.Select(failure => new
            {
                failure.ReleaseId,
                failure.DistributionSiteRegistrationId,
            })
            .ToListAsync();

        remainingFailures.Count.ShouldBe(2);
        remainingFailures.ShouldContain(failure =>
            failure.ReleaseId == release.Id
            && failure.DistributionSiteRegistrationId == otherRegistration.Id
        );

        remainingFailures.ShouldContain(failure =>
            failure.ReleaseId == otherRelease.Id
            && failure.DistributionSiteRegistrationId == registration.Id
        );
    }

    private static ForumPostingFailure CreateGaveUpFailure(
        int releaseId,
        int distributionSiteRegistrationId
    )
    {
        return new ForumPostingFailure
        {
            ReleaseId = releaseId,
            DistributionSiteRegistrationId = distributionSiteRegistrationId,
            FailedAttemptCount = 5,
            LastErrorMessage = "Forum unavailable.",
            State = ForumPostingFailureState.GaveUp,
        };
    }

    private async Task<DistributionSiteRegistration> AddRegistrationAsync(string name)
    {
        var registration = new DistributionSiteRegistration
        {
            Name = name,
            DistributionSiteClassName = "GenericXenForo",
            SerializedConfig = "{}",
            IsActive = true,
        };

        DbContext.DistributionSiteRegistrations.Add(registration);
        await DbContext.SaveChangesAsync();

        return registration;
    }

    private async Task<Release> AddReleaseAsync(string name)
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
        await DbContext.SaveChangesAsync();

        return release;
    }
}

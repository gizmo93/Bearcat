using Bearcat.Domain.Entities;
using Bearcat.Domain.IntegrationTest.Shared;
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
    public async Task DeleteAsync_PostedLocationExists_DeletesOnlyThatPostedLocation()
    {
        // Arrange
        var release = await AddReleaseAsync("Bearcat.Release.001");
        var postedLocationId = await service.AddForReleaseAsync(
            release.Id,
            "https://forum.example/threads/1",
            cancellationToken: CancellationToken.None
        );
        var otherPostedLocationId = await service.AddForReleaseAsync(
            release.Id,
            "https://forum.example/threads/2",
            cancellationToken: CancellationToken.None
        );
        DbContext.ChangeTracker.Clear();

        // Act
        await service.DeleteAsync(postedLocationId, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        (await DbContext.PostedLocations.Select(location => location.Id).ToListAsync()).ShouldBe([
            otherPostedLocationId,
        ]);
        (await DbContext.Releases.SingleAsync()).Id.ShouldBe(release.Id);
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

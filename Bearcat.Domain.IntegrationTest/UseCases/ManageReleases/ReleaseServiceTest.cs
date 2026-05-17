using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleases;

public class ReleaseServiceTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private ReleaseService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        service = new ReleaseService(new ReleaseWriteRepository(dbContext));
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task CreateAsync_ValidRelease_PersistsReleaseAndReturnsId()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync("Managed releases");

        // Act
        var result = await service.CreateAsync(
            "Bearcat.Release.001",
            "/tmp/release",
            ReleaseType.Managed,
            releaseGroup.Id,
            CancellationToken.None
        );

        // Assert
        var release = await dbContext.Releases.SingleAsync();

        result.ShouldBeGreaterThan(0);
        release.ShouldNotBeNull();
        release.Id.ShouldBe(result);
        release.Name.ShouldBe("Bearcat.Release.001");
        release.ReleaseFolderPath.ShouldBe("/tmp/release");
        release.ReleaseType.ShouldBe(ReleaseType.Managed);
        release.ReleaseGroupId.ShouldBe(releaseGroup.Id);
    }

    [Test]
    public async Task UpdateAsync_ReleaseExists_UpdatesNameAndReleaseGroup()
    {
        // Arrange
        var firstGroup = await AddReleaseGroupAsync("First group");
        var secondGroup = await AddReleaseGroupAsync("Second group");
        var release = await AddReleaseAsync(firstGroup.Id);

        // Act
        await service.UpdateAsync(
            release.Id,
            "Bearcat.Release.Updated",
            secondGroup.Id,
            CancellationToken.None
        );

        // Assert
        var result = await dbContext.Releases.SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(release.Id);
        result.Name.ShouldBe("Bearcat.Release.Updated");
        result.ReleaseGroupId.ShouldBe(secondGroup.Id);
        result.ReleaseFolderPath.ShouldBe("/tmp/release");
    }

    [Test]
    public async Task UpdateReleaseGroupAsync_ReleaseIdsAreEmpty_DoesNotChangeReleases()
    {
        // Arrange
        var firstGroup = await AddReleaseGroupAsync("First group");
        var secondGroup = await AddReleaseGroupAsync("Second group");
        var release = await AddReleaseAsync(firstGroup.Id);

        // Act
        await service.UpdateReleaseGroupAsync([], secondGroup.Id, CancellationToken.None);

        // Assert
        var result = await dbContext.Releases.SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(release.Id);
        result.ReleaseGroupId.ShouldBe(firstGroup.Id);
    }

    [Test]
    public async Task UpdateReleaseGroupAsync_ReleasesExist_UpdatesReleaseGroups()
    {
        // Arrange
        var firstGroup = await AddReleaseGroupAsync("First group");
        var secondGroup = await AddReleaseGroupAsync("Second group");
        var firstRelease = await AddReleaseAsync(firstGroup.Id, "Bearcat.Release.001");
        var secondRelease = await AddReleaseAsync(firstGroup.Id, "Bearcat.Release.002");

        // Act
        await service.UpdateReleaseGroupAsync(
            [firstRelease.Id, secondRelease.Id],
            secondGroup.Id,
            CancellationToken.None
        );

        // Assert
        var result = await dbContext.Releases.OrderBy(r => r.Id).ToListAsync();

        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldAllBe(r => r.ReleaseGroupId == secondGroup.Id);
    }

    [Test]
    public async Task DeleteAsync_ReleaseExists_RemovesRelease()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync("Managed releases");
        var release = await AddReleaseAsync(releaseGroup.Id);

        // Act
        await service.DeleteAsync(release.Id, CancellationToken.None);

        // Assert
        var result = await dbContext.Releases.AnyAsync();

        result.ShouldBeFalse();
    }

    private async Task<ReleaseGroup> AddReleaseGroupAsync(string name)
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = name,
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };

        dbContext.ReleaseGroups.Add(releaseGroup);
        await dbContext.SaveChangesAsync();

        return releaseGroup;
    }

    private async Task<Release> AddReleaseAsync(
        int releaseGroupId,
        string name = "Bearcat.Release.001"
    )
    {
        var release = new Release
        {
            Name = name,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/tmp/release",
            ReleaseGroupId = releaseGroupId,
        };

        dbContext.Releases.Add(release);
        await dbContext.SaveChangesAsync();

        return release;
    }
}

using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseGroups;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleaseGroups;

public class ReleaseGroupServiceTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private ReleaseGroupService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        var repository = new ReleaseGroupRepository(dbContext, dbContext);
        service = new ReleaseGroupService(repository);
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task CreateAsync_ValidReleaseGroup_PersistsTrimmedReleaseGroupAndReturnsId()
    {
        // Arrange
        var name = "  Managed releases  ";

        // Act
        var result = await service.CreateAsync(name, true, 24, CancellationToken.None);

        // Assert
        var releaseGroup = await dbContext.ReleaseGroups.SingleAsync();

        result.ShouldBeGreaterThan(0);
        releaseGroup.ShouldNotBeNull();
        releaseGroup.Id.ShouldBe(result);
        releaseGroup.Name.ShouldBe("Managed releases");
        releaseGroup.EnableAutomaticReuploads.ShouldBeTrue();
        releaseGroup.NumberOfHoursUntilReupload.ShouldBe(24);
    }

    [Test]
    public async Task CreateAsync_NameIsBlank_ThrowsArgumentException()
    {
        // Arrange
        var name = " ";

        // Act
        var result = await Should.ThrowAsync<ArgumentException>(async () =>
            await service.CreateAsync(name, false, 24, CancellationToken.None)
        );

        // Assert
        result.ShouldNotBeNull();
        result.Message.ShouldContain("Name is required.");
    }

    [Test]
    public async Task CreateAsync_NumberOfHoursUntilReuploadIsNegative_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var numberOfHoursUntilReupload = -1;

        // Act
        var result = await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
            await service.CreateAsync(
                "Managed releases",
                false,
                numberOfHoursUntilReupload,
                CancellationToken.None
            )
        );

        // Assert
        result.ShouldNotBeNull();
        result.ParamName.ShouldBe("numberOfHoursUntilReupload");
    }

    [Test]
    public async Task UpdateAsync_ReleaseGroupExists_UpdatesReleaseGroup()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync();

        // Act
        await service.UpdateAsync(
            releaseGroup.Id,
            "  Updated releases  ",
            true,
            48,
            CancellationToken.None
        );

        // Assert
        var result = await dbContext.ReleaseGroups.SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(releaseGroup.Id);
        result.Name.ShouldBe("Updated releases");
        result.EnableAutomaticReuploads.ShouldBeTrue();
        result.NumberOfHoursUntilReupload.ShouldBe(48);
    }

    [Test]
    public async Task DeleteAsync_ReleaseGroupHasNoAssignedReleases_RemovesReleaseGroup()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync();

        // Act
        await service.DeleteAsync(releaseGroup.Id, CancellationToken.None);

        // Assert
        var result = await dbContext.ReleaseGroups.AnyAsync();

        result.ShouldBeFalse();
    }

    [Test]
    public async Task DeleteAsync_ReleaseGroupHasAssignedReleases_ThrowsInvalidOperationException()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync();
        dbContext.Releases.Add(
            new Release
            {
                Name = "Bearcat.Release.001",
                ReleaseType = ReleaseType.Managed,
                ReleaseFolderPath = "/tmp/release",
                ReleaseGroupId = releaseGroup.Id,
            }
        );
        await dbContext.SaveChangesAsync();

        // Act
        var result = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.DeleteAsync(releaseGroup.Id, CancellationToken.None)
        );

        // Assert
        result.ShouldNotBeNull();
        result.Message.ShouldBe("Release groups with assigned releases cannot be deleted.");
    }

    private async Task<ReleaseGroup> AddReleaseGroupAsync()
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = "Managed releases",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };

        dbContext.ReleaseGroups.Add(releaseGroup);
        await dbContext.SaveChangesAsync();

        return releaseGroup;
    }
}

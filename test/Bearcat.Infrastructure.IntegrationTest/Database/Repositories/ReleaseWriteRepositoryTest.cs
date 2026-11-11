using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class ReleaseWriteRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    [TestCase("Bearcat.Release.1080p-GRP", true)]
    [TestCase("BEARCAT.release.1080P-grp", true)]
    [TestCase("Bearcat.Release.720p-GRP", false)]
    public async Task NameExistsAsync_ExistingRelease_ReturnsWhetherNameIsUsedIgnoringCase(
        string name,
        bool expectedResult
    )
    {
        // Arrange
        await AddReleaseAsync("Bearcat.Release.1080p-GRP");

        // Act
        var result = await new ReleaseWriteRepository(CreateDbContext()).NameExistsAsync(
            name,
            excludedReleaseId: null,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(expectedResult);
    }

    [Test]
    public async Task NameExistsAsync_NameOnlyUsedByExcludedRelease_ReturnsFalse()
    {
        // Arrange
        var release = await AddReleaseAsync("Bearcat.Release.1080p-GRP");

        // Act
        var result = await new ReleaseWriteRepository(CreateDbContext()).NameExistsAsync(
            "bearcat.release.1080p-grp",
            release.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBeFalse();
    }

    private async Task<Release> AddReleaseAsync(string name)
    {
        var release = new Release
        {
            Name = name,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = $"/releases/{name}",
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

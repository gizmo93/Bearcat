using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleases;

public class ReleaseClassificationServiceTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private ReleaseClassificationService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        service = new ReleaseClassificationService(
            new ReleaseClassificationRepository(dbContext),
            CreateTimeProvider(),
            NullLogger<ReleaseClassificationService>.Instance
        );
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task ClassifyAsync_ReleaseWithoutMedia_PersistsClassificationFromName()
    {
        // Arrange
        var release = await AddReleaseAsync("Amok.1994.German.1080p.BluRay.x264-PL3X");

        // Act
        var classified = await service.ClassifyAsync(release.Id);

        // Assert
        classified.ShouldBeTrue();
        var stored = await GetClassificationAsync(release.Id);
        stored.ShouldNotBeNull();
        stored.Title.ShouldBe("Amok");
        stored.Year.ShouldBe(1994);
        stored.Resolution.ShouldBe(ReleaseResolution.R1080p);
        stored.ResolutionSource.ShouldBe(ClassificationSource.ReleaseName);
        stored.PrimaryLanguage.ShouldBe("German");
    }

    [Test]
    public async Task ClassifyAsync_CalledTwice_UpdatesInPlaceWithoutDuplicate()
    {
        // Arrange
        var release = await AddReleaseAsync("Amok.1994.German.1080p.BluRay.x264-PL3X");

        // Act
        await service.ClassifyAsync(release.Id);
        await service.ClassifyAsync(release.Id);

        // Assert
        var count = await dbContext.ReleaseClassifications.CountAsync(classification =>
            classification.ReleaseId == release.Id
        );
        count.ShouldBe(1);
    }

    [Test]
    public async Task ProcessPendingClassificationsAsync_UnclassifiedRelease_ClassifiesIt()
    {
        // Arrange
        var release = await AddReleaseAsync("Black.Diamond.2025.German.BDRip.x264-CPTN");

        // Act
        var classifiedCount = await service.ProcessPendingClassificationsAsync();

        // Assert
        classifiedCount.ShouldBeGreaterThanOrEqualTo(1);
        var stored = await GetClassificationAsync(release.Id);
        stored.ShouldNotBeNull();
        stored.PrimaryLanguage.ShouldBe("German");
        stored.Resolution.ShouldBe(ReleaseResolution.Unknown);
    }

    private async Task<ReleaseClassification?> GetClassificationAsync(int releaseId)
    {
        return await dbContext
            .ReleaseClassifications.AsNoTracking()
            .FirstOrDefaultAsync(classification => classification.ReleaseId == releaseId);
    }

    private async Task<Release> AddReleaseAsync(string name)
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = $"Release group {Guid.NewGuid():N}",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
            Releases = [],
        };
        dbContext.ReleaseGroups.Add(releaseGroup);
        await dbContext.SaveChangesAsync();

        var release = new Release
        {
            Name = name,
            CreatedAt = DateTime.UtcNow,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = $"/tmp/{name}",
            ReleaseGroupId = releaseGroup.Id,
            ArchiveConfigs = [],
            UploadConfigs = [],
        };
        dbContext.Releases.Add(release);
        await dbContext.SaveChangesAsync();

        return release;
    }

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }
}

using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.ReleaseNameParsing;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
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

public class ReleaseClassificationServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private ReleaseClassificationService service = null!;

    [SetUp]
    public void Setup()
    {
        service = new ReleaseClassificationService(
            new ReleaseClassificationRepository(DbContext),
            CreateTimeProvider(),
            NullLogger<ReleaseClassificationService>.Instance
        );
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
        var count = await DbContext.ReleaseClassifications.CountAsync(classification =>
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

    [Test]
    public async Task ProcessPendingClassificationsAsync_OtherWorkerClassifiedReleaseFirst_KeepsExistingClassificationWithoutDuplicate()
    {
        // Arrange
        var release = await AddReleaseAsync("Amok.1994.German.1080p.BluRay.x264-PL3X");
        var racingService = new ReleaseClassificationService(
            new ReleaseClassificationRepositoryWithConcurrentWorker(
                new ReleaseClassificationRepository(DbContext),
                () => AddClassificationFromOtherWorkerAsync(release.Id)
            ),
            CreateTimeProvider(),
            NullLogger<ReleaseClassificationService>.Instance
        );
        DbContext.ChangeTracker.Clear();

        // Act
        var classifiedCount = await racingService.ProcessPendingClassificationsAsync();

        // Assert
        classifiedCount.ShouldBe(1);
        var classifications = await DbContext
            .ReleaseClassifications.AsNoTracking()
            .Where(classification => classification.ReleaseId == release.Id)
            .ToListAsync();
        classifications.ShouldHaveSingleItem().Title.ShouldBe("Classified by other worker");
    }

    private async Task AddClassificationFromOtherWorkerAsync(int releaseId)
    {
        var otherWorkerDbContext = CreateDbContext();
        var classification = ReleaseClassificationBuilder.Build(
            releaseName: "Amok.1994.German.1080p.BluRay.x264-PL3X",
            mediaFiles: [],
            contentKind: null,
            nfoContent: null
        );
        classification.ReleaseId = releaseId;
        classification.Title = "Classified by other worker";
        classification.ClassifiedAt = DateTime.UtcNow;
        otherWorkerDbContext.ReleaseClassifications.Add(classification);
        await otherWorkerDbContext.SaveChangesAsync();
    }

    private async Task<ReleaseClassification?> GetClassificationAsync(int releaseId)
    {
        return await DbContext
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
        DbContext.ReleaseGroups.Add(releaseGroup);
        await DbContext.SaveChangesAsync();

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
        DbContext.Releases.Add(release);
        await DbContext.SaveChangesAsync();

        return release;
    }

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }

    private sealed class ReleaseClassificationRepositoryWithConcurrentWorker(
        IReleaseClassificationRepository repository,
        Func<Task> runConcurrentWorkerBeforeFirstSave
    ) : IReleaseClassificationRepository
    {
        private bool concurrentWorkerHasRun;

        public Task<Release?> GetReleaseForClassificationAsync(
            int releaseId,
            CancellationToken cancellationToken = default
        ) => repository.GetReleaseForClassificationAsync(releaseId, cancellationToken);

        public Task<IReadOnlyList<Release>> GetReleasesNeedingClassificationAsync(
            int count,
            int parserVersion,
            HashSet<int> excludedReleaseIds,
            CancellationToken cancellationToken = default
        ) =>
            repository.GetReleasesNeedingClassificationAsync(
                count,
                parserVersion,
                excludedReleaseIds,
                cancellationToken
            );

        public void ClearChangeTracker() => repository.ClearChangeTracker();

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!concurrentWorkerHasRun)
            {
                concurrentWorkerHasRun = true;
                await runConcurrentWorkerBeforeFirstSave();
            }

            await repository.SaveChangesAsync(cancellationToken);
        }
    }
}

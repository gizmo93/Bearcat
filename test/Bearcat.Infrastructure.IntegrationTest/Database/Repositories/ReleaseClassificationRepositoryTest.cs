using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class ReleaseClassificationRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const int ParserVersion = 3;

    private ReleaseClassificationRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        repository = new ReleaseClassificationRepository(DbContext);
    }

    [Test]
    public async Task GetReleasesNeedingClassificationAsync_ReleasesWithAndWithoutCurrentClassification_ReturnsReleasesWithoutCurrentClassification()
    {
        // Arrange
        var unclassifiedRelease = await AddReleaseAsync(
            "Unclassified",
            classificationParserVersion: null
        );
        var outdatedRelease = await AddReleaseAsync(
            "Outdated",
            classificationParserVersion: ParserVersion - 1
        );
        await AddReleaseAsync("Current", classificationParserVersion: ParserVersion);

        // Act
        var result = await repository.GetReleasesNeedingClassificationAsync(
            count: 100,
            parserVersion: ParserVersion,
            afterReleaseId: 0,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result
            .Select(release => release.Id)
            .ShouldBe([unclassifiedRelease.Id, outdatedRelease.Id]);
    }

    [Test]
    public async Task GetReleasesNeedingClassificationAsync_AfterReleaseIdAndCount_ReturnsReleasesWithLowestIdsAfterGivenReleaseId()
    {
        // Arrange
        var firstRelease = await AddReleaseAsync("First", classificationParserVersion: null);
        var secondRelease = await AddReleaseAsync("Second", classificationParserVersion: null);
        var thirdRelease = await AddReleaseAsync("Third", classificationParserVersion: null);
        await AddReleaseAsync("Fourth", classificationParserVersion: null);

        // Act
        var result = await repository.GetReleasesNeedingClassificationAsync(
            count: 2,
            parserVersion: ParserVersion,
            afterReleaseId: firstRelease.Id,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.Select(release => release.Id).ShouldBe([secondRelease.Id, thirdRelease.Id]);
    }

    private async Task<Release> AddReleaseAsync(string name, int? classificationParserVersion)
    {
        var release = new Release
        {
            Name = name,
            ReleaseType = ReleaseType.Unmanaged,
            CreatedAt = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc),
            ReleaseGroup = new ReleaseGroup
            {
                Name = $"{name} group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
            Classification = classificationParserVersion is null
                ? null
                : new ReleaseClassification
                {
                    Title = name,
                    ParserVersion = classificationParserVersion.Value,
                    ContentType = ReleaseContentType.Movie,
                },
        };

        DbContext.Releases.Add(release);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return release;
    }
}

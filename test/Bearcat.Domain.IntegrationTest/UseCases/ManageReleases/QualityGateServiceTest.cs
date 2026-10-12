using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.QualityGate;
using Bearcat.Domain.Shared.QualityGate.Checks;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.FileSystem;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleases;

public class QualityGateServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private QualityGateService service = null!;

    [SetUp]
    public void Setup()
    {
        service = new QualityGateService(
            new QualityGateRepository(DbContext),
            new QualityGateEvaluator(
                [new RequiredReleaseInfoQualityCheck()],
                new FileSystemService()
            ),
            CreateTimeProvider()
        );
    }

    [Test]
    public async Task ReevaluatePendingReleasesAsync_MorePendingReleasesThanOneBatchAndFirstBatchStaysFailed_EvaluatesReleaseOfSecondBatch()
    {
        // Arrange
        var releaseGroup = CreateReleaseGroupRequiringNfo();
        var failingReleases = Enumerable
            .Range(1, 500)
            .Select(releaseNumber => CreateRelease(releaseGroup, releaseNumber, withNfo: false))
            .ToList();
        var passingRelease = CreateRelease(releaseGroup, 501, withNfo: true);
        await AddReleasesAsync([.. failingReleases, passingRelease]);

        // Act
        await service.ReevaluatePendingReleasesAsync(CancellationToken.None);

        // Assert
        await ShouldHaveStateAsync(passingRelease, QualityGateState.Passed, issueCount: 0);
        await ShouldHaveStateAsync(failingReleases[0], QualityGateState.Failed, issueCount: 1);
        await ShouldHaveStateAsync(failingReleases[^1], QualityGateState.Failed, issueCount: 1);
    }

    [Test]
    public async Task ReevaluatePendingReleasesAsync_MorePendingReleasesThanOneBatchAndFirstBatchPasses_EvaluatesReleaseOfSecondBatch()
    {
        // Arrange
        var releaseGroup = CreateReleaseGroupRequiringNfo();
        var passingReleases = Enumerable
            .Range(1, 500)
            .Select(releaseNumber => CreateRelease(releaseGroup, releaseNumber, withNfo: true))
            .ToList();
        var failingRelease = CreateRelease(releaseGroup, 501, withNfo: false);
        await AddReleasesAsync([.. passingReleases, failingRelease]);

        // Act
        await service.ReevaluatePendingReleasesAsync(CancellationToken.None);

        // Assert
        await ShouldHaveStateAsync(failingRelease, QualityGateState.Failed, issueCount: 1);
        await ShouldHaveStateAsync(passingReleases[0], QualityGateState.Passed, issueCount: 0);
        await ShouldHaveStateAsync(passingReleases[^1], QualityGateState.Passed, issueCount: 0);
    }

    [Test]
    public async Task ReevaluatePendingReleasesAsync_ManuallyApprovedRelease_KeepsReleaseApproved()
    {
        // Arrange
        var releaseGroup = CreateReleaseGroupRequiringNfo();
        var release = CreateRelease(releaseGroup, 1, withNfo: false);
        release.QualityGateState = QualityGateState.ManuallyApproved;
        await AddReleasesAsync([release]);

        // Act
        await service.ReevaluatePendingReleasesAsync(CancellationToken.None);

        // Assert
        await ShouldHaveStateAsync(release, QualityGateState.ManuallyApproved, issueCount: 0);
    }

    private async Task AddReleasesAsync(IReadOnlyList<Release> releases)
    {
        DbContext.Releases.AddRange(releases);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
    }

    private async Task ShouldHaveStateAsync(
        Release release,
        QualityGateState expectedState,
        int issueCount
    )
    {
        DbContext.ChangeTracker.Clear();
        var result = await DbContext
            .Releases.Include(r => r.QualityIssues)
            .SingleAsync(r => r.Id == release.Id);

        result.QualityGateState.ShouldBe(expectedState);
        result.QualityIssues.Count.ShouldBe(issueCount);
    }

    private static ReleaseGroup CreateReleaseGroupRequiringNfo()
    {
        return new ReleaseGroup
        {
            Name = "Movies",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
            QualityProfile = new QualityProfile
            {
                Name = "Require NFO",
                Rules =
                [
                    new QualityCheckRule
                    {
                        RuleType = QualityCheckRuleType.RequiredReleaseInfo,
                        ParametersJson = QualityCheckParameterValues.Serialize(
                            new Dictionary<string, object?>
                            {
                                ["requireCover"] = false,
                                ["requireDescription"] = false,
                                ["requireNfo"] = true,
                            }
                        ),
                    },
                ],
            },
        };
    }

    private static Release CreateRelease(ReleaseGroup releaseGroup, int releaseNumber, bool withNfo)
    {
        var name = $"Bearcat.Release.{releaseNumber:D3}";

        return new Release
        {
            Name = name,
            CreatedAt = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc),
            ReleaseType = ReleaseType.Unmanaged,
            ReleaseGroup = releaseGroup,
            QualityGateState = QualityGateState.NotEvaluated,
            ReleaseNfo = withNfo
                ? new ReleaseNfo { FileName = $"{name}.nfo", Content = "nfo" }
                : null,
        };
    }

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }
}

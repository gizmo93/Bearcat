using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.Shared.QualityGate;

public class QualityGateResetRepositoryTest : BearcatIntegrationTest
{
    private static readonly DateTime EvaluatedAt = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

    private BearcatDbContext dbContext = null!;
    private QualityGateResetRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        repository = new QualityGateResetRepository(dbContext);
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task ResetForQualityProfileAsync_ReleasesOfAssignedGroups_ResetsStateAndDeletesIssues()
    {
        // Arrange
        var profile = CreateProfile();
        var releaseGroup = CreateReleaseGroup(profile);
        var failedRelease = AddRelease(releaseGroup, "Failed", QualityGateState.Failed);
        var passedRelease = AddRelease(releaseGroup, "Passed", QualityGateState.Passed);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Act
        await repository.ResetForQualityProfileAsync(profile.Id, CancellationToken.None);

        // Assert
        var releases = await dbContext
            .Releases.Include(r => r.QualityIssues)
            .Where(r => r.Id == failedRelease.Id || r.Id == passedRelease.Id)
            .ToListAsync();

        releases.Count.ShouldBe(2);
        releases.ShouldAllBe(r =>
            r.QualityGateState == QualityGateState.NotEvaluated
            && r.QualityGateEvaluatedAt == null
            && r.QualityIssues.Count == 0
        );
        (await dbContext.ReleaseQualityIssues.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task ResetForQualityProfileAsync_ManuallyApprovedRelease_LeavesReleaseUntouched()
    {
        // Arrange
        var profile = CreateProfile();
        var releaseGroup = CreateReleaseGroup(profile);
        var approvedRelease = AddRelease(
            releaseGroup,
            "Approved",
            QualityGateState.ManuallyApproved
        );
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Act
        await repository.ResetForQualityProfileAsync(profile.Id, CancellationToken.None);

        // Assert
        var result = await dbContext
            .Releases.Include(r => r.QualityIssues)
            .SingleAsync(r => r.Id == approvedRelease.Id);

        result.QualityGateState.ShouldBe(QualityGateState.ManuallyApproved);
        result.QualityGateEvaluatedAt.ShouldBe(EvaluatedAt);
        result.QualityIssues.ShouldHaveSingleItem();
    }

    [Test]
    public async Task ResetForQualityProfileAsync_ReleaseOfGroupWithOtherProfile_LeavesReleaseUntouched()
    {
        // Arrange
        var profile = CreateProfile();
        var otherReleaseGroup = CreateReleaseGroup(CreateProfile());
        var otherRelease = AddRelease(otherReleaseGroup, "Other", QualityGateState.Failed);
        dbContext.QualityProfiles.Add(profile);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Act
        await repository.ResetForQualityProfileAsync(profile.Id, CancellationToken.None);

        // Assert
        var result = await dbContext
            .Releases.Include(r => r.QualityIssues)
            .SingleAsync(r => r.Id == otherRelease.Id);

        result.QualityGateState.ShouldBe(QualityGateState.Failed);
        result.QualityGateEvaluatedAt.ShouldBe(EvaluatedAt);
        result.QualityIssues.ShouldHaveSingleItem();
    }

    [Test]
    public async Task ResetForReleaseGroupAsync_ReleasesOfGroup_ResetsStateAndDeletesIssues()
    {
        // Arrange
        var releaseGroup = CreateReleaseGroup(qualityProfile: null);
        var failedRelease = AddRelease(releaseGroup, "Failed", QualityGateState.Failed);
        var approvedRelease = AddRelease(
            releaseGroup,
            "Approved",
            QualityGateState.ManuallyApproved
        );
        var otherRelease = AddRelease(
            CreateReleaseGroup(qualityProfile: null),
            "Other",
            QualityGateState.Failed
        );
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Act
        await repository.ResetForReleaseGroupAsync(releaseGroup.Id, CancellationToken.None);

        // Assert
        var releases = await dbContext.Releases.Include(r => r.QualityIssues).ToListAsync();
        var reset = releases.Single(r => r.Id == failedRelease.Id);
        var approved = releases.Single(r => r.Id == approvedRelease.Id);
        var other = releases.Single(r => r.Id == otherRelease.Id);

        reset.QualityGateState.ShouldBe(QualityGateState.NotEvaluated);
        reset.QualityGateEvaluatedAt.ShouldBeNull();
        reset.QualityIssues.ShouldBeEmpty();
        approved.QualityGateState.ShouldBe(QualityGateState.ManuallyApproved);
        approved.QualityGateEvaluatedAt.ShouldBe(EvaluatedAt);
        approved.QualityIssues.ShouldHaveSingleItem();
        other.QualityGateState.ShouldBe(QualityGateState.Failed);
        other.QualityGateEvaluatedAt.ShouldBe(EvaluatedAt);
        other.QualityIssues.ShouldHaveSingleItem();
    }

    private static QualityProfile CreateProfile() =>
        new()
        {
            Name = "Require media info",
            Rules =
            [
                new QualityCheckRule
                {
                    RuleType = QualityCheckRuleType.MediaInfoPresent,
                    ParametersJson = "{}",
                },
            ],
        };

    private static ReleaseGroup CreateReleaseGroup(QualityProfile? qualityProfile) =>
        new()
        {
            Name = "Managed releases",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
            QualityProfile = qualityProfile,
        };

    private Release AddRelease(ReleaseGroup releaseGroup, string name, QualityGateState state)
    {
        var release = new Release
        {
            Name = name,
            CreatedAt = EvaluatedAt,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = $"/tmp/{name}",
            ReleaseGroup = releaseGroup,
            QualityGateState = state,
            QualityGateEvaluatedAt = EvaluatedAt,
            QualityIssues =
            [
                new ReleaseQualityIssue
                {
                    RuleType = QualityCheckRuleType.MediaInfoPresent,
                    Description = "No media info has been extracted",
                },
            ],
        };

        dbContext.Releases.Add(release);

        return release;
    }
}

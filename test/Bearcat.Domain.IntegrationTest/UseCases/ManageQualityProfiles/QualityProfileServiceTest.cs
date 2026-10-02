using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageQualityProfiles;
using Bearcat.Domain.UseCases.ManageQualityProfiles.Dto;
using Bearcat.Domain.UseCases.ManageQualityProfiles.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageQualityProfiles;

public class QualityProfileServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string NfoPatternParameters = "{\"Pattern\": \"*.nfo\"}";
    private const string MinimumSizeParameters = "{\"MinimumSizeMb\": 700}";
    private const string ReleaseInfoParameters = "{\"Fields\": [\"VideoType\", \"AudioType\"]}";

    private QualityProfileRepository repository = null!;
    private QualityProfileService service = null!;

    [SetUp]
    public void Setup()
    {
        repository = new QualityProfileRepository(DbContext, DbContext);
        service = new QualityProfileService(repository, new QualityGateResetRepository(DbContext));
    }

    [Test]
    public async Task CreateAsync_ProfileWithRules_PersistsTrimmedNameAndRules()
    {
        // Act
        var profileId = await service.CreateAsync(
            "  Movies  ",
            [
                new QualityCheckRuleInput(
                    QualityCheckRuleType.FilePatternPresent,
                    NfoPatternParameters
                ),
                new QualityCheckRuleInput(QualityCheckRuleType.MediaInfoPresent, "{}"),
            ],
            CancellationToken.None
        );

        // Assert
        var profile = await CreateDbContext()
            .QualityProfiles.Include(p => p.Rules)
            .SingleAsync(p => p.Id == profileId);
        profile.Name.ShouldBe("Movies");
        profile
            .Rules.OrderBy(r => r.Id)
            .Select(r => (r.RuleType, r.ParametersJson))
            .ShouldBe([
                (QualityCheckRuleType.FilePatternPresent, NfoPatternParameters),
                (QualityCheckRuleType.MediaInfoPresent, "{}"),
            ]);
    }

    [Test]
    public async Task GetAllAsync_ProfilesWithRulesAndGroups_ReturnsCountsOrderedByName()
    {
        // Arrange
        var seriesProfile = await AddProfileAsync(
            "Series",
            (QualityCheckRuleType.MediaInfoPresent, "{}")
        );
        var emptyProfile = await AddProfileAsync("Empty");
        var moviesProfile = await AddProfileAsync(
            "Movies",
            (QualityCheckRuleType.FilePatternPresent, NfoPatternParameters),
            (QualityCheckRuleType.MinimumFolderSize, MinimumSizeParameters),
            (QualityCheckRuleType.RequiredReleaseInfo, ReleaseInfoParameters)
        );
        await AddReleaseGroupAsync("Movie group one", moviesProfile.Id);
        await AddReleaseGroupAsync("Movie group two", moviesProfile.Id);
        await AddReleaseGroupAsync("Series group", seriesProfile.Id);
        await AddReleaseGroupAsync("Unassigned group", null);

        // Act
        var result = await repository.GetAllAsync(CancellationToken.None);

        // Assert
        result.ShouldBe([
            new QualityProfileReadModel(emptyProfile.Id, "Empty", 0, 0),
            new QualityProfileReadModel(moviesProfile.Id, "Movies", 3, 2),
            new QualityProfileReadModel(seriesProfile.Id, "Series", 1, 1),
        ]);
    }

    [Test]
    public async Task GetDetailAsync_SeveralProfilesWithRules_ReturnsRulesOfRequestedProfileOrderedById()
    {
        // Arrange
        await AddProfileAsync(
            "Series",
            (QualityCheckRuleType.MediaInfoPresent, "{}"),
            (QualityCheckRuleType.FilePatternPresent, NfoPatternParameters)
        );
        var moviesProfile = await AddProfileAsync(
            "Movies",
            (QualityCheckRuleType.RequiredReleaseInfo, ReleaseInfoParameters),
            (QualityCheckRuleType.MinimumFolderSize, MinimumSizeParameters),
            (QualityCheckRuleType.FilePatternPresent, NfoPatternParameters)
        );
        await AddProfileAsync("Empty");

        // Act
        var result = await repository.GetDetailAsync(moviesProfile.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(moviesProfile.Id);
        result.Name.ShouldBe("Movies");
        result.Rules.ShouldBe([
            new QualityCheckRuleReadModel(
                QualityCheckRuleType.RequiredReleaseInfo,
                ReleaseInfoParameters
            ),
            new QualityCheckRuleReadModel(
                QualityCheckRuleType.MinimumFolderSize,
                MinimumSizeParameters
            ),
            new QualityCheckRuleReadModel(
                QualityCheckRuleType.FilePatternPresent,
                NfoPatternParameters
            ),
        ]);
    }

    [Test]
    public async Task GetDetailAsync_ProfileWithoutRules_ReturnsEmptyRules()
    {
        // Arrange
        await AddProfileAsync("Movies", (QualityCheckRuleType.MediaInfoPresent, "{}"));
        var emptyProfile = await AddProfileAsync("Empty");

        // Act
        var result = await repository.GetDetailAsync(emptyProfile.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Empty");
        result.Rules.ShouldBeEmpty();
    }

    [Test]
    public async Task GetDetailAsync_ProfileDoesNotExist_ReturnsNull()
    {
        // Arrange
        var profile = await AddProfileAsync(
            "Movies",
            (QualityCheckRuleType.MediaInfoPresent, "{}")
        );

        // Act
        var result = await repository.GetDetailAsync(profile.Id + 1, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task UpdateAsync_ProfileWithRules_ReplacesRulesAndResetsReleasesOfAssignedGroups()
    {
        // Arrange
        var otherProfile = await AddProfileAsync(
            "Series",
            (QualityCheckRuleType.MediaInfoPresent, "{}")
        );
        var profile = await AddProfileAsync(
            "Movies",
            (QualityCheckRuleType.FilePatternPresent, NfoPatternParameters),
            (QualityCheckRuleType.MinimumFolderSize, MinimumSizeParameters)
        );
        var releaseGroup = await AddReleaseGroupAsync("Movie group", profile.Id);
        var release = await AddReleaseAsync(releaseGroup.Id, QualityGateState.Passed);

        // Act
        await service.UpdateAsync(
            profile.Id,
            " Movies HD ",
            [
                new QualityCheckRuleInput(
                    QualityCheckRuleType.RequiredReleaseInfo,
                    ReleaseInfoParameters
                ),
            ],
            CancellationToken.None
        );

        // Assert
        var assertContext = CreateDbContext();
        var rules = await assertContext
            .QualityCheckRules.OrderBy(r => r.Id)
            .Select(r => new { r.QualityProfileId, r.RuleType })
            .ToListAsync();
        rules.ShouldBe([
            new
            {
                QualityProfileId = otherProfile.Id,
                RuleType = QualityCheckRuleType.MediaInfoPresent,
            },
            new
            {
                QualityProfileId = profile.Id,
                RuleType = QualityCheckRuleType.RequiredReleaseInfo,
            },
        ]);
        var storedProfile = await assertContext.QualityProfiles.SingleAsync(p =>
            p.Id == profile.Id
        );
        storedProfile.Name.ShouldBe("Movies HD");
        var storedRelease = await assertContext.Releases.SingleAsync(r => r.Id == release.Id);
        storedRelease.QualityGateState.ShouldBe(QualityGateState.NotEvaluated);
    }

    [Test]
    public async Task DeleteAsync_ProfileAssignedToReleaseGroup_DeletesRulesAndUnassignsGroup()
    {
        // Arrange
        var otherProfile = await AddProfileAsync(
            "Series",
            (QualityCheckRuleType.MediaInfoPresent, "{}")
        );
        var profile = await AddProfileAsync(
            "Movies",
            (QualityCheckRuleType.FilePatternPresent, NfoPatternParameters),
            (QualityCheckRuleType.MinimumFolderSize, MinimumSizeParameters)
        );
        var releaseGroup = await AddReleaseGroupAsync("Movie group", profile.Id);
        var otherReleaseGroup = await AddReleaseGroupAsync("Series group", otherProfile.Id);
        var release = await AddReleaseAsync(releaseGroup.Id, QualityGateState.Failed);

        // Act
        await service.DeleteAsync(profile.Id, CancellationToken.None);

        // Assert
        var assertContext = CreateDbContext();
        (await assertContext.QualityProfiles.Select(p => p.Id).ToListAsync()).ShouldBe([
            otherProfile.Id,
        ]);
        (
            await assertContext.QualityCheckRules.Select(r => r.QualityProfileId).ToListAsync()
        ).ShouldBe([otherProfile.Id]);
        var storedGroups = await assertContext
            .ReleaseGroups.Where(g => g.Id == releaseGroup.Id || g.Id == otherReleaseGroup.Id)
            .OrderBy(g => g.Id)
            .Select(g => new { g.Id, g.QualityProfileId })
            .ToListAsync();
        storedGroups.ShouldBe([
            new { releaseGroup.Id, QualityProfileId = (int?)null },
            new { otherReleaseGroup.Id, QualityProfileId = (int?)otherProfile.Id },
        ]);
        var storedRelease = await assertContext.Releases.SingleAsync(r => r.Id == release.Id);
        storedRelease.QualityGateState.ShouldBe(QualityGateState.NotEvaluated);
    }

    private async Task<QualityProfile> AddProfileAsync(
        string name,
        params (QualityCheckRuleType RuleType, string ParametersJson)[] rules
    )
    {
        var profile = new QualityProfile
        {
            Name = name,
            Rules = rules
                .Select(rule => new QualityCheckRule
                {
                    RuleType = rule.RuleType,
                    ParametersJson = rule.ParametersJson,
                })
                .ToList(),
        };

        DbContext.QualityProfiles.Add(profile);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return profile;
    }

    private async Task<ReleaseGroup> AddReleaseGroupAsync(string name, int? qualityProfileId)
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = name,
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
            QualityProfileId = qualityProfileId,
        };

        DbContext.ReleaseGroups.Add(releaseGroup);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return releaseGroup;
    }

    private async Task<Release> AddReleaseAsync(int releaseGroupId, QualityGateState state)
    {
        var release = new Release
        {
            Name = "Bearcat.Movie.2026-GRP",
            CreatedAt = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc),
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/tmp/release",
            ReleaseGroupId = releaseGroupId,
            QualityGateState = state,
            QualityGateEvaluatedAt = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc),
        };

        DbContext.Releases.Add(release);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return release;
    }
}

using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class QualityGateRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private QualityGateRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        repository = new QualityGateRepository(DbContext);
    }

    [Test]
    public async Task GetPendingReleasesAsync_FailedRelease_IncludesRelease()
    {
        // Arrange
        var release = await AddReleaseAsync(QualityGateState.Failed);

        // Act
        var result = await repository.GetPendingReleasesAsync(CancellationToken.None);

        // Assert
        result.Select(r => r.Id).ShouldBe([release.Id]);
    }

    [Test]
    public async Task GetPendingReleasesAsync_FailedReleaseWithUploadInProgress_IncludesRelease()
    {
        // Arrange
        var release = await AddReleaseAsync(
            QualityGateState.Failed,
            uploads: [(OnlineState.Unknown, UploadState.WaitingForArchive)]
        );

        // Act
        var result = await repository.GetPendingReleasesAsync(CancellationToken.None);

        // Assert
        result.Select(r => r.Id).ShouldBe([release.Id]);
    }

    [Test]
    public async Task GetPendingReleasesAsync_FailedReleaseWithoutProfile_IncludesRelease()
    {
        // Arrange
        var release = await AddReleaseAsync(QualityGateState.Failed, withProfile: false);

        // Act
        var result = await repository.GetPendingReleasesAsync(CancellationToken.None);

        // Assert
        result.Select(r => r.Id).ShouldBe([release.Id]);
    }

    [Test]
    public async Task GetPendingReleasesAsync_NotEvaluatedRelease_IncludesRelease()
    {
        // Arrange
        var release = await AddReleaseAsync(QualityGateState.NotEvaluated);

        // Act
        var result = await repository.GetPendingReleasesAsync(CancellationToken.None);

        // Assert
        result.Select(r => r.Id).ShouldBe([release.Id]);
    }

    [TestCase(QualityGateState.Failed)]
    [TestCase(QualityGateState.NotEvaluated)]
    public async Task GetPendingReleasesAsync_UnmanagedRelease_IncludesRelease(
        QualityGateState state
    )
    {
        // Arrange
        var release = await AddReleaseAsync(state, releaseType: ReleaseType.Unmanaged);

        // Act
        var result = await repository.GetPendingReleasesAsync(CancellationToken.None);

        // Assert
        result.Select(r => r.Id).ShouldBe([release.Id]);
    }

    [TestCase(QualityGateState.Passed)]
    [TestCase(QualityGateState.ManuallyApproved)]
    public async Task GetPendingReleasesAsync_PassedOrApprovedRelease_ExcludesRelease(
        QualityGateState state
    )
    {
        // Arrange
        await AddReleaseAsync(state);

        // Act
        var result = await repository.GetPendingReleasesAsync(CancellationToken.None);

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetForEvaluationAsync_SeveralReleasesWithDetails_LoadsRequestedReleaseWithAllIncludes()
    {
        // Arrange
        await AddEvaluationReleaseAsync("Bearcat.Other.2026-GRP", "Other profile");
        var release = await AddEvaluationReleaseAsync("Bearcat.Movie.2026-GRP", "Movie profile");

        // Act
        var result = await repository.GetForEvaluationAsync(release.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Bearcat.Movie.2026-GRP");
        result.ReleaseGroup.QualityProfile.ShouldNotBeNull();
        result.ReleaseGroup.QualityProfile.Name.ShouldBe("Movie profile");
        result
            .ReleaseGroup.QualityProfile.Rules.Select(rule => rule.RuleType)
            .ShouldBe(
                [QualityCheckRuleType.MediaInfoPresent, QualityCheckRuleType.FilePatternPresent],
                ignoreOrder: true
            );
        result.ReleaseInfo.ShouldNotBeNull();
        result.ReleaseInfo.ReleaseName.ShouldBe("Bearcat.Movie.2026-GRP info");
        result.Metadata.ShouldNotBeNull();
        result.Metadata.Title.ShouldBe("Bearcat.Movie.2026-GRP title");
        result.ReleaseNfo.ShouldNotBeNull();
        result.ReleaseNfo.Content.ShouldBe("Bearcat.Movie.2026-GRP nfo");
        result
            .MediaFiles.Select(file => file.RelativePath)
            .ShouldBe(
                ["Bearcat.Movie.2026-GRP/part1.mkv", "Bearcat.Movie.2026-GRP/part2.mkv"],
                ignoreOrder: true
            );
        result
            .QualityIssues.Select(issue => issue.Description)
            .ShouldBe(
                ["Bearcat.Movie.2026-GRP missing nfo", "Bearcat.Movie.2026-GRP missing media info"],
                ignoreOrder: true
            );
    }

    [Test]
    public async Task GetForEvaluationAsync_ReleaseDoesNotExist_ReturnsNull()
    {
        // Arrange
        var release = await AddEvaluationReleaseAsync("Bearcat.Movie.2026-GRP", "Movie profile");

        // Act
        var result = await repository.GetForEvaluationAsync(release.Id + 1, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    private async Task<Release> AddEvaluationReleaseAsync(string name, string profileName)
    {
        var release = new Release
        {
            Name = name,
            CreatedAt = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc),
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = $"/tmp/{name}",
            QualityGateState = QualityGateState.Failed,
            ReleaseGroup = new ReleaseGroup
            {
                Name = $"{name} group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
                QualityProfile = new QualityProfile
                {
                    Name = profileName,
                    Rules =
                    [
                        new QualityCheckRule
                        {
                            RuleType = QualityCheckRuleType.MediaInfoPresent,
                            ParametersJson = "{}",
                        },
                        new QualityCheckRule
                        {
                            RuleType = QualityCheckRuleType.FilePatternPresent,
                            ParametersJson = "{}",
                        },
                    ],
                },
            },
            ReleaseInfo = new ReleaseInfo
            {
                NfoDatabaseClassName = ReleaseInfo.LocalNfoSource,
                ReleaseName = $"{name} info",
            },
            Metadata = new ReleaseMetadata
            {
                MetadataDatabaseClassName = ReleaseMetadata.ManualSource,
                Title = $"{name} title",
            },
            ReleaseNfo = new ReleaseNfo { FileName = $"{name}.nfo", Content = $"{name} nfo" },
            MediaFiles =
            [
                CreateMediaFile($"{name}/part1.mkv"),
                CreateMediaFile($"{name}/part2.mkv"),
            ],
            QualityIssues =
            [
                new ReleaseQualityIssue
                {
                    RuleType = QualityCheckRuleType.FilePatternPresent,
                    Description = $"{name} missing nfo",
                },
                new ReleaseQualityIssue
                {
                    RuleType = QualityCheckRuleType.MediaInfoPresent,
                    Description = $"{name} missing media info",
                },
            ],
        };

        DbContext.Releases.Add(release);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return release;
    }

    private static ReleaseMediaFile CreateMediaFile(string relativePath)
    {
        return new ReleaseMediaFile
        {
            RelativePath = relativePath,
            SizeBytes = 1_500_000_000,
            MediaInfoJson = "{}",
            MediaInfoText = "General",
        };
    }

    private async Task<Release> AddReleaseAsync(
        QualityGateState state,
        bool withProfile = true,
        IReadOnlyList<(OnlineState OnlineState, UploadState UploadState)>? uploads = null,
        ReleaseType releaseType = ReleaseType.Managed
    )
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = "Managed releases",
            EnableAutomaticReuploads = true,
            NumberOfHoursUntilReupload = 24,
            QualityProfile = withProfile
                ? new QualityProfile
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
                }
                : null,
        };
        var release = new Release
        {
            Name = "Bearcat.Release.001",
            CreatedAt = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc),
            ReleaseType = releaseType,
            ReleaseFolderPath = releaseType is ReleaseType.Managed ? "/tmp/release" : null,
            ReleaseGroup = releaseGroup,
            QualityGateState = state,
        };
        var archiveConfig = new ArchiveConfig
        {
            Release = release,
            Name = "Main archive",
            ArchiveFilesBasePath = "/tmp/archive",
            ArchiverName = "zip",
            ArchiveNamePrefix = "bearcat-release",
            ArchivePassword = "secret",
            ArchiveFileSizeMb = 512,
        };
        var hosterRegistration = new HosterRegistration
        {
            Name = "Hoster",
            SerializedConfig = "{}",
            HosterClassName = "TestHoster",
            IsActive = true,
        };
        var uploadConfig = new UploadConfig
        {
            Release = release,
            ArchiveConfig = archiveConfig,
            HosterRegistration = hosterRegistration,
            Name = "Default upload",
            Uploads = (uploads ?? [])
                .Select(upload => new Upload
                {
                    CreatedAt = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc),
                    UploadState = upload.UploadState,
                    OnlineState = upload.OnlineState,
                    ErrorMessages = [],
                })
                .ToList(),
        };

        DbContext.UploadConfigs.Add(uploadConfig);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return release;
    }
}

using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleases;

public class QualityGateRepositoryTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private QualityGateRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        repository = new QualityGateRepository(dbContext);
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
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

    [TestCase(QualityGateState.Passed)]
    [TestCase(QualityGateState.NotEvaluated)]
    [TestCase(QualityGateState.ManuallyApproved)]
    public async Task GetPendingReleasesAsync_NonFailedRelease_ExcludesRelease(
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

    private async Task<Release> AddReleaseAsync(
        QualityGateState state,
        bool withProfile = true,
        IReadOnlyList<(OnlineState OnlineState, UploadState UploadState)>? uploads = null
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
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/tmp/release",
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

        dbContext.UploadConfigs.Add(uploadConfig);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        return release;
    }
}

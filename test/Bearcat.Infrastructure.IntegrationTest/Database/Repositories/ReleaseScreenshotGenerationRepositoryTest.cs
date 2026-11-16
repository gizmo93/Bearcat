using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class ReleaseScreenshotGenerationRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private static readonly DateTime CreatedAt = new(
        2026,
        11,
        15,
        12,
        0,
        0,
        DateTimeKind.Unspecified
    );

    private ReleaseScreenshotGenerationRepository repository = null!;
    private ReleaseGroup releaseGroup = null!;

    [SetUp]
    public void Setup()
    {
        repository = new ReleaseScreenshotGenerationRepository(DbContext);
        releaseGroup = new ReleaseGroup
        {
            Name = "Group",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };
    }

    [Test]
    public async Task GetReleaseIdsPendingScreenshotGenerationAsync_ReleasesInDifferentStates_ReturnsManagedReleasesWhoseScreenshotsAnActiveImageUploadConfigUploads()
    {
        // Arrange
        var screenshotsRelease = AddRelease(
            "Screenshots",
            release => release.CreateScreenshots = true
        );
        var screenshotGridRelease = AddRelease(
            "Grid",
            release => release.CreateScreenshotGrid = true
        );
        AddRelease("Disabled", _ => { });
        AddRelease(
            "Unmanaged",
            release =>
            {
                release.CreateScreenshots = true;
                release.ReleaseType = ReleaseType.Unmanaged;
                release.ReleaseFolderPath = null;
            }
        );
        AddRelease(
            "WithoutVideo",
            release =>
            {
                release.CreateScreenshots = true;
                release.MediaMetadataExtractedAt = CreatedAt;
                release.MediaFiles.Clear();
            }
        );
        var notExtractedRelease = AddRelease(
            "NotExtracted",
            release =>
            {
                release.CreateScreenshots = true;
                release.MediaFiles.Clear();
            }
        );
        AddRelease(
            "Generated",
            release =>
            {
                release.CreateScreenshots = true;
                release.ScreenshotsGeneratedAt = CreatedAt;
            }
        );
        AddRelease(
            "WithoutImageUploadConfig",
            release =>
            {
                release.CreateScreenshots = true;
                release.ImageUploadConfigs.Clear();
            }
        );
        AddRelease(
            "ImageUploadConfigUploadsOnlyGrid",
            release =>
            {
                release.CreateScreenshots = true;
                release.ImageUploadConfigs.Single().UploadScreenshots = false;
            }
        );
        AddRelease(
            "InactiveImageHoster",
            release =>
            {
                release.CreateScreenshots = true;
                release.ImageUploadConfigs.Single().ImageHosterRegistration.IsActive = false;
            }
        );
        AddRelease(
            "Failed",
            release =>
            {
                release.CreateScreenshots = true;
                release.ScreenshotGenerationErrorMessage = "ffmpeg failed";
            }
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetReleaseIdsPendingScreenshotGenerationAsync(
            CancellationToken.None
        );

        // Assert
        result.ShouldBe([screenshotsRelease.Id, screenshotGridRelease.Id, notExtractedRelease.Id]);
    }

    private Release AddRelease(string name, Action<Release> configure)
    {
        var release = new Release
        {
            Name = name,
            CreatedAt = CreatedAt,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = $"/releases/{name}",
            ReleaseGroup = releaseGroup,
            MediaFiles =
            [
                new ReleaseMediaFile
                {
                    RelativePath = "movie.mkv",
                    SizeBytes = 1_000,
                    MediaInfoJson = "{}",
                    MediaInfoText = "General",
                },
            ],
            ImageUploadConfigs =
            [
                new ImageUploadConfig
                {
                    Name = "Screens",
                    ImageHosterRegistration = new ImageHosterRegistration
                    {
                        Name = $"{name} hoster",
                        ImageHosterClassName = "PixHost",
                        SerializedConfig = "{}",
                        IsActive = true,
                    },
                    UploadCover = false,
                    UploadScreenshots = true,
                    UploadScreenshotGrid = true,
                },
            ],
        };

        configure(release);
        DbContext.Releases.Add(release);

        return release;
    }
}

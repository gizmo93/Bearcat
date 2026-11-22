using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class ReleaseScreenshotReadRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private static readonly DateTime CreatedAt = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    private ReleaseScreenshotReadRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        repository = new ReleaseScreenshotReadRepository(DbContext);
    }

    [Test]
    public async Task GetOverviewAsync_OfflineImageUploads_PicksHostedUrlsOfOnlineUploadsOnly()
    {
        // Arrange
        var release = new Release
        {
            Name = "Bodies.2023.S01E01-GRP",
            CreatedAt = CreatedAt,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/tmp/bodies",
            ReleaseGroup = new ReleaseGroup
            {
                Name = "Bodies group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
            Screenshots = [CreateScreenshot(index: 1), CreateScreenshot(index: 2)],
        };

        var firstConfig = CreateImageUploadConfig(release, "ImgBB");
        var secondConfig = CreateImageUploadConfig(release, "PixHost");
        DbContext.ImageUploadConfigs.AddRange(firstConfig, secondConfig);
        await DbContext.SaveChangesAsync();

        var firstScreenshot = release.Screenshots.Single(screenshot => screenshot.Index == 1);
        var secondScreenshot = release.Screenshots.Single(screenshot => screenshot.Index == 2);

        await AddImageUploadAsync(
            config: firstConfig,
            screenshot: firstScreenshot,
            onlineState: OnlineState.Offline,
            url: "https://imgbb.test/1-offline.jpg"
        );

        await AddImageUploadAsync(
            config: secondConfig,
            screenshot: firstScreenshot,
            onlineState: OnlineState.Online,
            url: "https://pixhost.test/1-online.jpg"
        );

        await AddImageUploadAsync(
            config: firstConfig,
            screenshot: secondScreenshot,
            onlineState: OnlineState.Offline,
            url: "https://imgbb.test/2-offline.jpg"
        );

        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetOverviewAsync(release.Id, CancellationToken.None);

        // Assert
        var first = result.Screenshots.Single(screenshot => screenshot.Index == 1);
        first.HostedFullUrl.ShouldBe("https://pixhost.test/1-online.jpg");
        first.HostedPreviewUrl.ShouldBe("https://pixhost.test/1-online.jpg");

        var second = result.Screenshots.Single(screenshot => screenshot.Index == 2);
        second.HostedFullUrl.ShouldBeNull();
        second.HostedPreviewUrl.ShouldBeNull();
    }

    private async Task AddImageUploadAsync(
        ImageUploadConfig config,
        ReleaseScreenshot screenshot,
        OnlineState onlineState,
        string url
    )
    {
        DbContext.ImageUploads.Add(
            new ImageUpload
            {
                ImageUploadConfig = config,
                ReleaseScreenshot = screenshot,
                CreatedAt = CreatedAt,
                UploadedAt = CreatedAt,
                UploadState = UploadState.Completed,
                OnlineState = onlineState,
                ImageUrls = [new ImageUploadUrl { ImageSize = ImageSize.Full, Url = url }],
            }
        );

        await DbContext.SaveChangesAsync();
    }

    private static ImageUploadConfig CreateImageUploadConfig(Release release, string name)
    {
        return new ImageUploadConfig
        {
            Release = release,
            Name = name,
            ImageHosterRegistration = new ImageHosterRegistration
            {
                Name = name,
                ImageHosterClassName = name,
                SerializedConfig = "{}",
                IsActive = true,
            },
            UploadCover = false,
            UploadScreenshots = true,
            UploadScreenshotGrid = false,
        };
    }

    private static ReleaseScreenshot CreateScreenshot(int index)
    {
        return new ReleaseScreenshot
        {
            Kind = ReleaseScreenshotKind.Screenshot,
            Index = index,
            RelativeFilePath = $"screenshots/1/Screenshot-{index}.jpg",
            Width = 1920,
            Height = 1080,
            SizeBytes = 1024,
            CreatedAt = CreatedAt,
        };
    }
}

using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageImageUploads.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageImageUploads;

public class ImageUploadReadRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private static readonly DateTime Midnight = new(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);

    private ImageUploadReadRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        repository = new ImageUploadReadRepository(DbContext);
    }

    [Test]
    public async Task GetImageUploadsAsync_UploadedAndPendingUploads_OrdersByUploadedAtOrCreatedAtDescending()
    {
        // Arrange
        var seed = await AddImageUploadsAsync();

        // Act
        var result = await repository.GetImageUploadsAsync(seed.ReleaseId, CancellationToken.None);

        // Assert
        result
            .Select(upload => upload.ImageUploadId)
            .ShouldBe([
                seed.UploadedLatest.Id,
                seed.UploadedAtMidnight.Id,
                seed.CreatedAtMidnight.Id,
                seed.CreatedBeforeMidnight.Id,
            ]);

        var uploadedLatest = result[0];
        uploadedLatest.ImageUploadConfigName.ShouldBe("Screens");
        uploadedLatest.ImageHosterRegistrationName.ShouldBe("PixHost");
        uploadedLatest.CreatedAt.ShouldBe(Midnight.AddHours(-2));
        uploadedLatest.UploadedAt.ShouldBe(Midnight.AddMilliseconds(500));
        uploadedLatest.UploadState.ShouldBe(UploadState.Completed);
        uploadedLatest.UrlCount.ShouldBe(4);
        uploadedLatest.ErrorMessages.ShouldBeEmpty();

        var createdBeforeMidnight = result[3];
        createdBeforeMidnight.ImageUploadConfigName.ShouldBe("Cover");
        createdBeforeMidnight.ImageHosterRegistrationName.ShouldBe("ImgBB");
        createdBeforeMidnight.CreatedAt.ShouldBe(Midnight.AddMilliseconds(-250));
        createdBeforeMidnight.UploadedAt.ShouldBeNull();
        createdBeforeMidnight.UploadState.ShouldBe(UploadState.Failed);
        createdBeforeMidnight.UrlCount.ShouldBe(0);
        createdBeforeMidnight.ErrorMessages.ShouldBe(["Upload of Crème.png timed out", "HTTP 500"]);
    }

    [Test]
    public async Task GetImageUploadUrlsAsync_UrlsOfSeveralSizes_ReturnsUrlsOrderedBySizeAndId()
    {
        // Arrange
        var seed = await AddImageUploadsAsync();

        // Act
        var result = await repository.GetImageUploadUrlsAsync(
            seed.ReleaseId,
            seed.UploadedLatest.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe([
            new ReleaseImageUploadUrlReadModel(ImageSize.Full, "https://img.test/full-1.png"),
            new ReleaseImageUploadUrlReadModel(ImageSize.Full, "https://img.test/full-2.png"),
            new ReleaseImageUploadUrlReadModel(
                ImageSize.Thumbnail,
                "https://img.test/thumbnail.png"
            ),
            new ReleaseImageUploadUrlReadModel(ImageSize.Medium, "https://img.test/medium.png"),
        ]);
    }

    [Test]
    public async Task GetImageUploadUrlsAsync_UploadOfOtherRelease_ReturnsEmpty()
    {
        // Arrange
        var seed = await AddImageUploadsAsync();

        // Act
        var result = await repository.GetImageUploadUrlsAsync(
            seed.ReleaseId,
            seed.UploadOfOtherRelease.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBeEmpty();
    }

    private async Task<ImageUploadSeed> AddImageUploadsAsync()
    {
        var imgBb = CreateImageHosterRegistration("ImgBB");
        var pixHost = CreateImageHosterRegistration("PixHost");
        var release = CreateRelease("Bearcat.Release.2026-GRP");
        var otherRelease = CreateRelease("Bearcat.Other.2026-GRP");
        var coverConfig = new ImageUploadConfig
        {
            Release = release,
            ImageHosterRegistration = imgBb,
            Name = "Cover",
        };
        var screensConfig = new ImageUploadConfig
        {
            Release = release,
            ImageHosterRegistration = pixHost,
            Name = "Screens",
        };
        var otherReleaseConfig = new ImageUploadConfig
        {
            Release = otherRelease,
            ImageHosterRegistration = imgBb,
            Name = "Other cover",
        };
        DbContext.ImageUploadConfigs.AddRange(coverConfig, screensConfig, otherReleaseConfig);
        await DbContext.SaveChangesAsync();

        var uploadOfOtherRelease = await AddImageUploadAsync(
            otherReleaseConfig,
            Midnight.AddDays(1),
            Midnight.AddDays(1),
            UploadState.Completed
        );
        await AddImageUploadUrlAsync(
            uploadOfOtherRelease,
            ImageSize.Full,
            "https://img.test/other.png"
        );
        var createdAtMidnight = await AddImageUploadAsync(
            coverConfig,
            Midnight,
            null,
            UploadState.Pending
        );
        var uploadedLatest = await AddImageUploadAsync(
            screensConfig,
            Midnight.AddHours(-2),
            Midnight.AddMilliseconds(500),
            UploadState.Completed
        );
        await AddImageUploadUrlAsync(
            uploadedLatest,
            ImageSize.Medium,
            "https://img.test/medium.png"
        );
        await AddImageUploadUrlAsync(uploadedLatest, ImageSize.Full, "https://img.test/full-1.png");
        await AddImageUploadUrlAsync(
            uploadedLatest,
            ImageSize.Thumbnail,
            "https://img.test/thumbnail.png"
        );
        await AddImageUploadUrlAsync(uploadedLatest, ImageSize.Full, "https://img.test/full-2.png");
        var createdBeforeMidnight = await AddImageUploadAsync(
            coverConfig,
            Midnight.AddMilliseconds(-250),
            null,
            UploadState.Failed,
            ["Upload of Crème.png timed out", "HTTP 500"]
        );
        var uploadedAtMidnight = await AddImageUploadAsync(
            screensConfig,
            Midnight.AddHours(-1),
            Midnight,
            UploadState.Completed
        );
        await AddImageUploadUrlAsync(
            uploadedAtMidnight,
            ImageSize.Full,
            "https://img.test/full.png"
        );
        DbContext.ChangeTracker.Clear();

        return new ImageUploadSeed(
            release.Id,
            createdAtMidnight,
            uploadedLatest,
            createdBeforeMidnight,
            uploadedAtMidnight,
            uploadOfOtherRelease
        );
    }

    private async Task<ImageUpload> AddImageUploadAsync(
        ImageUploadConfig imageUploadConfig,
        DateTime createdAt,
        DateTime? uploadedAt,
        UploadState uploadState,
        List<string>? errorMessages = null
    )
    {
        var imageUpload = new ImageUpload
        {
            ImageUploadConfig = imageUploadConfig,
            CreatedAt = createdAt,
            UploadedAt = uploadedAt,
            UploadState = uploadState,
            ErrorMessages = errorMessages ?? [],
        };

        DbContext.ImageUploads.Add(imageUpload);
        await DbContext.SaveChangesAsync();

        return imageUpload;
    }

    private async Task AddImageUploadUrlAsync(
        ImageUpload imageUpload,
        ImageSize imageSize,
        string url
    )
    {
        DbContext.ImageUploadUrls.Add(
            new ImageUploadUrl
            {
                ImageUploadId = imageUpload.Id,
                ImageSize = imageSize,
                Url = url,
            }
        );
        await DbContext.SaveChangesAsync();
    }

    private static ImageHosterRegistration CreateImageHosterRegistration(string name)
    {
        return new ImageHosterRegistration
        {
            Name = name,
            ImageHosterClassName = name,
            SerializedConfig = "{}",
            IsActive = true,
        };
    }

    private static Release CreateRelease(string name)
    {
        return new Release
        {
            Name = name,
            CreatedAt = Midnight,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = $"/tmp/{name}",
            ReleaseGroup = new ReleaseGroup
            {
                Name = $"{name} group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
        };
    }

    private sealed record ImageUploadSeed(
        int ReleaseId,
        ImageUpload CreatedAtMidnight,
        ImageUpload UploadedLatest,
        ImageUpload CreatedBeforeMidnight,
        ImageUpload UploadedAtMidnight,
        ImageUpload UploadOfOtherRelease
    );
}

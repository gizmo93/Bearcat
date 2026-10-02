using Bearcat.Abstractions;
using Bearcat.Abstractions.Media;
using Bearcat.Domain.Entities;
using Bearcat.Domain.IntegrationTest.Shared;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleases;

public class MediaMetadataServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string MediaInfoJson =
        "{\"media\": {\"track\": [{\"@type\": \"General\", \"FileSize\": \"1500000000\"}]}}";

    private static readonly DateTime ExtractedAt = new(
        2026,
        9,
        30,
        0,
        0,
        0,
        250,
        DateTimeKind.Unspecified
    );

    private MediaMetadataRepository repository = null!;
    private Mock<IFileSystemService> fileSystemServiceMock = null!;
    private Mock<IMediaMetadataExtractor> extractorMock = null!;
    private MediaMetadataService service = null!;

    [SetUp]
    public void Setup()
    {
        repository = new MediaMetadataRepository(DbContext);
        fileSystemServiceMock = new Mock<IFileSystemService>(MockBehavior.Strict);
        extractorMock = new Mock<IMediaMetadataExtractor>(MockBehavior.Strict);
        var timeProvider = new ControllableTimeProvider(ExtractedAt);

        service = new MediaMetadataService(
            repository,
            extractorMock.Object,
            fileSystemServiceMock.Object,
            new ReleaseClassificationService(
                new ReleaseClassificationRepository(DbContext),
                timeProvider,
                NullLogger<ReleaseClassificationService>.Instance
            ),
            timeProvider,
            NullLogger<MediaMetadataService>.Instance
        );
    }

    [Test]
    public async Task GetReleaseWithMediaFilesAsync_ReleaseWithDetails_LoadsMediaFilesAndDetails()
    {
        // Arrange
        await AddReleaseAsync("Other.Movie.2025.German.720p.WEB.x264-GRP", ["other.mkv"]);
        var release = await AddReleaseAsync(
            "Amok.1994.German.1080p.BluRay.x264-PL3X",
            ["amok.part1.mkv", "amok.part2.mkv"]
        );

        // Act
        var result = await repository.GetReleaseWithMediaFilesAsync(
            release.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Amok.1994.German.1080p.BluRay.x264-PL3X");
        result
            .MediaFiles.Select(file => file.RelativePath)
            .ShouldBe(["amok.part1.mkv", "amok.part2.mkv"], ignoreOrder: true);
        result.Classification.ShouldNotBeNull();
        result.Classification.Title.ShouldBe("Amok.1994.German.1080p.BluRay.x264-PL3X title");
        result.ReleaseInfo.ShouldNotBeNull();
        result.ReleaseInfo.ReleaseName.ShouldBe("Amok.1994.German.1080p.BluRay.x264-PL3X");
        result.ReleaseNfo.ShouldNotBeNull();
        result.ReleaseNfo.FileName.ShouldBe("Amok.1994.German.1080p.BluRay.x264-PL3X.nfo");
    }

    [Test]
    public async Task ExtractForReleaseAsync_ReleaseWithExistingMediaFiles_ReplacesMediaFilesAndUpdatesClassification()
    {
        // Arrange
        var otherRelease = await AddReleaseAsync(
            "Other.Movie.2025.German.720p.WEB.x264-GRP",
            ["other.mkv"]
        );
        var releaseFolderPath = TestContext.CurrentContext.WorkDirectory;
        var release = await AddReleaseAsync(
            "Amok.1994.German.1080p.BluRay.x264-PL3X",
            ["old.part1.mkv", "old.part2.mkv"],
            releaseFolderPath
        );
        var moviePath = Path.Combine(releaseFolderPath, "Amok.mkv");
        var nfoPath = Path.Combine(releaseFolderPath, "Amok.nfo");
        fileSystemServiceMock
            .Setup(fileSystem => fileSystem.GetFilesInPath(releaseFolderPath, true))
            .Returns([nfoPath, moviePath]);
        extractorMock
            .Setup(extractor => extractor.ExtractAsync(moviePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaProbeResult(MediaInfoJson, "General"));

        // Act
        await service.ExtractForReleaseAsync(release.Id, CancellationToken.None);

        // Assert
        var assertContext = CreateDbContext();
        var mediaFiles = await assertContext
            .ReleaseMediaFiles.OrderBy(file => file.Id)
            .Select(file => new
            {
                file.ReleaseId,
                file.RelativePath,
                file.SizeBytes,
            })
            .ToListAsync();
        mediaFiles.ShouldBe([
            new
            {
                ReleaseId = otherRelease.Id,
                RelativePath = "other.mkv",
                SizeBytes = 1_000L,
            },
            new
            {
                ReleaseId = release.Id,
                RelativePath = "Amok.mkv",
                SizeBytes = 1_500_000_000L,
            },
        ]);
        var classifications = await assertContext
            .ReleaseClassifications.Where(classification => classification.ReleaseId == release.Id)
            .ToListAsync();
        var classification = classifications.ShouldHaveSingleItem();
        classification.Title.ShouldBe("Amok");
        classification.Resolution.ShouldBe(ReleaseResolution.R1080p);
        classification.ClassifiedAt.ShouldBe(ExtractedAt);
        var storedRelease = await assertContext.Releases.SingleAsync(r => r.Id == release.Id);
        storedRelease.MediaMetadataExtractedAt.ShouldBe(ExtractedAt);
    }

    private async Task<Release> AddReleaseAsync(
        string name,
        IReadOnlyList<string> mediaFilePaths,
        string? releaseFolderPath = null
    )
    {
        var release = new Release
        {
            Name = name,
            CreatedAt = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc),
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = releaseFolderPath ?? $"/tmp/{name}",
            ReleaseGroup = new ReleaseGroup
            {
                Name = $"{name} group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
            MediaFiles = mediaFilePaths
                .Select(path => new ReleaseMediaFile
                {
                    RelativePath = path,
                    SizeBytes = 1_000,
                    MediaInfoJson = "{}",
                    MediaInfoText = "General",
                })
                .ToList(),
            Classification = new ReleaseClassification
            {
                Title = $"{name} title",
                ContentType = ReleaseContentType.Movie,
                ClassifiedAt = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Unspecified),
            },
            ReleaseInfo = new ReleaseInfo
            {
                NfoDatabaseClassName = ReleaseInfo.LocalNfoSource,
                ReleaseName = name,
            },
            ReleaseNfo = new ReleaseNfo { FileName = $"{name}.nfo", Content = "NFO" },
        };

        DbContext.Releases.Add(release);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return release;
    }
}

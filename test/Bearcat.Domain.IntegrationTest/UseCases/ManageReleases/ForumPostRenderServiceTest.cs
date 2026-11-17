using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Rendering;
using Bearcat.Domain.UseCases.ManageReleases.ForumPostRendering;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleases;

public class ForumPostRenderServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private ForumPostRenderService service = null!;

    [SetUp]
    public void Setup()
    {
        var forumPostTemplateRepository = new ForumPostTemplateRepository(DbContext, DbContext);
        var releaseReadRepository = new ReleaseReadRepository(
            ReadDbContext,
            Mock.Of<IArchiverFactory>(factory => factory.GetArchivers() == new List<ArchiverDto>()),
            Mock.Of<ILinkCrypterFactory>(),
            Mock.Of<IHosterFactory>()
        );
        var renderModelBuilder = new ReleaseForumPostRenderModelBuilder(
            releaseReadRepository: releaseReadRepository,
            uploadRepository: releaseReadRepository,
            imageLinkRepository: releaseReadRepository
        );
        var renderSource = new ReleaseForumPostRenderSource(
            renderModelBuilder,
            Mock.Of<IDistributionSiteRegistrationReadRepository>()
        );

        service = new ForumPostRenderService(forumPostTemplateRepository, [renderSource]);
    }

    [Test]
    public async Task RenderAsync_TemplateUsesImageLinks_RendersUrlsByConfigNameAndSize()
    {
        // Arrange
        var release = await AddReleaseAsync();
        var template = new ForumPostTemplate
        {
            Name = "Image links template",
            TemplateBody =
                "{{ imagelinks.imgbb_cover.full }}|{{ imagelinks[\"ImgBB Cover\"].thumbnail }}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        DbContext.ForumPostTemplates.Add(template);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await service.RenderAsync(
            release.Id,
            template.Id,
            null,
            CancellationToken.None
        );

        // Assert
        result.Errors.ShouldBeEmpty();
        result.Content.ShouldBe("https://img.example/full.jpg|https://img.example/thumb.jpg");
    }

    [Test]
    public async Task RenderAsync_TemplateUsesScreenshotImageLinks_RendersScreenshotsByIndexAndScreenshotGrid()
    {
        // Arrange
        var release = await AddReleaseAsync();
        var imageUploadConfig = release.ImageUploadConfigs.Single();

        AddScreenshotImageUpload(
            imageUploadConfig: imageUploadConfig,
            kind: ReleaseScreenshotKind.Screenshot,
            index: 2,
            timestamp: new TimeSpan(1, 2, 3),
            url: "https://img.example/screenshot-2.jpg"
        );

        AddScreenshotImageUpload(
            imageUploadConfig: imageUploadConfig,
            kind: ReleaseScreenshotKind.Screenshot,
            index: 1,
            timestamp: TimeSpan.FromSeconds(90),
            url: "https://img.example/screenshot-1.jpg"
        );

        AddScreenshotImageUpload(
            imageUploadConfig: imageUploadConfig,
            kind: ReleaseScreenshotKind.ScreenshotGrid,
            index: 1,
            timestamp: null,
            url: "https://img.example/grid.jpg"
        );

        DbContext.ImageUploadConfigs.Add(
            new ImageUploadConfig
            {
                Release = release,
                Name = "Empty",
                ImageHosterRegistration = imageUploadConfig.ImageHosterRegistration,
                ImageUploads = [],
            }
        );

        var template = new ForumPostTemplate
        {
            Name = "Screenshot image links template",
            TemplateBody =
                "{{ imagelinks.imgbb_cover.full }}"
                + "|{{ for screenshot in imagelinks.imgbb_cover.screenshots }}"
                + "[{{ screenshot.index }} {{ screenshot.timestamp }} {{ screenshot.full }}]"
                + "{{ end }}|{{ imagelinks.imgbb_cover.screenshotgrid.full }}"
                + "|{{ for screenshot in imagelinks.empty.screenshots }}x{{ end }}"
                + "{{ imagelinks.empty.screenshotgrid.full }}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        DbContext.ForumPostTemplates.Add(template);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await service.RenderAsync(
            release.Id,
            template.Id,
            null,
            CancellationToken.None
        );

        // Assert
        result.Errors.ShouldBeEmpty();
        result.Content.ShouldBe(
            "https://img.example/full.jpg"
                + "|[1 00:01:30 https://img.example/screenshot-1.jpg]"
                + "[2 01:02:03 https://img.example/screenshot-2.jpg]"
                + "|https://img.example/grid.jpg|"
        );
    }

    [Test]
    public async Task LoadPreviewDataAsync_ExistingRelease_ReturnsDataNodesAndRendersPreview()
    {
        // Arrange
        var release = await AddReleaseAsync();

        // Act
        var previewData = await service.LoadPreviewDataAsync(
            ForumPostTemplateType.Release,
            release.Id,
            CancellationToken.None
        );

        // Assert
        previewData.ShouldNotBeNull();
        previewData
            .DataNodes.Select(node => node.Name)
            .ShouldBe([
                "release",
                "release_info",
                "classification",
                "uploads",
                "link_crypters",
                "imagelinks",
            ]);
        previewData
            .DataNodes.Single(node => node.Name == "release")
            .Children.Single(node => node.Name == "name")
            .Value.ShouldBe("Bearcat.Release.2026-GRP");
        var renderResult = await ForumPostRenderService.RenderPreviewAsync(
            previewData,
            "{{ release.name }}|{{ imagelinks.imgbb_cover.full }}"
        );
        renderResult.Errors.ShouldBeEmpty();
        renderResult.Content.ShouldBe("Bearcat.Release.2026-GRP|https://img.example/full.jpg");
    }

    private async Task<Release> AddReleaseAsync()
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = "Bearcat group",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
            Releases = [],
        };
        var release = new Release
        {
            Name = "Bearcat.Release.2026-GRP",
            CreatedAt = DateTime.UtcNow,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/tmp/Bearcat.Release.2026-GRP",
            ReleaseGroup = releaseGroup,
            ArchiveConfigs = [],
            UploadConfigs = [],
            ImageUploadConfigs = [],
        };
        var imageHosterRegistration = new ImageHosterRegistration
        {
            Name = "ImgBB",
            ImageHosterClassName = "ImgBb",
            SerializedConfig = "{}",
            IsActive = true,
        };
        var imageUploadConfig = new ImageUploadConfig
        {
            Release = release,
            Name = "ImgBB Cover",
            ImageHosterRegistration = imageHosterRegistration,
            ImageUploads = [],
        };
        var imageUpload = new ImageUpload
        {
            ImageUploadConfig = imageUploadConfig,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            UploadedAt = DateTime.UtcNow,
            UploadState = UploadState.Completed,
            ImageUrls =
            [
                new ImageUploadUrl
                {
                    ImageSize = ImageSize.Full,
                    Url = "https://img.example/full.jpg",
                },
                new ImageUploadUrl
                {
                    ImageSize = ImageSize.Thumbnail,
                    Url = "https://img.example/thumb.jpg",
                },
            ],
        };

        DbContext.AddRange(release, imageHosterRegistration, imageUploadConfig, imageUpload);
        await DbContext.SaveChangesAsync();

        return release;
    }

    private void AddScreenshotImageUpload(
        ImageUploadConfig imageUploadConfig,
        ReleaseScreenshotKind kind,
        int index,
        TimeSpan? timestamp,
        string url
    )
    {
        DbContext.ImageUploads.Add(
            new ImageUpload
            {
                ImageUploadConfig = imageUploadConfig,
                ReleaseScreenshot = new ReleaseScreenshot
                {
                    Release = imageUploadConfig.Release!,
                    Kind = kind,
                    Index = index,
                    Timestamp = timestamp,
                    RelativeFilePath = $"screenshots/{kind}-{index}.jpg",
                    Width = 1920,
                    Height = 1080,
                    SizeBytes = 1024,
                    CreatedAt = DateTime.UtcNow,
                },
                CreatedAt = DateTime.UtcNow,
                UploadedAt = DateTime.UtcNow,
                UploadState = UploadState.Completed,
                ImageUrls = [new ImageUploadUrl { ImageSize = ImageSize.Full, Url = url }],
            }
        );
    }
}

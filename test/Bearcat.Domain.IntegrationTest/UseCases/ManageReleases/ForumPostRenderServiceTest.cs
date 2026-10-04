using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ForumPostRendering;
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
            Mock.Of<ILinkCrypterFactory>()
        );
        var uploadBuilder = new ReleaseForumPostUploadBuilder(releaseReadRepository);
        var imageLinkBuilder = new ForumPostImageLinkBuilder(releaseReadRepository);
        var renderSource = new ReleaseForumPostRenderSource(
            releaseReadRepository,
            uploadBuilder,
            imageLinkBuilder
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
        var result = await service.RenderAsync(release.Id, template.Id, CancellationToken.None);

        // Assert
        result.Errors.ShouldBeEmpty();
        result.Content.ShouldBe("https://img.example/full.jpg|https://img.example/thumb.jpg");
    }

    [Test]
    public async Task RenderAsync_TemplateUsesPrimaryLanguage_RendersNativeLanguageName()
    {
        var release = await AddReleaseAsync();
        release.PrimaryLanguageCode = "de";
        var template = new ForumPostTemplate
        {
            Name = "Primary language template",
            TemplateBody = "{{ release.primary_language }}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        DbContext.ForumPostTemplates.Add(template);
        await DbContext.SaveChangesAsync();

        var result = await service.RenderAsync(release.Id, template.Id, CancellationToken.None);

        result.Errors.ShouldBeEmpty();
        result.Content.ShouldBe("Deutsch");
        service
            .GetVariables(ForumPostTemplateType.Release)
            .Single(node => node.Name == "release")
            .Children.ShouldContain(node =>
                node.Insertion != null && node.Insertion.Text == "{{ release.primary_language }}"
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
            .ShouldBe(["release", "release_info", "classification", "uploads", "imagelinks"]);
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

    [Test]
    public async Task LoadPreviewDataAsync_MissingRelease_ReturnsNull()
    {
        // Act
        var previewData = await service.LoadPreviewDataAsync(
            ForumPostTemplateType.Release,
            999,
            CancellationToken.None
        );

        // Assert
        previewData.ShouldBeNull();
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
}

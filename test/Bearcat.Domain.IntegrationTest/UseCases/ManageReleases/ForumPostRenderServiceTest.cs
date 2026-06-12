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

public class ForumPostRenderServiceTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private ForumPostRenderService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = CreateDbContext();
        var forumPostTemplateRepository = new ForumPostTemplateRepository(dbContext, dbContext);
        var releaseReadRepository = new ReleaseReadRepository(
            dbContext,
            Mock.Of<IArchiverFactory>(factory => factory.GetArchivers() == new List<ArchiverDto>()),
            Mock.Of<ILinkCrypterFactory>()
        );
        var uploadBuilder = new ReleaseForumPostUploadBuilder(releaseReadRepository);
        var renderSource = new ReleaseForumPostRenderSource(releaseReadRepository, uploadBuilder);

        service = new ForumPostRenderService(forumPostTemplateRepository, [renderSource]);
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
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

        dbContext.ForumPostTemplates.Add(template);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.RenderAsync(release.Id, template.Id, CancellationToken.None);

        // Assert
        result.Errors.ShouldBeEmpty();
        result.Content.ShouldBe("https://img.example/full.jpg|https://img.example/thumb.jpg");
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

        dbContext.AddRange(release, imageHosterRegistration, imageUploadConfig, imageUpload);
        await dbContext.SaveChangesAsync();

        return release;
    }
}

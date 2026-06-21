using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Rendering;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ForumPostRendering;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleaseCollections;

public class ReleaseCollectionForumPostRenderTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private ForumPostRenderService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = CreateDbContext();

        var releaseReadRepository = new ReleaseReadRepository(
            dbContext,
            Mock.Of<IArchiverFactory>(factory =>
                factory.GetArchivers()
                == new List<ArchiverDto> { new("RAR", "RarArchiver", ".rar") }
            ),
            Mock.Of<ILinkCrypterFactory>()
        );
        var uploadBuilder = new ReleaseForumPostUploadBuilder(releaseReadRepository);
        var imageLinkBuilder = new ForumPostImageLinkBuilder(releaseReadRepository);
        var collectionSource = new ReleaseCollectionForumPostRenderSource(
            new ReleaseCollectionForumPostRepository(dbContext),
            uploadBuilder,
            imageLinkBuilder
        );

        service = new ForumPostRenderService(
            new ForumPostTemplateRepository(dbContext, dbContext),
            [collectionSource]
        );
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task RenderAsync_CollectionTemplate_RendersSeriesReleasesUploadsAndCrypters()
    {
        // Arrange
        var collection = await AddCollectionWithUploadAsync();
        var template = new ForumPostTemplate
        {
            Name = "Collection template",
            Type = ForumPostTemplateType.ReleaseCollection,
            TemplateBody =
                "{{ series.title }}|{{ series.description }}"
                + "{{ for release in releases }}|{{ release.name }}"
                + "{{ for upload in release.uploads }}#{{ upload.name }}/{{ upload.archive_format }}/{{ upload.archive_password }}"
                + "{{ for crypter in upload.link_crypters }}[{{ crypter.name }}:{{ crypter.password }}:{{ crypter.container_link }}]"
                + "{{ end }}{{ end }}{{ end }}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        dbContext.ForumPostTemplates.Add(template);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.RenderAsync(collection.Id, template.Id, CancellationToken.None);

        // Assert
        result.Errors.ShouldBeEmpty();
        result.Content.ShouldBe(
            "Bodies|Vier Detectives"
                + "|Bodies.2023.S01E01-GRP#Rapidgator/RAR/archivepw"
                + "[filecrypt:crypterpw:https://filecrypt.example/abc]"
        );
    }

    [Test]
    public async Task RenderAsync_CollectionTemplateUsesImageLinks_RendersUrlsByConfigNameAndSize()
    {
        // Arrange
        var collection = await AddCollectionWithImageUploadAsync();
        var template = new ForumPostTemplate
        {
            Name = "Collection image links template",
            Type = ForumPostTemplateType.ReleaseCollection,
            TemplateBody =
                "{{ imagelinks.imgbb_cover.full }}|{{ imagelinks[\"ImgBB Cover\"].thumbnail }}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        dbContext.ForumPostTemplates.Add(template);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.RenderAsync(collection.Id, template.Id, CancellationToken.None);

        // Assert
        result.Errors.ShouldBeEmpty();
        result.Content.ShouldBe("https://img.example/full.jpg|https://img.example/thumb.jpg");
    }

    private async Task<ReleaseCollection> AddCollectionWithImageUploadAsync()
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = $"Release group {Guid.NewGuid():N}",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
            Releases = [],
        };

        var collection = new ReleaseCollection
        {
            ReleaseGroup = releaseGroup,
            Key = $"key-{Guid.NewGuid():N}",
            Name = "Bodies.2023.S01.German.DL.1080p",
            CreatedAt = DateTime.UtcNow,
            Metadata = new ReleaseCollectionMetadata
            {
                SeriesDatabaseClassName = "TvdbSeriesDatabase",
                Title = "Bodies",
                Description = "Vier Detectives",
                CoverUrl = "https://artworks.example/cover.jpg",
                SeriesDatabaseUrl = "https://www.thetvdb.com/series/bodies",
            },
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
            ReleaseCollection = collection,
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

        dbContext.AddRange(
            releaseGroup,
            collection,
            imageHosterRegistration,
            imageUploadConfig,
            imageUpload
        );
        await dbContext.SaveChangesAsync();

        return collection;
    }

    private async Task<ReleaseCollection> AddCollectionWithUploadAsync()
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = $"Release group {Guid.NewGuid():N}",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
            Releases = [],
        };

        var collection = new ReleaseCollection
        {
            ReleaseGroup = releaseGroup,
            Key = $"key-{Guid.NewGuid():N}",
            Name = "Bodies.2023.S01.German.DL.1080p",
            CreatedAt = DateTime.UtcNow,
            Metadata = new ReleaseCollectionMetadata
            {
                SeriesDatabaseClassName = "TvdbSeriesDatabase",
                Title = "Bodies",
                Description = "Vier Detectives",
                CoverUrl = "https://artworks.example/cover.jpg",
                SeriesDatabaseUrl = "https://www.thetvdb.com/series/bodies",
            },
        };

        var hosterRegistration = new HosterRegistration
        {
            Name = "Rapidgator hoster",
            SerializedConfig = "{}",
            IsActive = true,
            HosterClassName = "Rapidgator",
        };

        var release = new Release
        {
            Name = "Bodies.2023.S01E01-GRP",
            CreatedAt = DateTime.UtcNow,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/tmp/bodies-s01e01",
            ReleaseGroup = releaseGroup,
            ReleaseCollection = collection,
        };

        var archiveConfig = new ArchiveConfig
        {
            Release = release,
            Name = "Default archive",
            ArchiveFilesBasePath = "/tmp/archives",
            ArchiverName = "RarArchiver",
            ArchivePassword = "archivepw",
            ArchiveFileSizeMb = 100,
        };

        var linkCrypterRegistration = new LinkCrypterRegistration
        {
            Name = "filecrypt",
            LinkCrypterClassName = "FileCrypt",
            SerializedConfig = "{}",
            IsActive = true,
        };

        var uploadConfig = new UploadConfig
        {
            Release = release,
            HosterRegistration = hosterRegistration,
            ArchiveConfig = archiveConfig,
            Name = "Rapidgator",
            LinkCrypters =
            [
                new UploadConfigLinkCrypter
                {
                    LinkCrypterRegistration = linkCrypterRegistration,
                    Password = "crypterpw",
                },
            ],
        };

        var upload = new Upload
        {
            UploadConfig = uploadConfig,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            UploadedAt = DateTime.UtcNow,
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Online,
        };

        var container = new LinkCrypterContainer
        {
            Scope = LinkCrypterContainerScope.Release,
            Upload = upload,
            LinkCrypterRegistration = linkCrypterRegistration,
            ContainerUrl = "https://filecrypt.example/abc",
            State = LinkCrypterContainerState.Created,
            CreatedAt = DateTime.UtcNow,
        };

        dbContext.AddRange(
            releaseGroup,
            collection,
            hosterRegistration,
            release,
            archiveConfig,
            linkCrypterRegistration,
            uploadConfig,
            upload,
            container
        );
        await dbContext.SaveChangesAsync();

        return collection;
    }
}

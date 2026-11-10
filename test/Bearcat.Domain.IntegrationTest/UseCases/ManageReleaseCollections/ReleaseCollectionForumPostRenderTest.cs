using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Rendering;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageReleases.ForumPostRendering;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleaseCollections;

public class ReleaseCollectionForumPostRenderTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private ForumPostRenderService service = null!;

    [SetUp]
    public void Setup()
    {
        var releaseReadRepository = new ReleaseReadRepository(
            ReadDbContext,
            Mock.Of<IArchiverFactory>(factory =>
                factory.GetArchivers()
                == new List<ArchiverDto> { new("RAR", "RarArchiver", ".rar") }
            ),
            Mock.Of<ILinkCrypterFactory>(),
            Mock.Of<IHosterFactory>(factory =>
                factory.GetByName("Rapidgator")
                == Mock.Of<IHoster>(hoster => hoster.Name == "Rapidgator")
            )
        );
        var imageLinkBuilder = new ForumPostImageLinkBuilder(releaseReadRepository);

        var releaseRenderModelBuilder = new ReleaseForumPostRenderModelBuilder(
            releaseReadRepository: releaseReadRepository,
            uploadBuilder: new ReleaseForumPostUploadBuilder(releaseReadRepository),
            imageLinkBuilder: imageLinkBuilder
        );

        var collectionSource = new ReleaseCollectionForumPostRenderSource(
            repository: new ReleaseCollectionForumPostRepository(DbContext),
            releaseRenderModelBuilder: releaseRenderModelBuilder,
            imageLinkBuilder: imageLinkBuilder
        );

        service = new ForumPostRenderService(
            new ForumPostTemplateRepository(DbContext, DbContext),
            [collectionSource]
        );
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
        DbContext.ForumPostTemplates.Add(template);
        await DbContext.SaveChangesAsync();

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
        DbContext.ForumPostTemplates.Add(template);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await service.RenderAsync(collection.Id, template.Id, CancellationToken.None);

        // Assert
        result.Errors.ShouldBeEmpty();
        result.Content.ShouldBe("https://img.example/full.jpg|https://img.example/thumb.jpg");
    }

    [Test]
    public async Task RenderAsync_TemplateUsesPrimaryLanguage_RendersNativeLanguageName()
    {
        var collection = await AddCollectionWithImageUploadAsync();
        collection.PrimaryLanguageCode = "de";
        var template = new ForumPostTemplate
        {
            Name = "Collection primary language template",
            Type = ForumPostTemplateType.ReleaseCollection,
            TemplateBody = "{{ collection.primary_language }}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        DbContext.ForumPostTemplates.Add(template);
        await DbContext.SaveChangesAsync();

        var result = await service.RenderAsync(collection.Id, template.Id, CancellationToken.None);

        result.Errors.ShouldBeEmpty();
        result.Content.ShouldBe("Deutsch");
        service
            .GetVariables(ForumPostTemplateType.ReleaseCollection)
            .Single(node => node.Name == "collection")
            .Children.ShouldContain(node =>
                node.Insertion != null && node.Insertion.Text == "{{ collection.primary_language }}"
            );
    }

    [Test]
    public async Task RenderAsync_CollectionTemplateUsesPerReleaseData_RendersOwnDataOfEachRelease()
    {
        // Arrange
        var collection = await AddCollectionWithTwoDetailedReleasesAsync();
        var template = new ForumPostTemplate
        {
            Name = "Collection per release template",
            Type = ForumPostTemplateType.ReleaseCollection,
            TemplateBody =
                "{{ for release in releases }}|{{ release.name }}"
                + ";{{ release.nfo }}"
                + ";{{ release.release_info.release_name }}"
                + ";{{ release.classification.title }}/{{ release.classification.resolution }}"
                + "{{ for crypter in release.link_crypters }};{{ crypter.container_link }}{{ end }}"
                + ";{{ release.imagelinks.cover.full }}"
                + "{{ end }}|{{ imagelinks.cover.full }}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        DbContext.ForumPostTemplates.Add(template);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await service.RenderAsync(collection.Id, template.Id, CancellationToken.None);

        // Assert
        result.Errors.ShouldBeEmpty();
        result.Content.ShouldBe(
            "|Bodies.2023.S01E01-GRP;E01 nfo;Bodies.2023.S01E01.Info;Bodies E01/1080p"
                + ";https://crypter.example/E01;https://img.example/E01.jpg"
                + "|Bodies.2023.S01E02-GRP;E02 nfo;Bodies.2023.S01E02.Info;Bodies E02/2160p"
                + ";https://crypter.example/E02;https://img.example/E02.jpg"
                + "|"
        );
    }

    [Test]
    public void GetVariables_CollectionType_ContainsPerReleaseVariablesIncludingImageLinks()
    {
        // Act
        var variables = service.GetVariables(ForumPostTemplateType.ReleaseCollection);

        // Assert
        var releasesVariable = variables.Single(node => node.Name == "releases");
        releasesVariable.LoopStatement.ShouldBe("for release in releases");

        releasesVariable
            .Children.Select(node => node.Path)
            .ShouldBe([
                "release.name",
                "release.primary_language",
                "release.nfo",
                "release.steam_app_id",
                "release.steam_url",
                "release.imdb_id",
                "release.imdb_url",
                "release.main_video",
                "release.media_files",
                "release.release_info",
                "release.classification",
                "release.uploads",
                "release.link_crypters",
                "release.imagelinks",
            ]);

        releasesVariable
            .Children.Single(node => node.Path == "release.imagelinks")
            .Children.Select(node => node.Insertion?.Text)
            .ShouldContain("{{ release.imagelinks.<image_upload_config_name>.full }}");

        variables
            .Single(node => node.Path == "imagelinks")
            .Children.Select(node => node.Insertion?.Text)
            .ShouldContain("{{ imagelinks.<image_upload_config_name>.full }}");
    }

    [Test]
    public async Task LoadPreviewDataAsync_ExistingCollection_ReturnsDataNodesAndRendersPreview()
    {
        // Arrange
        var collection = await AddCollectionWithUploadAsync();

        // Act
        var previewData = await service.LoadPreviewDataAsync(
            ForumPostTemplateType.ReleaseCollection,
            collection.Id,
            CancellationToken.None
        );

        // Assert
        previewData.ShouldNotBeNull();
        previewData
            .DataNodes.Select(node => node.Name)
            .ShouldBe(["collection", "series", "releases", "imagelinks"]);
        var firstRelease = previewData
            .DataNodes.Single(node => node.Name == "releases")
            .Children.Single();
        firstRelease.Name.ShouldBe("[0]");
        firstRelease
            .Children.Single(node => node.Name == "name")
            .Value.ShouldBe("Bodies.2023.S01E01-GRP");

        firstRelease
            .Children.Select(node => node.Name)
            .ShouldBe([
                "name",
                "primary_language",
                "nfo",
                "steam_app_id",
                "steam_url",
                "imdb_id",
                "imdb_url",
                "main_video",
                "media_files",
                "release_info",
                "classification",
                "uploads",
                "link_crypters",
                "imagelinks",
            ]);

        var renderResult = await ForumPostRenderService.RenderPreviewAsync(
            previewData,
            "{{ series.title }}{{ for release in releases }}|{{ release.name }}{{ end }}"
        );
        renderResult.Errors.ShouldBeEmpty();
        renderResult.Content.ShouldBe("Bodies|Bodies.2023.S01E01-GRP");
    }

    [Test]
    public async Task LoadPreviewDataAsync_MissingCollection_ReturnsNull()
    {
        // Act
        var previewData = await service.LoadPreviewDataAsync(
            ForumPostTemplateType.ReleaseCollection,
            999,
            CancellationToken.None
        );

        // Assert
        previewData.ShouldBeNull();
    }

    private async Task<ReleaseCollection> AddCollectionWithTwoDetailedReleasesAsync()
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
        };

        var linkCrypterRegistration = new LinkCrypterRegistration
        {
            Name = "filecrypt",
            LinkCrypterClassName = "FileCrypt",
            SerializedConfig = "{}",
            IsActive = true,
        };

        var imageHosterRegistration = new ImageHosterRegistration
        {
            Name = "ImgBB",
            ImageHosterClassName = "ImgBb",
            SerializedConfig = "{}",
            IsActive = true,
        };

        DbContext.AddRange(
            releaseGroup,
            collection,
            linkCrypterRegistration,
            imageHosterRegistration
        );

        foreach (
            var (episode, resolution) in new[]
            {
                ("E02", ReleaseResolution.R2160p),
                ("E01", ReleaseResolution.R1080p),
            }
        )
        {
            AddDetailedRelease(
                episode: episode,
                resolution: resolution,
                releaseGroup: releaseGroup,
                collection: collection,
                linkCrypterRegistration: linkCrypterRegistration,
                imageHosterRegistration: imageHosterRegistration
            );
        }

        await DbContext.SaveChangesAsync();

        return collection;
    }

    private void AddDetailedRelease(
        string episode,
        ReleaseResolution resolution,
        ReleaseGroup releaseGroup,
        ReleaseCollection collection,
        LinkCrypterRegistration linkCrypterRegistration,
        ImageHosterRegistration imageHosterRegistration
    )
    {
        var release = new Release
        {
            Name = $"Bodies.2023.S01{episode}-GRP",
            CreatedAt = DateTime.UtcNow,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = $"/tmp/bodies-s01{episode}",
            ReleaseGroup = releaseGroup,
            ReleaseCollection = collection,
            ReleaseNfo = new ReleaseNfo { FileName = $"{episode}.nfo", Content = $"{episode} nfo" },
            ReleaseInfo = new ReleaseInfo
            {
                NfoDatabaseClassName = "XrelNfoDatabase",
                ReleaseName = $"Bodies.2023.S01{episode}.Info",
                ExternalInfos = [],
            },
            Classification = new ReleaseClassification
            {
                Title = $"Bodies {episode}",
                ContentType = ReleaseContentType.TvShowEpisode,
                ContentTypeSource = ClassificationSource.ReleaseName,
                Resolution = resolution,
                ResolutionSource = ClassificationSource.ReleaseName,
                ParserVersion = 1,
                ClassifiedAt = DateTime.UtcNow,
            },
        };

        var container = new LinkCrypterContainer
        {
            Scope = LinkCrypterContainerScope.ReleaseAllHosters,
            Release = release,
            LinkCrypterRegistration = linkCrypterRegistration,
            ContainerUrl = $"https://crypter.example/{episode}",
            State = LinkCrypterContainerState.Created,
            CreatedAt = DateTime.UtcNow,
        };

        var imageUploadConfig = new ImageUploadConfig
        {
            Release = release,
            Name = "Cover",
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
                    Url = $"https://img.example/{episode}.jpg",
                },
            ],
        };

        DbContext.AddRange(release, container, imageUploadConfig, imageUpload);
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
                MetadataDatabaseClassName = "TvdbMetadataDatabase",
                Title = "Bodies",
                Description = "Vier Detectives",
                CoverUrl = "https://artworks.example/cover.jpg",
                MetadataDatabaseUrl = "https://www.thetvdb.com/series/bodies",
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

        DbContext.AddRange(
            releaseGroup,
            collection,
            imageHosterRegistration,
            imageUploadConfig,
            imageUpload
        );
        await DbContext.SaveChangesAsync();

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
                MetadataDatabaseClassName = "TvdbMetadataDatabase",
                Title = "Bodies",
                Description = "Vier Detectives",
                CoverUrl = "https://artworks.example/cover.jpg",
                MetadataDatabaseUrl = "https://www.thetvdb.com/series/bodies",
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
            UploadSource = UploadSource.ArchiveFiles,
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
            HosterRegistration = uploadConfig.HosterRegistration,
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

        DbContext.AddRange(
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
        await DbContext.SaveChangesAsync();

        return collection;
    }
}

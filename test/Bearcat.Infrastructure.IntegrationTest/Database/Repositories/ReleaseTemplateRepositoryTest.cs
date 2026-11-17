using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.IntegrationTest.Database.MaterializedValues;
using Bearcat.IntegrationTest.Utils;
using Moq;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class ReleaseTemplateRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private ReleaseTemplateRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        var archiverFactory = new Mock<IArchiverFactory>();
        archiverFactory
            .Setup(factory => factory.GetArchivers())
            .Returns([new ArchiverDto("RAR", "rar", ".rar")]);
        var linkCrypterFactory = new Mock<ILinkCrypterFactory>();
        linkCrypterFactory
            .Setup(factory => factory.GetLinkCrypters())
            .Returns([
                new LinkCrypterDto(
                    "Test crypter",
                    "TestCrypter",
                    [],
                    SupportsCaptcha: true,
                    SupportsContainerDownload: false,
                    SupportsClickAndLoad: true
                ),
            ]);
        repository = new ReleaseTemplateRepository(
            DbContext,
            DbContext,
            archiverFactory.Object,
            linkCrypterFactory.Object
        );
    }

    [Test]
    public async Task GetAllAsync_TemplatesWithAndWithoutChildren_ReturnsChildCountsOrderedByName()
    {
        // Arrange
        var seed = await AddSeriesTemplateWithChildrenAsync();
        var movieTemplate = new ReleaseTemplate
        {
            Name = "Movie template",
            ReleaseType = ReleaseType.Unmanaged,
            ReleaseContentType = ReleaseContentType.Movie,
            ReleaseGroupId = seed.ReleaseGroupId,
        };
        DbContext.ReleaseTemplates.Add(movieTemplate);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetAllAsync(CancellationToken.None);

        // Assert
        result.ShouldBe([
            new ReleaseTemplateSummaryReadModel(
                ReleaseTemplateId: movieTemplate.Id,
                Name: "Movie template",
                ReleaseType: ReleaseType.Unmanaged,
                ReleaseContentType: ReleaseContentType.Movie,
                ReleaseGroupId: seed.ReleaseGroupId,
                ReleaseGroupName: "Series group",
                ReleaseCollectionDetectionMode: ReleaseCollectionDetectionMode.Disabled,
                ReleaseCollectionPattern: null,
                ReleaseCollectionKeyTemplate: null,
                ReleaseCollectionNameTemplate: null,
                CreateScreenshots: false,
                ScreenshotCount: 6,
                CreateScreenshotGrid: false,
                ScreenshotGridColumns: 4,
                ScreenshotGridRows: 4,
                ArchiveConfigTemplateCount: 0,
                UploadConfigTemplateCount: 0,
                ImageUploadConfigTemplateCount: 0,
                LinkCrypterTemplateCount: 0
            ),
            new ReleaseTemplateSummaryReadModel(
                ReleaseTemplateId: seed.ReleaseTemplateId,
                Name: "Series template",
                ReleaseType: ReleaseType.Managed,
                ReleaseContentType: ReleaseContentType.TvShowEpisode,
                ReleaseGroupId: seed.ReleaseGroupId,
                ReleaseGroupName: "Series group",
                ReleaseCollectionDetectionMode: ReleaseCollectionDetectionMode.SeriesEpisodePattern,
                ReleaseCollectionPattern: "S\\d{2}",
                ReleaseCollectionKeyTemplate: "{title}.s{season}",
                ReleaseCollectionNameTemplate: "{title} S{season}",
                CreateScreenshots: false,
                ScreenshotCount: 6,
                CreateScreenshotGrid: false,
                ScreenshotGridColumns: 4,
                ScreenshotGridRows: 4,
                ArchiveConfigTemplateCount: 2,
                UploadConfigTemplateCount: 2,
                ImageUploadConfigTemplateCount: 2,
                LinkCrypterTemplateCount: 3
            ),
        ]);
    }

    [Test]
    public async Task GetDetailAsync_UploadConfigTemplateTargetsHosterAccountPool_ReturnsHosterAccountPoolAsUploadTarget()
    {
        // Arrange
        var testData = new MaterializedValuesTestData(DbContext);
        var hosterAccountPool = testData.AddHosterAccountPool(
            "Main",
            testData.AddHosterRegistration("Member")
        );
        var uploadConfigTemplate = testData.AddHosterAccountPoolUploadConfigTemplate(
            hosterAccountPool
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetDetailAsync(
            uploadConfigTemplate.ReleaseTemplateId,
            CancellationToken.None
        );

        // Assert
        var template = result!.UploadConfigTemplates.ShouldHaveSingleItem();
        template.DisplayName.ShouldBe("Main");
        template.HosterRegistrationId.ShouldBeNull();
        template.HosterRegistrationName.ShouldBeNull();
        template.HosterAccountPoolId.ShouldBe(hosterAccountPool.Id);
        template.HosterAccountPoolName.ShouldBe("Main");
    }

    private async Task<SeriesTemplateSeed> AddSeriesTemplateWithChildrenAsync()
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = "Series group",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };
        var rapidgator = new HosterRegistration
        {
            Name = "Rapidgator",
            SerializedConfig = "{}",
            HosterClassName = "RapidgatorHoster",
            IsActive = true,
        };
        var ddownload = new HosterRegistration
        {
            Name = "Ddownload",
            SerializedConfig = "{}",
            HosterClassName = "DdownloadHoster",
            IsActive = true,
        };
        var filecrypt = new LinkCrypterRegistration
        {
            Name = "Filecrypt",
            LinkCrypterClassName = "TestCrypter",
            SerializedConfig = "{}",
            IsActive = true,
        };
        var keeplinks = new LinkCrypterRegistration
        {
            Name = "Keeplinks",
            LinkCrypterClassName = "TestCrypter",
            SerializedConfig = "{}",
            IsActive = true,
        };
        var pixhost = new ImageHosterRegistration
        {
            Name = "PiXhost",
            ImageHosterClassName = "PiXhost",
            SerializedConfig = "{}",
            IsActive = true,
        };
        var imgbb = new ImageHosterRegistration
        {
            Name = "ImgBB",
            ImageHosterClassName = "ImgBb",
            SerializedConfig = "{}",
            IsActive = true,
        };
        var premiumAdText = new AdditionalArchiveContent
        {
            Name = "Premium ad text",
            Type = AdditionalArchiveContentType.TextFile,
            FileName = "premium.txt",
            TextContent = "Buy premium via my link.",
        };
        var premiumAdFolder = new AdditionalArchiveContent
        {
            Name = "Premium ad folder",
            Type = AdditionalArchiveContentType.Path,
            SourcePath = "/data/ads/premium",
        };
        var releaseTemplate = new ReleaseTemplate
        {
            Name = "Series template",
            ReleaseType = ReleaseType.Managed,
            ReleaseContentType = ReleaseContentType.TvShowEpisode,
            ReleaseGroup = releaseGroup,
            ReleaseCollectionDetectionMode = ReleaseCollectionDetectionMode.SeriesEpisodePattern,
            ReleaseCollectionPattern = "S\\d{2}",
            ReleaseCollectionKeyTemplate = "{title}.s{season}",
            ReleaseCollectionNameTemplate = "{title} S{season}",
        };
        var rarTemplate = new ArchiveConfigTemplate
        {
            ReleaseTemplate = releaseTemplate,
            Name = "RAR",
            ArchiveFilesBasePath = "/tmp/archives",
            ArchiverName = "rar",
            ArchivePassword = "archive-secret",
            ArchiveFileSizeMb = 1024,
            UseReleaseNameAsArchiveName = true,
            CreateNonceFile = false,
            AdditionalArchiveContents = [premiumAdText, premiumAdFolder],
        };
        var sevenZipTemplate = new ArchiveConfigTemplate
        {
            ReleaseTemplate = releaseTemplate,
            Name = "7z",
            ArchiveFilesBasePath = "/tmp/archives",
            ArchiverName = "7z",
            ArchiveFileSizeMb = 0,
            PackReleaseFolderAsRootFolder = false,
        };
        var rapidgatorFilecryptTemplate = new UploadConfigLinkCrypterTemplate
        {
            LinkCrypterRegistration = filecrypt,
            ContainerScope = LinkCrypterContainerScope.Release,
            Password = "filecrypt-secret",
            EnableCaptcha = false,
        };
        var rapidgatorKeeplinksTemplate = new UploadConfigLinkCrypterTemplate
        {
            LinkCrypterRegistration = keeplinks,
            ContainerScope = LinkCrypterContainerScope.ReleaseCollection,
            Password = "keeplinks-secret",
        };
        var rapidgatorTemplate = new UploadConfigTemplate
        {
            UploadSource = UploadSource.ArchiveFiles,
            ReleaseTemplate = releaseTemplate,
            ArchiveConfigTemplate = rarTemplate,
            HosterRegistration = rapidgator,
            PremiumOnlyDownload = true,
            CollectionUploadSlotKey = "rapidgator",
            CollectionUploadSlotName = "Rapidgator",
            CollectionUploadSlotIsRequired = true,
            CollectionUploadSlotPasswordPolicy =
                CollectionUploadSlotPasswordPolicy.MustEqualExpectedValue,
            CollectionUploadSlotExpectedArchivePassword = "slot-secret",
            LinkCrypterTemplates = [rapidgatorKeeplinksTemplate, rapidgatorFilecryptTemplate],
        };
        var mirrorTemplate = new UploadConfigTemplate
        {
            UploadSource = UploadSource.ArchiveFiles,
            ReleaseTemplate = releaseTemplate,
            ArchiveConfigTemplate = sevenZipTemplate,
            HosterRegistration = ddownload,
            Name = "Mirror",
            LinkCrypterTemplates =
            [
                new UploadConfigLinkCrypterTemplate
                {
                    LinkCrypterRegistration = filecrypt,
                    Password = "mirror-secret",
                },
            ],
        };
        var screensTemplate = new ImageUploadConfigTemplate
        {
            ReleaseTemplate = releaseTemplate,
            ImageHosterRegistration = imgbb,
            Name = "Screens",
            UploadScreenshots = true,
            UploadScreenshotGrid = true,
        };
        var pixhostTemplate = new ImageUploadConfigTemplate
        {
            ReleaseTemplate = releaseTemplate,
            ImageHosterRegistration = pixhost,
        };
        var seriesCoverTemplate = new CollectionImageUploadConfigTemplate
        {
            ReleaseTemplate = releaseTemplate,
            ImageHosterRegistration = imgbb,
            Name = "Series cover",
        };

        DbContext.AddRange(
            releaseTemplate,
            rarTemplate,
            sevenZipTemplate,
            rapidgatorTemplate,
            mirrorTemplate,
            screensTemplate,
            pixhostTemplate,
            seriesCoverTemplate
        );
        await DbContext.SaveChangesAsync();

        return new SeriesTemplateSeed(
            ReleaseGroupId: releaseGroup.Id,
            ReleaseTemplateId: releaseTemplate.Id,
            RarArchiveConfigTemplateId: rarTemplate.Id,
            SevenZipArchiveConfigTemplateId: sevenZipTemplate.Id,
            RapidgatorUploadConfigTemplateId: rapidgatorTemplate.Id,
            MirrorUploadConfigTemplateId: mirrorTemplate.Id,
            RapidgatorFilecryptTemplateId: rapidgatorFilecryptTemplate.Id,
            RapidgatorKeeplinksTemplateId: rapidgatorKeeplinksTemplate.Id,
            RapidgatorHosterRegistrationId: rapidgator.Id,
            FilecryptRegistrationId: filecrypt.Id,
            KeeplinksRegistrationId: keeplinks.Id,
            PixhostRegistrationId: pixhost.Id,
            ImgbbRegistrationId: imgbb.Id,
            PixhostImageUploadConfigTemplateId: pixhostTemplate.Id,
            ScreensImageUploadConfigTemplateId: screensTemplate.Id,
            SeriesCoverCollectionImageUploadConfigTemplateId: seriesCoverTemplate.Id,
            PremiumAdFolderId: premiumAdFolder.Id,
            PremiumAdTextId: premiumAdText.Id
        );
    }

    private sealed record SeriesTemplateSeed(
        int ReleaseGroupId,
        int ReleaseTemplateId,
        int RarArchiveConfigTemplateId,
        int SevenZipArchiveConfigTemplateId,
        int RapidgatorUploadConfigTemplateId,
        int MirrorUploadConfigTemplateId,
        int RapidgatorFilecryptTemplateId,
        int RapidgatorKeeplinksTemplateId,
        int RapidgatorHosterRegistrationId,
        int FilecryptRegistrationId,
        int KeeplinksRegistrationId,
        int PixhostRegistrationId,
        int ImgbbRegistrationId,
        int PixhostImageUploadConfigTemplateId,
        int ScreensImageUploadConfigTemplateId,
        int SeriesCoverCollectionImageUploadConfigTemplateId,
        int PremiumAdFolderId,
        int PremiumAdTextId
    );
}

using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleaseTemplates;

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
                ArchiveConfigTemplateCount: 2,
                UploadConfigTemplateCount: 2,
                ImageUploadConfigTemplateCount: 2,
                LinkCrypterTemplateCount: 3
            ),
        ]);
    }

    [Test]
    public async Task GetDetailAsync_TemplateWithChildren_ReturnsMappedAndOrderedChildren()
    {
        // Arrange
        var seed = await AddSeriesTemplateWithChildrenAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetDetailAsync(
            seed.ReleaseTemplateId,
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.ReleaseTemplateId.ShouldBe(seed.ReleaseTemplateId);
        result.Name.ShouldBe("Series template");
        result.ReleaseType.ShouldBe(ReleaseType.Managed);
        result.ReleaseContentType.ShouldBe(ReleaseContentType.TvShowEpisode);
        result.ReleaseGroupId.ShouldBe(seed.ReleaseGroupId);
        result.ReleaseGroupName.ShouldBe("Series group");
        result.ReleaseCollectionDetectionMode.ShouldBe(
            ReleaseCollectionDetectionMode.SeriesEpisodePattern
        );
        result.ReleaseCollectionPattern.ShouldBe("S\\d{2}");
        result.ReleaseCollectionKeyTemplate.ShouldBe("{title}.s{season}");
        result.ReleaseCollectionNameTemplate.ShouldBe("{title} S{season}");

        result.ArchiveConfigTemplates.Select(template => template.Name).ShouldBe(["7z", "RAR"]);
        var sevenZipTemplate = result.ArchiveConfigTemplates[0];
        sevenZipTemplate.ArchiveConfigTemplateId.ShouldBe(seed.SevenZipArchiveConfigTemplateId);
        sevenZipTemplate.ArchiverName.ShouldBe("7z");
        sevenZipTemplate.ArchiverDisplayName.ShouldBe("7z");
        sevenZipTemplate.UploadConfigTemplateCount.ShouldBe(1);
        sevenZipTemplate.AdditionalArchiveContents.ShouldBeEmpty();

        var rarTemplate = result.ArchiveConfigTemplates[1];
        rarTemplate.ArchiveConfigTemplateId.ShouldBe(seed.RarArchiveConfigTemplateId);
        rarTemplate.ArchiveFilesBasePath.ShouldBe("/tmp/archives");
        rarTemplate.ArchiverName.ShouldBe("rar");
        rarTemplate.ArchiverDisplayName.ShouldBe("RAR");
        rarTemplate.ArchivePassword.ShouldBe("archive-secret");
        rarTemplate.ArchiveFileSizeMb.ShouldBe(1024);
        rarTemplate.UseReleaseNameAsArchiveName.ShouldBeTrue();
        rarTemplate.UploadConfigTemplateCount.ShouldBe(1);
        rarTemplate.AdditionalArchiveContents.ShouldBe([
            new AssignedAdditionalArchiveContentReadModel(
                seed.PremiumAdFolderId,
                "Premium ad folder",
                AdditionalArchiveContentType.Path
            ),
            new AssignedAdditionalArchiveContentReadModel(
                seed.PremiumAdTextId,
                "Premium ad text",
                AdditionalArchiveContentType.TextFile
            ),
        ]);

        result
            .UploadConfigTemplates.Select(template => template.DisplayName)
            .ShouldBe(["Mirror", "Rapidgator"]);
        var mirrorTemplate = result.UploadConfigTemplates[0];
        mirrorTemplate.UploadConfigTemplateId.ShouldBe(seed.MirrorUploadConfigTemplateId);
        mirrorTemplate.Name.ShouldBe("Mirror");
        mirrorTemplate.HosterRegistrationName.ShouldBe("Ddownload");
        mirrorTemplate.ArchiveConfigTemplateId.ShouldBe(seed.SevenZipArchiveConfigTemplateId);
        mirrorTemplate.ArchiveConfigTemplateName.ShouldBe("7z");
        mirrorTemplate.PremiumOnlyDownload.ShouldBeFalse();
        mirrorTemplate.CollectionUploadSlotKey.ShouldBeNull();
        mirrorTemplate
            .LinkCrypterTemplates.Select(template => template.LinkCrypterRegistrationName)
            .ShouldBe(["Filecrypt"]);

        var rapidgatorTemplate = result.UploadConfigTemplates[1];
        rapidgatorTemplate.UploadConfigTemplateId.ShouldBe(seed.RapidgatorUploadConfigTemplateId);
        rapidgatorTemplate.Name.ShouldBeNull();
        rapidgatorTemplate.HosterRegistrationId.ShouldBe(seed.RapidgatorHosterRegistrationId);
        rapidgatorTemplate.HosterRegistrationName.ShouldBe("Rapidgator");
        rapidgatorTemplate.ArchiveConfigTemplateName.ShouldBe("RAR");
        rapidgatorTemplate.PremiumOnlyDownload.ShouldBeTrue();
        rapidgatorTemplate.CollectionUploadSlotKey.ShouldBe("rapidgator");
        rapidgatorTemplate.CollectionUploadSlotName.ShouldBe("Rapidgator");
        rapidgatorTemplate.CollectionUploadSlotIsRequired.ShouldBeTrue();
        rapidgatorTemplate.CollectionUploadSlotPasswordPolicy.ShouldBe(
            CollectionUploadSlotPasswordPolicy.MustEqualExpectedValue
        );
        rapidgatorTemplate.CollectionUploadSlotExpectedArchivePassword.ShouldBe("slot-secret");
        rapidgatorTemplate.LinkCrypterTemplates.ShouldBe([
            new UploadConfigLinkCrypterTemplateReadModel(
                UploadConfigLinkCrypterTemplateId: seed.RapidgatorFilecryptTemplateId,
                LinkCrypterRegistrationId: seed.FilecryptRegistrationId,
                LinkCrypterRegistrationName: "Filecrypt",
                LinkCrypterName: "Test crypter",
                ContainerScope: LinkCrypterContainerScope.Release,
                Password: "filecrypt-secret",
                EnableCaptcha: false,
                EnableContainerDownload: true,
                EnableClickAndLoad: true,
                SupportsCaptcha: true,
                SupportsContainerDownload: false,
                SupportsClickAndLoad: true
            ),
            new UploadConfigLinkCrypterTemplateReadModel(
                UploadConfigLinkCrypterTemplateId: seed.RapidgatorKeeplinksTemplateId,
                LinkCrypterRegistrationId: seed.KeeplinksRegistrationId,
                LinkCrypterRegistrationName: "Keeplinks",
                LinkCrypterName: "Test crypter",
                ContainerScope: LinkCrypterContainerScope.ReleaseCollection,
                Password: "keeplinks-secret",
                EnableCaptcha: true,
                EnableContainerDownload: true,
                EnableClickAndLoad: true,
                SupportsCaptcha: true,
                SupportsContainerDownload: false,
                SupportsClickAndLoad: true
            ),
        ]);

        result.ImageUploadConfigTemplates.ShouldBe([
            new ImageUploadConfigTemplateReadModel(
                seed.PixhostImageUploadConfigTemplateId,
                null,
                "PiXhost",
                seed.PixhostRegistrationId,
                "PiXhost"
            ),
            new ImageUploadConfigTemplateReadModel(
                seed.ScreensImageUploadConfigTemplateId,
                "Screens",
                "Screens",
                seed.ImgbbRegistrationId,
                "ImgBB"
            ),
        ]);
        result.CollectionImageUploadConfigTemplates.ShouldBe([
            new ImageUploadConfigTemplateReadModel(
                seed.SeriesCoverCollectionImageUploadConfigTemplateId,
                "Series cover",
                "Series cover",
                seed.ImgbbRegistrationId,
                "ImgBB"
            ),
        ]);
    }

    [Test]
    public async Task GetDetailAsync_TemplateDoesNotExist_ReturnsNull()
    {
        // Arrange
        var seed = await AddSeriesTemplateWithChildrenAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetDetailAsync(
            seed.ReleaseTemplateId + 1,
            CancellationToken.None
        );

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task GetByIdAsync_TemplateExists_ReturnsTemplateWithReleaseGroup()
    {
        // Arrange
        var seed = await AddSeriesTemplateWithChildrenAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetByIdAsync(seed.ReleaseTemplateId, CancellationToken.None);

        // Assert
        result.Id.ShouldBe(seed.ReleaseTemplateId);
        result.Name.ShouldBe("Series template");
        result.ReleaseGroup.ShouldNotBeNull();
        result.ReleaseGroup.Id.ShouldBe(seed.ReleaseGroupId);
        result.ReleaseGroup.Name.ShouldBe("Series group");
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
            AdditionalArchiveContents = [premiumAdText, premiumAdFolder],
        };
        var sevenZipTemplate = new ArchiveConfigTemplate
        {
            ReleaseTemplate = releaseTemplate,
            Name = "7z",
            ArchiveFilesBasePath = "/tmp/archives",
            ArchiverName = "7z",
            ArchiveFileSizeMb = 0,
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

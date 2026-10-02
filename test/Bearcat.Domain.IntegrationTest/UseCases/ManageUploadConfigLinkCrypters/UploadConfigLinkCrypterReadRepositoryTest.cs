using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageUploadConfigLinkCrypters;

public class UploadConfigLinkCrypterReadRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string CaptchaCrypterClassName = "CaptchaCrypter";
    private const string ContainerCrypterClassName = "ContainerCrypter";

    private UploadConfigLinkCrypterReadRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        var linkCrypterFactoryMock = new Mock<ILinkCrypterFactory>(MockBehavior.Strict);
        linkCrypterFactoryMock
            .Setup(factory => factory.GetLinkCrypters())
            .Returns([
                new LinkCrypterDto(
                    "Captcha crypter",
                    CaptchaCrypterClassName,
                    [],
                    SupportsCaptcha: true,
                    SupportsContainerDownload: false,
                    SupportsClickAndLoad: true
                ),
                new LinkCrypterDto(
                    "Container crypter",
                    ContainerCrypterClassName,
                    [],
                    SupportsCaptcha: false,
                    SupportsContainerDownload: true,
                    SupportsClickAndLoad: false
                ),
            ]);

        repository = new UploadConfigLinkCrypterReadRepository(
            DbContext,
            linkCrypterFactoryMock.Object
        );
    }

    [Test]
    public async Task GetByIdAsync_UploadConfigWithoutCollectionSlot_ReturnsReadModelWithoutCollection()
    {
        // Arrange
        var seed = await AddLinkCryptersAsync();

        // Act
        var result = await repository.GetByIdAsync(
            seed.ReleaseCaptchaLinkCrypter.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(
            new UploadConfigLinkCrypterReadModel(
                seed.ReleaseCaptchaLinkCrypter.Id,
                "Captcha crypter",
                "Captcha registration",
                seed.CaptchaRegistrationId,
                "secret",
                LinkCrypterContainerScope.Release,
                LinkCrypterIsActive: true,
                EnableCaptcha: false,
                EnableContainerDownload: true,
                EnableClickAndLoad: true,
                SupportsCaptcha: true,
                SupportsContainerDownload: false,
                SupportsClickAndLoad: true,
                ReleaseCollectionId: null
            )
        );
    }

    [Test]
    public async Task GetByIdAsync_UploadConfigWithCollectionSlot_ReturnsReleaseCollectionId()
    {
        // Arrange
        var seed = await AddLinkCryptersAsync();

        // Act
        var result = await repository.GetByIdAsync(
            seed.CollectionLinkCrypter.Id,
            CancellationToken.None
        );

        // Assert
        result.UploadConfigLinkCrypterId.ShouldBe(seed.CollectionLinkCrypter.Id);
        result.ContainerScope.ShouldBe(LinkCrypterContainerScope.ReleaseCollection);
        result.ReleaseCollectionId.ShouldBe(seed.ReleaseCollectionId);
    }

    [Test]
    public async Task GetByUploadConfigIdAsync_UploadConfigWithoutCollectionSlot_ReturnsItsLinkCrypters()
    {
        // Arrange
        var seed = await AddLinkCryptersAsync();

        // Act
        var result = await repository.GetByUploadConfigIdAsync(
            seed.ReleaseUploadConfigId,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(
            [
                new UploadConfigLinkCrypterReadModel(
                    seed.ReleaseCaptchaLinkCrypter.Id,
                    "Captcha crypter",
                    "Captcha registration",
                    seed.CaptchaRegistrationId,
                    "secret",
                    LinkCrypterContainerScope.Release,
                    LinkCrypterIsActive: true,
                    EnableCaptcha: false,
                    EnableContainerDownload: true,
                    EnableClickAndLoad: true,
                    SupportsCaptcha: true,
                    SupportsContainerDownload: false,
                    SupportsClickAndLoad: true,
                    ReleaseCollectionId: null
                ),
                new UploadConfigLinkCrypterReadModel(
                    seed.ReleaseContainerLinkCrypter.Id,
                    "Container crypter",
                    "Container registration",
                    seed.ContainerRegistrationId,
                    null,
                    LinkCrypterContainerScope.Release,
                    LinkCrypterIsActive: false,
                    EnableCaptcha: true,
                    EnableContainerDownload: true,
                    EnableClickAndLoad: true,
                    SupportsCaptcha: false,
                    SupportsContainerDownload: true,
                    SupportsClickAndLoad: false,
                    ReleaseCollectionId: null
                ),
            ],
            ignoreOrder: true
        );
    }

    [Test]
    public async Task GetByUploadConfigIdAsync_UploadConfigWithCollectionSlot_ReturnsReleaseCollectionId()
    {
        // Arrange
        var seed = await AddLinkCryptersAsync();

        // Act
        var result = await repository.GetByUploadConfigIdAsync(
            seed.CollectionUploadConfigId,
            CancellationToken.None
        );

        // Assert
        var readModel = result.ShouldHaveSingleItem();
        readModel.UploadConfigLinkCrypterId.ShouldBe(seed.CollectionLinkCrypter.Id);
        readModel.ReleaseCollectionId.ShouldBe(seed.ReleaseCollectionId);
    }

    [Test]
    public async Task GetLinkCrypterOptionsAsync_ActiveAndInactiveRegistrations_ReturnsActiveRegistrations()
    {
        // Arrange
        var seed = await AddLinkCryptersAsync();

        // Act
        var result = await repository.GetLinkCrypterOptionsAsync(CancellationToken.None);

        // Assert
        result.ShouldBe([
            new LinkCrypterOptionReadModel(
                seed.CaptchaRegistrationId,
                "Captcha registration",
                SupportsCaptcha: true,
                SupportsContainerDownload: false,
                SupportsClickAndLoad: true
            ),
        ]);
    }

    private async Task<LinkCrypterSeed> AddLinkCryptersAsync()
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = "Release group",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };
        var releaseCollection = new ReleaseCollection
        {
            ReleaseGroup = releaseGroup,
            Key = "bodies-2023-s01",
            Name = "Bodies.2023.S01",
            CreatedAt = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
        };
        var collectionUploadSlot = new CollectionUploadSlot
        {
            ReleaseCollection = releaseCollection,
            Key = "main",
            Name = "Main slot",
        };
        var release = new Release
        {
            Name = "Bodies.2023.S01E01",
            CreatedAt = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/tmp/release",
            ReleaseGroup = releaseGroup,
            ReleaseCollection = releaseCollection,
        };
        var archiveConfig = new ArchiveConfig
        {
            Release = release,
            Name = "Main archive",
            ArchiveFilesBasePath = "/tmp/archive",
            ArchiverName = "zip",
            ArchiveFileSizeMb = 512,
        };
        var hosterRegistration = new HosterRegistration
        {
            Name = "Hoster",
            SerializedConfig = "{}",
            HosterClassName = "TestHoster",
            IsActive = true,
        };
        var releaseUploadConfig = new UploadConfig
        {
            Release = release,
            ArchiveConfig = archiveConfig,
            HosterRegistration = hosterRegistration,
            Name = "Release upload",
        };
        var collectionUploadConfig = new UploadConfig
        {
            Release = release,
            ArchiveConfig = archiveConfig,
            HosterRegistration = hosterRegistration,
            CollectionUploadSlot = collectionUploadSlot,
            Name = "Collection upload",
        };
        var captchaRegistration = new LinkCrypterRegistration
        {
            Name = "Captcha registration",
            LinkCrypterClassName = CaptchaCrypterClassName,
            SerializedConfig = "{}",
            IsActive = true,
        };
        var containerRegistration = new LinkCrypterRegistration
        {
            Name = "Container registration",
            LinkCrypterClassName = ContainerCrypterClassName,
            SerializedConfig = "{}",
            IsActive = false,
        };
        var releaseCaptchaLinkCrypter = new UploadConfigLinkCrypter
        {
            UploadConfig = releaseUploadConfig,
            LinkCrypterRegistration = captchaRegistration,
            Password = "secret",
            ContainerScope = LinkCrypterContainerScope.Release,
            EnableCaptcha = false,
        };
        var releaseContainerLinkCrypter = new UploadConfigLinkCrypter
        {
            UploadConfig = releaseUploadConfig,
            LinkCrypterRegistration = containerRegistration,
            Password = null,
            ContainerScope = LinkCrypterContainerScope.Release,
        };
        var collectionLinkCrypter = new UploadConfigLinkCrypter
        {
            UploadConfig = collectionUploadConfig,
            LinkCrypterRegistration = captchaRegistration,
            Password = "collection-secret",
            ContainerScope = LinkCrypterContainerScope.ReleaseCollection,
        };

        DbContext.UploadConfigLinkCrypters.AddRange(
            releaseCaptchaLinkCrypter,
            releaseContainerLinkCrypter,
            collectionLinkCrypter
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return new LinkCrypterSeed(
            releaseCollection.Id,
            releaseUploadConfig.Id,
            collectionUploadConfig.Id,
            captchaRegistration.Id,
            containerRegistration.Id,
            releaseCaptchaLinkCrypter,
            releaseContainerLinkCrypter,
            collectionLinkCrypter
        );
    }

    private sealed record LinkCrypterSeed(
        int ReleaseCollectionId,
        int ReleaseUploadConfigId,
        int CollectionUploadConfigId,
        int CaptchaRegistrationId,
        int ContainerRegistrationId,
        UploadConfigLinkCrypter ReleaseCaptchaLinkCrypter,
        UploadConfigLinkCrypter ReleaseContainerLinkCrypter,
        UploadConfigLinkCrypter CollectionLinkCrypter
    );
}

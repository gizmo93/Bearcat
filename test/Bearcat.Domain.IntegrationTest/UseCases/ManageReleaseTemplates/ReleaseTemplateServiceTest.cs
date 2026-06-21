using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseTemplates;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleaseTemplates;

public class ReleaseTemplateServiceTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private ReleaseTemplateService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        service = new ReleaseTemplateService(CreateRepository());
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task CreateAsync_ValidTemplate_PersistsTemplate()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync();

        // Act
        var result = await service.CreateAsync(
            "Scene template",
            ReleaseType.Managed,
            ReleaseContentType.Movie,
            releaseGroup.Id,
            CancellationToken.None
        );

        // Assert
        var template = await dbContext.ReleaseTemplates.SingleAsync();

        result.ShouldBeGreaterThan(0);
        template.Id.ShouldBe(result);
        template.Name.ShouldBe("Scene template");
        template.ReleaseType.ShouldBe(ReleaseType.Managed);
        template.ReleaseGroupId.ShouldBe(releaseGroup.Id);
    }

    [Test]
    public async Task CreateAsync_UnmanagedTemplate_CreatesFixedArchiveConfigTemplate()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync();

        // Act
        var result = await service.CreateAsync(
            "Unmanaged template",
            ReleaseType.Unmanaged,
            ReleaseContentType.Movie,
            releaseGroup.Id,
            CancellationToken.None
        );

        // Assert
        var template = await dbContext
            .ReleaseTemplates.Include(t => t.ArchiveConfigTemplates)
            .SingleAsync();
        var archiveConfigTemplate = template.ArchiveConfigTemplates.Single();

        result.ShouldBeGreaterThan(0);
        template.ReleaseType.ShouldBe(ReleaseType.Unmanaged);
        archiveConfigTemplate.Name.ShouldBe("Unmanaged archives");
        archiveConfigTemplate.ArchiveFilesBasePath.ShouldBe("Release folder");
        archiveConfigTemplate.ArchiverName.ShouldBe("Unmanaged");
        archiveConfigTemplate.ArchivePassword.ShouldBeNull();
        archiveConfigTemplate.ArchiveFileSizeMb.ShouldBe(0);
        archiveConfigTemplate.UseReleaseNameAsArchiveName.ShouldBeFalse();
    }

    [Test]
    public async Task CreateArchiveUploadAndLinkCrypterTemplates_PersistsTemplateChildren()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync();
        var releaseTemplate = await AddReleaseTemplateAsync(releaseGroup.Id);
        var hosterRegistration = await AddHosterRegistrationAsync();
        var linkCrypterRegistration = await AddLinkCrypterRegistrationAsync();

        // Act
        var archiveConfigTemplateId = await service.CreateArchiveConfigTemplateAsync(
            releaseTemplate.Id,
            "RAR Forum A",
            "/tmp/archives",
            "rar",
            "archive-secret",
            1024,
            true,
            CancellationToken.None
        );
        var uploadConfigTemplateId = await service.CreateUploadConfigTemplateAsync(
            releaseTemplate.Id,
            " ",
            hosterRegistration.Id,
            archiveConfigTemplateId,
            true,
            CancellationToken.None
        );
        await service.CreateUploadConfigLinkCrypterTemplateAsync(
            uploadConfigTemplateId,
            linkCrypterRegistration.Id,
            "container-secret",
            true,
            true,
            true,
            CancellationToken.None
        );

        // Assert
        var template = await dbContext
            .ReleaseTemplates.AsSplitQuery()
            .Include(t => t.ArchiveConfigTemplates)
            .Include(t => t.UploadConfigTemplates)
                .ThenInclude(u => u.LinkCrypterTemplates)
            .SingleAsync();

        var archiveConfigTemplate = template.ArchiveConfigTemplates.Single();
        archiveConfigTemplate.Name.ShouldBe("RAR Forum A");
        archiveConfigTemplate.ArchiveFilesBasePath.ShouldBe("/tmp/archives");
        archiveConfigTemplate.ArchiverName.ShouldBe("rar");
        archiveConfigTemplate.ArchivePassword.ShouldBe("archive-secret");
        archiveConfigTemplate.ArchiveFileSizeMb.ShouldBe(1024);
        archiveConfigTemplate.UseReleaseNameAsArchiveName.ShouldBeTrue();

        var uploadConfigTemplate = template.UploadConfigTemplates.Single();
        uploadConfigTemplate.Name.ShouldBeNull();
        uploadConfigTemplate.HosterRegistrationId.ShouldBe(hosterRegistration.Id);
        uploadConfigTemplate.ArchiveConfigTemplateId.ShouldBe(archiveConfigTemplateId);
        uploadConfigTemplate.PremiumOnlyDownload.ShouldBeTrue();

        var linkCrypterTemplate = uploadConfigTemplate.LinkCrypterTemplates.Single();
        linkCrypterTemplate.LinkCrypterRegistrationId.ShouldBe(linkCrypterRegistration.Id);
        linkCrypterTemplate.Password.ShouldBe("container-secret");
        linkCrypterTemplate.EnableCaptcha.ShouldBeTrue();
        linkCrypterTemplate.EnableContainerDownload.ShouldBeTrue();
        linkCrypterTemplate.EnableClickAndLoad.ShouldBeTrue();
    }

    [Test]
    public async Task UpdateAsync_TemplateExists_UpdatesTemplate()
    {
        // Arrange
        var firstReleaseGroup = await AddReleaseGroupAsync();
        var secondReleaseGroup = await AddReleaseGroupAsync("Updated releases");
        var releaseTemplate = await AddReleaseTemplateAsync(firstReleaseGroup.Id);

        // Act
        await service.UpdateAsync(
            releaseTemplate.Id,
            "Updated template",
            ReleaseType.Managed,
            ReleaseContentType.Movie,
            secondReleaseGroup.Id,
            CancellationToken.None
        );

        // Assert
        var result = await dbContext.ReleaseTemplates.SingleAsync();

        result.Name.ShouldBe("Updated template");
        result.ReleaseType.ShouldBe(ReleaseType.Managed);
        result.ReleaseGroupId.ShouldBe(secondReleaseGroup.Id);
    }

    [Test]
    public async Task UpdateAsync_ReleaseTypeChanges_ThrowsInvalidOperationException()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync();
        var releaseTemplate = await AddReleaseTemplateAsync(releaseGroup.Id);

        // Act
        var result = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.UpdateAsync(
                releaseTemplate.Id,
                "Updated template",
                ReleaseType.Unmanaged,
                ReleaseContentType.Movie,
                releaseGroup.Id,
                CancellationToken.None
            )
        );

        // Assert
        result.Message.ShouldBe("Release template type cannot be changed.");
    }

    [Test]
    public async Task DeleteAsync_TemplateExists_RemovesTemplateWithChildren()
    {
        // Arrange
        var seed = await AddReleaseTemplateWithChildrenAsync();

        // Act
        await service.DeleteAsync(seed.ReleaseTemplateId, CancellationToken.None);

        // Assert
        (await dbContext.ReleaseTemplates.AnyAsync()).ShouldBeFalse();
        (await dbContext.ArchiveConfigTemplates.AnyAsync()).ShouldBeFalse();
        (await dbContext.UploadConfigTemplates.AnyAsync()).ShouldBeFalse();
        (await dbContext.UploadConfigLinkCrypterTemplates.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task UpdateArchiveConfigTemplateAsync_TemplateExists_UpdatesArchiveConfigTemplate()
    {
        // Arrange
        var seed = await AddReleaseTemplateWithChildrenAsync();

        // Act
        await service.UpdateArchiveConfigTemplateAsync(
            seed.ArchiveConfigTemplateId,
            "ZIP Forum B",
            "/tmp/updated-archives",
            "zip",
            " ",
            2048,
            false,
            CancellationToken.None
        );

        // Assert
        var result = await dbContext.ArchiveConfigTemplates.SingleAsync();

        result.Name.ShouldBe("ZIP Forum B");
        result.ArchiveFilesBasePath.ShouldBe("/tmp/updated-archives");
        result.ArchiverName.ShouldBe("zip");
        result.ArchivePassword.ShouldBeNull();
        result.ArchiveFileSizeMb.ShouldBe(2048);
        result.UseReleaseNameAsArchiveName.ShouldBeFalse();
    }

    [Test]
    public async Task DeleteArchiveConfigTemplateAsync_TemplateExists_RemovesArchiveConfigTemplate()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync();
        var releaseTemplate = await AddReleaseTemplateAsync(releaseGroup.Id);
        var archiveConfigTemplateId = await service.CreateArchiveConfigTemplateAsync(
            releaseTemplate.Id,
            "RAR Forum A",
            "/tmp/archives",
            "rar",
            "archive-secret",
            1024,
            true,
            CancellationToken.None
        );

        // Act
        await service.DeleteArchiveConfigTemplateAsync(
            archiveConfigTemplateId,
            CancellationToken.None
        );

        // Assert
        (await dbContext.ArchiveConfigTemplates.AnyAsync()).ShouldBeFalse();
        (await dbContext.ReleaseTemplates.AnyAsync()).ShouldBeTrue();
    }

    [Test]
    public async Task CreateArchiveConfigTemplateAsync_UnmanagedTemplate_ThrowsInvalidOperationException()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync();
        var releaseTemplateId = await service.CreateAsync(
            "Unmanaged template",
            ReleaseType.Unmanaged,
            ReleaseContentType.Movie,
            releaseGroup.Id,
            CancellationToken.None
        );

        // Act
        var result = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.CreateArchiveConfigTemplateAsync(
                releaseTemplateId,
                "RAR Forum A",
                "/tmp/archives",
                "rar",
                "archive-secret",
                1024,
                true,
                CancellationToken.None
            )
        );

        // Assert
        result.Message.ShouldBe(
            "Archive config templates for unmanaged release templates cannot be changed."
        );
    }

    [Test]
    public async Task UpdateArchiveConfigTemplateAsync_UnmanagedTemplate_ThrowsInvalidOperationException()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync();
        var releaseTemplateId = await service.CreateAsync(
            "Unmanaged template",
            ReleaseType.Unmanaged,
            ReleaseContentType.Movie,
            releaseGroup.Id,
            CancellationToken.None
        );
        var archiveConfigTemplate = await dbContext.ArchiveConfigTemplates.SingleAsync();

        // Act
        var result = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.UpdateArchiveConfigTemplateAsync(
                archiveConfigTemplate.Id,
                "RAR Forum A",
                "/tmp/archives",
                "rar",
                "archive-secret",
                1024,
                true,
                CancellationToken.None
            )
        );

        // Assert
        releaseTemplateId.ShouldBeGreaterThan(0);
        result.Message.ShouldBe(
            "Archive config templates for unmanaged release templates cannot be changed."
        );
    }

    [Test]
    public async Task DeleteArchiveConfigTemplateAsync_UnmanagedTemplate_ThrowsInvalidOperationException()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync();
        await service.CreateAsync(
            "Unmanaged template",
            ReleaseType.Unmanaged,
            ReleaseContentType.Movie,
            releaseGroup.Id,
            CancellationToken.None
        );
        var archiveConfigTemplate = await dbContext.ArchiveConfigTemplates.SingleAsync();

        // Act
        var result = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.DeleteArchiveConfigTemplateAsync(
                archiveConfigTemplate.Id,
                CancellationToken.None
            )
        );

        // Assert
        result.Message.ShouldBe(
            "Archive config templates for unmanaged release templates cannot be changed."
        );
    }

    [Test]
    public async Task CreateUploadConfigTemplateAsync_UnmanagedTemplate_UsesFixedArchiveConfigTemplate()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync();
        var releaseTemplateId = await service.CreateAsync(
            "Unmanaged template",
            ReleaseType.Unmanaged,
            ReleaseContentType.Movie,
            releaseGroup.Id,
            CancellationToken.None
        );
        var hosterRegistration = await AddHosterRegistrationAsync();

        // Act
        await service.CreateUploadConfigTemplateAsync(
            releaseTemplateId,
            null,
            hosterRegistration.Id,
            archiveConfigTemplateId: 12345,
            premiumOnlyDownload: true,
            CancellationToken.None
        );

        // Assert
        var archiveConfigTemplate = await dbContext.ArchiveConfigTemplates.SingleAsync();
        var uploadConfigTemplate = await dbContext.UploadConfigTemplates.SingleAsync();

        uploadConfigTemplate.ArchiveConfigTemplateId.ShouldBe(archiveConfigTemplate.Id);
        uploadConfigTemplate.PremiumOnlyDownload.ShouldBeTrue();
    }

    [Test]
    public async Task UpdateUploadConfigTemplateAsync_TemplateExists_UpdatesUploadConfigTemplate()
    {
        // Arrange
        var seed = await AddReleaseTemplateWithChildrenAsync();
        var secondHosterRegistration = await AddHosterRegistrationAsync("Second hoster");
        var secondArchiveConfigTemplateId = await service.CreateArchiveConfigTemplateAsync(
            seed.ReleaseTemplateId,
            "ZIP Forum B",
            "/tmp/second-archives",
            "zip",
            null,
            2048,
            false,
            CancellationToken.None
        );

        // Act
        await service.UpdateUploadConfigTemplateAsync(
            seed.UploadConfigTemplateId,
            "  Mirror upload  ",
            secondHosterRegistration.Id,
            secondArchiveConfigTemplateId,
            true,
            CancellationToken.None
        );

        // Assert
        var result = await dbContext.UploadConfigTemplates.SingleAsync(u =>
            u.Id == seed.UploadConfigTemplateId
        );

        result.Name.ShouldBe("Mirror upload");
        result.HosterRegistrationId.ShouldBe(secondHosterRegistration.Id);
        result.ArchiveConfigTemplateId.ShouldBe(secondArchiveConfigTemplateId);
        result.PremiumOnlyDownload.ShouldBeTrue();
    }

    [Test]
    public async Task DeleteUploadConfigTemplateAsync_TemplateExists_RemovesUploadConfigTemplateWithLinkCrypters()
    {
        // Arrange
        var seed = await AddReleaseTemplateWithChildrenAsync();

        // Act
        await service.DeleteUploadConfigTemplateAsync(
            seed.UploadConfigTemplateId,
            CancellationToken.None
        );

        // Assert
        (await dbContext.UploadConfigTemplates.AnyAsync()).ShouldBeFalse();
        (await dbContext.UploadConfigLinkCrypterTemplates.AnyAsync()).ShouldBeFalse();
        (
            await dbContext.ArchiveConfigTemplates.AnyAsync(a =>
                a.Id == seed.ArchiveConfigTemplateId
            )
        ).ShouldBeTrue();
    }

    [Test]
    public async Task UpdateUploadConfigLinkCrypterTemplateAsync_TemplateExists_UpdatesLinkCrypterPassword()
    {
        // Arrange
        var seed = await AddReleaseTemplateWithChildrenAsync();

        // Act
        await service.UpdateUploadConfigLinkCrypterTemplateAsync(
            seed.UploadConfigLinkCrypterTemplateId,
            " ",
            true,
            true,
            true,
            CancellationToken.None
        );

        // Assert
        var result = await dbContext.UploadConfigLinkCrypterTemplates.SingleAsync();

        result.Password.ShouldBeNull();
        result.EnableCaptcha.ShouldBeTrue();
        result.EnableContainerDownload.ShouldBeTrue();
        result.EnableClickAndLoad.ShouldBeTrue();
    }

    [Test]
    public async Task DeleteUploadConfigLinkCrypterTemplateAsync_TemplateExists_RemovesLinkCrypterTemplate()
    {
        // Arrange
        var seed = await AddReleaseTemplateWithChildrenAsync();

        // Act
        await service.DeleteUploadConfigLinkCrypterTemplateAsync(
            seed.UploadConfigLinkCrypterTemplateId,
            CancellationToken.None
        );

        // Assert
        (await dbContext.UploadConfigLinkCrypterTemplates.AnyAsync()).ShouldBeFalse();
        (
            await dbContext.UploadConfigTemplates.AnyAsync(u => u.Id == seed.UploadConfigTemplateId)
        ).ShouldBeTrue();
    }

    [Test]
    public async Task CreateTemplateFromReleaseAsync_ExistingRelease_CopiesReleaseConfiguration()
    {
        // Arrange
        var release = await AddReleaseWithConfigsAsync();

        // Act
        var result = await service.CreateTemplateFromReleaseAsync(
            release.Id,
            "Template from release",
            CancellationToken.None
        );

        // Assert
        var template = await dbContext
            .ReleaseTemplates.AsSplitQuery()
            .Include(t => t.ArchiveConfigTemplates)
            .Include(t => t.UploadConfigTemplates)
                .ThenInclude(u => u.LinkCrypterTemplates)
            .SingleAsync(t => t.Id == result);

        template.Name.ShouldBe("Template from release");
        template.ReleaseType.ShouldBe(release.ReleaseType);
        template.ReleaseGroupId.ShouldBe(release.ReleaseGroupId);

        var archiveConfigTemplate = template.ArchiveConfigTemplates.Single();
        archiveConfigTemplate.Name.ShouldBe("RAR Forum A");
        archiveConfigTemplate.ArchiveFilesBasePath.ShouldBe("/tmp/archives");
        archiveConfigTemplate.ArchiverName.ShouldBe("rar");
        archiveConfigTemplate.ArchivePassword.ShouldBe("archive-secret");
        archiveConfigTemplate.ArchiveFileSizeMb.ShouldBe(1024);
        archiveConfigTemplate.UseReleaseNameAsArchiveName.ShouldBeTrue();

        var uploadConfigTemplate = template.UploadConfigTemplates.Single();
        uploadConfigTemplate.Name.ShouldBeNull();
        uploadConfigTemplate.PremiumOnlyDownload.ShouldBeTrue();
        uploadConfigTemplate.CollectionUploadSlotKey.ShouldBe("forum-a");
        uploadConfigTemplate.CollectionUploadSlotName.ShouldBe("Forum A");
        uploadConfigTemplate.CollectionUploadSlotIsRequired.ShouldBeTrue();
        uploadConfigTemplate.CollectionUploadSlotPasswordPolicy.ShouldBe(
            CollectionUploadSlotPasswordPolicy.MustEqualExpectedValue
        );
        uploadConfigTemplate.CollectionUploadSlotExpectedArchivePassword.ShouldBe("archive-secret");
        uploadConfigTemplate.ArchiveConfigTemplateId.ShouldBe(archiveConfigTemplate.Id);
        uploadConfigTemplate.LinkCrypterTemplates.Single().Password.ShouldBe("container-secret");
    }

    private ReleaseTemplateRepository CreateRepository()
    {
        var archiverFactory = new Mock<IArchiverFactory>();
        archiverFactory
            .Setup(factory => factory.GetArchivers())
            .Returns([new ArchiverDto("RAR", "rar", ".rar")]);
        var linkCrypterFactory = new Mock<ILinkCrypterFactory>();
        linkCrypterFactory
            .Setup(factory => factory.GetLinkCrypters())
            .Returns([new LinkCrypterDto("Test crypter", "TestCrypter", [], true, true, true)]);
        return new ReleaseTemplateRepository(
            dbContext,
            dbContext,
            archiverFactory.Object,
            linkCrypterFactory.Object
        );
    }

    private async Task<ReleaseTemplateSeed> AddReleaseTemplateWithChildrenAsync()
    {
        var releaseGroup = await AddReleaseGroupAsync();
        var releaseTemplate = await AddReleaseTemplateAsync(releaseGroup.Id);
        var hosterRegistration = await AddHosterRegistrationAsync();
        var linkCrypterRegistration = await AddLinkCrypterRegistrationAsync();
        var archiveConfigTemplateId = await service.CreateArchiveConfigTemplateAsync(
            releaseTemplate.Id,
            "RAR Forum A",
            "/tmp/archives",
            "rar",
            "archive-secret",
            1024,
            true,
            CancellationToken.None
        );
        var uploadConfigTemplateId = await service.CreateUploadConfigTemplateAsync(
            releaseTemplate.Id,
            null,
            hosterRegistration.Id,
            archiveConfigTemplateId,
            true,
            CancellationToken.None
        );
        var uploadConfigLinkCrypterTemplateId =
            await service.CreateUploadConfigLinkCrypterTemplateAsync(
                uploadConfigTemplateId,
                linkCrypterRegistration.Id,
                "container-secret",
                true,
                true,
                true,
                CancellationToken.None
            );

        return new ReleaseTemplateSeed(
            releaseTemplate.Id,
            archiveConfigTemplateId,
            uploadConfigTemplateId,
            uploadConfigLinkCrypterTemplateId
        );
    }

    [Test]
    public async Task CreateCollectionImageUploadConfigTemplateAsync_DetectionDisabled_Throws()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync();
        var releaseTemplate = await AddReleaseTemplateAsync(releaseGroup.Id);
        var imageHosterRegistration = await AddImageHosterRegistrationAsync();

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.CreateCollectionImageUploadConfigTemplateAsync(
                releaseTemplate.Id,
                "Series cover",
                imageHosterRegistration.Id,
                CancellationToken.None
            )
        );

        var hasTemplate = await dbContext.CollectionImageUploadConfigTemplates.AnyAsync();
        hasTemplate.ShouldBeFalse();
    }

    [Test]
    public async Task CreateCollectionImageUploadConfigTemplateAsync_DetectionEnabled_PersistsTemplate()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync();
        var releaseTemplate = await AddReleaseTemplateAsync(releaseGroup.Id);
        releaseTemplate.ReleaseCollectionDetectionMode =
            ReleaseCollectionDetectionMode.SeriesEpisodePattern;
        await dbContext.SaveChangesAsync();
        var imageHosterRegistration = await AddImageHosterRegistrationAsync();

        // Act
        await service.CreateCollectionImageUploadConfigTemplateAsync(
            releaseTemplate.Id,
            "Series cover",
            imageHosterRegistration.Id,
            CancellationToken.None
        );

        // Assert
        var template = await dbContext.CollectionImageUploadConfigTemplates.SingleAsync();
        template.ReleaseTemplateId.ShouldBe(releaseTemplate.Id);
        template.ImageHosterRegistrationId.ShouldBe(imageHosterRegistration.Id);
        template.Name.ShouldBe("Series cover");
    }

    [Test]
    public async Task UpdateAsync_DetectionDisabled_RemovesCollectionImageConfigTemplates()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync();
        var releaseTemplate = await AddReleaseTemplateAsync(releaseGroup.Id);
        releaseTemplate.ReleaseCollectionDetectionMode =
            ReleaseCollectionDetectionMode.SeriesEpisodePattern;
        await dbContext.SaveChangesAsync();
        var imageHosterRegistration = await AddImageHosterRegistrationAsync();
        await service.CreateCollectionImageUploadConfigTemplateAsync(
            releaseTemplate.Id,
            "Series cover",
            imageHosterRegistration.Id,
            CancellationToken.None
        );

        // Act
        await service.UpdateAsync(
            releaseTemplate.Id,
            "Managed template",
            ReleaseType.Managed,
            ReleaseContentType.Movie,
            releaseGroup.Id,
            ReleaseCollectionDetectionMode.Disabled,
            cancellationToken: CancellationToken.None
        );

        // Assert
        var hasTemplate = await dbContext.CollectionImageUploadConfigTemplates.AnyAsync();
        hasTemplate.ShouldBeFalse();
    }

    private async Task<ImageHosterRegistration> AddImageHosterRegistrationAsync()
    {
        var imageHosterRegistration = new ImageHosterRegistration
        {
            Name = "ImgBB",
            ImageHosterClassName = "ImgBb",
            SerializedConfig = "{}",
            IsActive = true,
        };

        dbContext.ImageHosterRegistrations.Add(imageHosterRegistration);
        await dbContext.SaveChangesAsync();

        return imageHosterRegistration;
    }

    private async Task<ReleaseGroup> AddReleaseGroupAsync(string name = "Managed releases")
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = name,
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };

        dbContext.ReleaseGroups.Add(releaseGroup);
        await dbContext.SaveChangesAsync();

        return releaseGroup;
    }

    private async Task<ReleaseTemplate> AddReleaseTemplateAsync(int releaseGroupId)
    {
        var releaseTemplate = new ReleaseTemplate
        {
            Name = "Managed template",
            ReleaseType = ReleaseType.Managed,
            ReleaseGroupId = releaseGroupId,
        };

        dbContext.ReleaseTemplates.Add(releaseTemplate);
        await dbContext.SaveChangesAsync();

        return releaseTemplate;
    }

    private async Task<HosterRegistration> AddHosterRegistrationAsync(
        string name = "Primary hoster"
    )
    {
        var hosterRegistration = new HosterRegistration
        {
            Name = name,
            SerializedConfig = "{}",
            HosterClassName = "TestHoster",
            IsActive = true,
        };

        dbContext.HosterRegistrations.Add(hosterRegistration);
        await dbContext.SaveChangesAsync();

        return hosterRegistration;
    }

    private async Task<LinkCrypterRegistration> AddLinkCrypterRegistrationAsync()
    {
        var linkCrypterRegistration = new LinkCrypterRegistration
        {
            Name = "Main crypter",
            LinkCrypterClassName = "TestCrypter",
            SerializedConfig = "{}",
            IsActive = true,
        };

        dbContext.LinkCrypterRegistrations.Add(linkCrypterRegistration);
        await dbContext.SaveChangesAsync();

        return linkCrypterRegistration;
    }

    private async Task<Release> AddReleaseWithConfigsAsync()
    {
        var releaseGroup = await AddReleaseGroupAsync();
        var hosterRegistration = await AddHosterRegistrationAsync();
        var linkCrypterRegistration = await AddLinkCrypterRegistrationAsync();
        var release = new Release
        {
            Name = "Bearcat.Release.001",
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/tmp/releases/Bearcat.Release.001",
            ReleaseGroupId = releaseGroup.Id,
            ReleaseCollection = new ReleaseCollection
            {
                ReleaseGroupId = releaseGroup.Id,
                Key = "bearcat.release.001.collection",
                Name = "Bearcat Release 001 Collection",
                CreatedAt = DateTime.UtcNow,
            },
            ArchiveConfigs =
            [
                new ArchiveConfig
                {
                    Name = "RAR Forum A",
                    ArchiveFilesBasePath = "/tmp/archives",
                    ArchiverName = "rar",
                    ArchiveNamePrefix = "Bearcat.Release.001",
                    ArchivePassword = "archive-secret",
                    ArchiveFileSizeMb = 1024,
                },
            ],
        };
        release.UploadConfigs =
        [
            new UploadConfig
            {
                Name = hosterRegistration.Name,
                CollectionUploadSlot = new CollectionUploadSlot
                {
                    ReleaseCollection = release.ReleaseCollection!,
                    Key = "forum-a",
                    Name = "Forum A",
                    IsRequired = true,
                    PasswordPolicy = CollectionUploadSlotPasswordPolicy.MustEqualExpectedValue,
                    ExpectedArchivePassword = "archive-secret",
                },
                HosterRegistrationId = hosterRegistration.Id,
                ArchiveConfig = release.ArchiveConfigs.Single(),
                PremiumOnlyDownload = true,
                LinkCrypters =
                [
                    new UploadConfigLinkCrypter
                    {
                        LinkCrypterRegistrationId = linkCrypterRegistration.Id,
                        Password = "container-secret",
                    },
                ],
            },
        ];

        dbContext.Releases.Add(release);
        await dbContext.SaveChangesAsync();

        return release;
    }

    private sealed record ReleaseTemplateSeed(
        int ReleaseTemplateId,
        int ArchiveConfigTemplateId,
        int UploadConfigTemplateId,
        int UploadConfigLinkCrypterTemplateId
    );
}

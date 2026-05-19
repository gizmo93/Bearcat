using Bearcat.Abstractions.Archiver;
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
            ["forum-a", "", "forum-b"],
            CancellationToken.None
        );
        await service.CreateUploadConfigLinkCrypterTemplateAsync(
            uploadConfigTemplateId,
            linkCrypterRegistration.Id,
            "container-secret",
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
        uploadConfigTemplate.LinksDistributedTo.ShouldBe(["forum-a", "forum-b"]);

        var linkCrypterTemplate = uploadConfigTemplate.LinkCrypterTemplates.Single();
        linkCrypterTemplate.LinkCrypterRegistrationId.ShouldBe(linkCrypterRegistration.Id);
        linkCrypterTemplate.Password.ShouldBe("container-secret");
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
            ReleaseType.Unmanaged,
            secondReleaseGroup.Id,
            CancellationToken.None
        );

        // Assert
        var result = await dbContext.ReleaseTemplates.SingleAsync();

        result.Name.ShouldBe("Updated template");
        result.ReleaseType.ShouldBe(ReleaseType.Unmanaged);
        result.ReleaseGroupId.ShouldBe(secondReleaseGroup.Id);
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
            [" forum-c ", "", "forum-d"],
            CancellationToken.None
        );

        // Assert
        var result = await dbContext.UploadConfigTemplates.SingleAsync(u =>
            u.Id == seed.UploadConfigTemplateId
        );

        result.Name.ShouldBe("Mirror upload");
        result.HosterRegistrationId.ShouldBe(secondHosterRegistration.Id);
        result.ArchiveConfigTemplateId.ShouldBe(secondArchiveConfigTemplateId);
        result.LinksDistributedTo.ShouldBe(["forum-c", "forum-d"]);
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
            CancellationToken.None
        );

        // Assert
        var result = await dbContext.UploadConfigLinkCrypterTemplates.SingleAsync();

        result.Password.ShouldBeNull();
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
        uploadConfigTemplate.LinksDistributedTo.ShouldBe(["forum-a", "forum-b"]);
        uploadConfigTemplate.ArchiveConfigTemplateId.ShouldBe(archiveConfigTemplate.Id);
        uploadConfigTemplate.LinkCrypterTemplates.Single().Password.ShouldBe("container-secret");
    }

    private ReleaseTemplateRepository CreateRepository()
    {
        var archiverFactory = new Mock<IArchiverFactory>();
        archiverFactory
            .Setup(factory => factory.GetArchivers())
            .Returns([new ArchiverDto("RAR", "rar", ".rar")]);
        return new ReleaseTemplateRepository(dbContext, dbContext, archiverFactory.Object);
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
            ["forum-a", "forum-b"],
            CancellationToken.None
        );
        var uploadConfigLinkCrypterTemplateId =
            await service.CreateUploadConfigLinkCrypterTemplateAsync(
                uploadConfigTemplateId,
                linkCrypterRegistration.Id,
                "container-secret",
                CancellationToken.None
            );

        return new ReleaseTemplateSeed(
            releaseTemplate.Id,
            archiveConfigTemplateId,
            uploadConfigTemplateId,
            uploadConfigLinkCrypterTemplateId
        );
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
                HosterRegistrationId = hosterRegistration.Id,
                ArchiveConfig = release.ArchiveConfigs.Single(),
                LinksDistributedTo = ["forum-a", "forum-b"],
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

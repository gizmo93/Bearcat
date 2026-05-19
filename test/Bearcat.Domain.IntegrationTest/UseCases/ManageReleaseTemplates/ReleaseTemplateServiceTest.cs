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

    private async Task<ReleaseGroup> AddReleaseGroupAsync()
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = "Managed releases",
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

    private async Task<HosterRegistration> AddHosterRegistrationAsync()
    {
        var hosterRegistration = new HosterRegistration
        {
            Name = "Primary hoster",
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
}

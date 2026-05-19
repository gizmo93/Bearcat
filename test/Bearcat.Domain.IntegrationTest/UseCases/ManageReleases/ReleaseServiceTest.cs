using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleases;

public class ReleaseServiceTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private ReleaseService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        service = new ReleaseService(new ReleaseWriteRepository(dbContext));
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task CreateAsync_ValidRelease_PersistsReleaseAndReturnsId()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync("Managed releases");

        // Act
        var result = await service.CreateAsync(
            "Bearcat.Release.001",
            "/tmp/release",
            ReleaseType.Managed,
            releaseGroup.Id,
            CancellationToken.None
        );

        // Assert
        var release = await dbContext.Releases.SingleAsync();

        result.ShouldBeGreaterThan(0);
        release.ShouldNotBeNull();
        release.Id.ShouldBe(result);
        release.Name.ShouldBe("Bearcat.Release.001");
        release.ReleaseFolderPath.ShouldBe("/tmp/release");
        release.ReleaseType.ShouldBe(ReleaseType.Managed);
        release.ReleaseGroupId.ShouldBe(releaseGroup.Id);
    }

    [Test]
    public async Task UpdateAsync_ReleaseExists_UpdatesNameAndReleaseGroup()
    {
        // Arrange
        var firstGroup = await AddReleaseGroupAsync("First group");
        var secondGroup = await AddReleaseGroupAsync("Second group");
        var release = await AddReleaseAsync(firstGroup.Id);

        // Act
        await service.UpdateAsync(
            release.Id,
            "Bearcat.Release.Updated",
            secondGroup.Id,
            CancellationToken.None
        );

        // Assert
        var result = await dbContext.Releases.SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(release.Id);
        result.Name.ShouldBe("Bearcat.Release.Updated");
        result.ReleaseGroupId.ShouldBe(secondGroup.Id);
        result.ReleaseFolderPath.ShouldBe("/tmp/release");
    }

    [Test]
    public async Task UpdateReleaseGroupAsync_ReleaseIdsAreEmpty_DoesNotChangeReleases()
    {
        // Arrange
        var firstGroup = await AddReleaseGroupAsync("First group");
        var secondGroup = await AddReleaseGroupAsync("Second group");
        var release = await AddReleaseAsync(firstGroup.Id);

        // Act
        await service.UpdateReleaseGroupAsync([], secondGroup.Id, CancellationToken.None);

        // Assert
        var result = await dbContext.Releases.SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(release.Id);
        result.ReleaseGroupId.ShouldBe(firstGroup.Id);
    }

    [Test]
    public async Task UpdateReleaseGroupAsync_ReleasesExist_UpdatesReleaseGroups()
    {
        // Arrange
        var firstGroup = await AddReleaseGroupAsync("First group");
        var secondGroup = await AddReleaseGroupAsync("Second group");
        var firstRelease = await AddReleaseAsync(firstGroup.Id, "Bearcat.Release.001");
        var secondRelease = await AddReleaseAsync(firstGroup.Id, "Bearcat.Release.002");

        // Act
        await service.UpdateReleaseGroupAsync(
            [firstRelease.Id, secondRelease.Id],
            secondGroup.Id,
            CancellationToken.None
        );

        // Assert
        var result = await dbContext.Releases.OrderBy(r => r.Id).ToListAsync();

        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldAllBe(r => r.ReleaseGroupId == secondGroup.Id);
    }

    [Test]
    public async Task DeleteAsync_ReleaseExists_RemovesRelease()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync("Managed releases");
        var release = await AddReleaseAsync(releaseGroup.Id);

        // Act
        await service.DeleteAsync(release.Id, CancellationToken.None);

        // Assert
        var result = await dbContext.Releases.AnyAsync();

        result.ShouldBeFalse();
    }

    [Test]
    public async Task CreateFromTemplateAsync_ValidTemplate_PersistsReleaseWithConfigs()
    {
        // Arrange
        var seed = await AddReleaseTemplateAsync();

        // Act
        var result = await service.CreateFromTemplateAsync(
            seed.ReleaseTemplateId,
            "/tmp/releases/Bearcat.Release.Template",
            null,
            CancellationToken.None
        );

        // Assert
        var release = await dbContext
            .Releases.AsSplitQuery()
            .Include(r => r.ArchiveConfigs)
            .Include(r => r.UploadConfigs)
                .ThenInclude(u => u.LinkCrypters)
            .SingleAsync(r => r.Id == result);

        release.Name.ShouldBe("Bearcat.Release.Template");
        release.ReleaseFolderPath.ShouldBe("/tmp/releases/Bearcat.Release.Template");
        release.ReleaseType.ShouldBe(ReleaseType.Managed);
        release.ReleaseGroupId.ShouldBe(seed.ReleaseGroupId);

        var archiveConfig = release.ArchiveConfigs.Single();
        archiveConfig.Name.ShouldBe("RAR Forum A");
        archiveConfig.ArchiveFilesBasePath.ShouldBe("/tmp/archives");
        archiveConfig.ArchiverName.ShouldBe("rar");
        archiveConfig.ArchivePassword.ShouldBe("archive-secret");
        archiveConfig.ArchiveFileSizeMb.ShouldBe(1024);
        archiveConfig.ArchiveNamePrefix.ShouldBe(release.Name);

        var uploadConfig = release.UploadConfigs.Single();
        uploadConfig.Name.ShouldBe("Primary hoster");
        uploadConfig.HosterRegistrationId.ShouldBe(seed.HosterRegistrationId);
        uploadConfig.ArchiveConfigId.ShouldBe(archiveConfig.Id);
        uploadConfig.LinksDistributedTo.ShouldBe(["forum-a", "forum-b"]);

        var linkCrypter = uploadConfig.LinkCrypters.Single();
        linkCrypter.LinkCrypterRegistrationId.ShouldBe(seed.LinkCrypterRegistrationId);
        linkCrypter.Password.ShouldBe("container-secret");
    }

    private async Task<ReleaseGroup> AddReleaseGroupAsync(string name)
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

    private async Task<Release> AddReleaseAsync(
        int releaseGroupId,
        string name = "Bearcat.Release.001"
    )
    {
        var release = new Release
        {
            Name = name,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/tmp/release",
            ReleaseGroupId = releaseGroupId,
        };

        dbContext.Releases.Add(release);
        await dbContext.SaveChangesAsync();

        return release;
    }

    private async Task<ReleaseTemplateSeed> AddReleaseTemplateAsync()
    {
        var releaseGroup = await AddReleaseGroupAsync("Template group");
        var hosterRegistration = new HosterRegistration
        {
            Name = "Primary hoster",
            SerializedConfig = "{}",
            HosterClassName = "TestHoster",
            IsActive = true,
        };
        var linkCrypterRegistration = new LinkCrypterRegistration
        {
            Name = "Main crypter",
            LinkCrypterClassName = "TestCrypter",
            SerializedConfig = "{}",
            IsActive = true,
        };
        var releaseTemplate = new ReleaseTemplate
        {
            Name = "Managed template",
            ReleaseType = ReleaseType.Managed,
            ReleaseGroup = releaseGroup,
            ArchiveConfigTemplates =
            [
                new ArchiveConfigTemplate
                {
                    Name = "RAR Forum A",
                    ArchiveFilesBasePath = "/tmp/archives",
                    ArchiverName = "rar",
                    ArchivePassword = "archive-secret",
                    ArchiveFileSizeMb = 1024,
                    UseReleaseNameAsArchiveName = true,
                },
            ],
        };
        releaseTemplate.UploadConfigTemplates =
        [
            new UploadConfigTemplate
            {
                ReleaseTemplate = releaseTemplate,
                ArchiveConfigTemplate = releaseTemplate.ArchiveConfigTemplates.Single(),
                HosterRegistration = hosterRegistration,
                Name = null,
                LinksDistributedTo = ["forum-a", "", "forum-b"],
                LinkCrypterTemplates =
                [
                    new UploadConfigLinkCrypterTemplate
                    {
                        LinkCrypterRegistration = linkCrypterRegistration,
                        Password = "container-secret",
                    },
                ],
            },
        ];

        dbContext.ReleaseTemplates.Add(releaseTemplate);
        await dbContext.SaveChangesAsync();

        return new ReleaseTemplateSeed(
            releaseTemplate.Id,
            releaseGroup.Id,
            hosterRegistration.Id,
            linkCrypterRegistration.Id
        );
    }

    private sealed record ReleaseTemplateSeed(
        int ReleaseTemplateId,
        int ReleaseGroupId,
        int HosterRegistrationId,
        int LinkCrypterRegistrationId
    );
}

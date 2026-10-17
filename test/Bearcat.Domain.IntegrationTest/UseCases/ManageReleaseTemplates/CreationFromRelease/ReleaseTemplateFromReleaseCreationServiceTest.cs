using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.CreationFromRelease;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleaseTemplates.CreationFromRelease;

public class ReleaseTemplateFromReleaseCreationServiceTest(DatabaseProvider databaseProvider)
    : ReleaseTemplateIntegrationTest(databaseProvider)
{
    private ReleaseTemplateFromReleaseCreationService service = null!;

    [SetUp]
    public void Setup()
    {
        service = new ReleaseTemplateFromReleaseCreationService(CreateRepository());
    }

    [Test]
    public async Task CreateReleaseTemplateFromReleaseAsync_ExistingRelease_CopiesReleaseConfiguration()
    {
        // Arrange
        var release = await AddReleaseWithConfigsAsync();

        // Act
        var result = await service.CreateReleaseTemplateFromReleaseAsync(
            release.Id,
            "Template from release",
            CancellationToken.None
        );

        // Assert
        var template = await DbContext
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
        archiveConfigTemplate.PackReleaseFolderAsRootFolder.ShouldBeFalse();
        archiveConfigTemplate.CreateNonceFile.ShouldBeFalse();

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

    [Test]
    public async Task CreateReleaseTemplateFromReleaseAsync_ArchiveConfigWithAdditionalArchiveContents_CopiesContentsToArchiveConfigTemplate()
    {
        // Arrange
        var release = await AddReleaseWithConfigsAsync();
        var additionalArchiveContents = await AddAdditionalArchiveContentsAsync();
        release.ArchiveConfigs.Single().AdditionalArchiveContents = additionalArchiveContents;
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await service.CreateReleaseTemplateFromReleaseAsync(
            release.Id,
            "Template from release",
            CancellationToken.None
        );

        // Assert
        var archiveConfigTemplate = await CreateDbContext()
            .ArchiveConfigTemplates.Include(template => template.AdditionalArchiveContents)
            .SingleAsync(template => template.ReleaseTemplateId == result);

        archiveConfigTemplate
            .AdditionalArchiveContents.Select(content => content.Id)
            .Order()
            .ShouldBe(additionalArchiveContents.Select(content => content.Id).Order());
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
                    PackReleaseFolderAsRootFolder = false,
                    CreateNonceFile = false,
                },
            ],
        };
        release.UploadConfigs =
        [
            new UploadConfig
            {
                UploadSource = UploadSource.ArchiveFiles,
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

        DbContext.Releases.Add(release);
        await DbContext.SaveChangesAsync();

        return release;
    }
}

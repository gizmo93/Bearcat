using Bearcat.Domain.UseCases.ManageReleaseTemplates;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.ArchiveConfigTemplates;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.UploadConfigLinkCrypterTemplates;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.UploadConfigTemplates;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleaseTemplates.UploadConfigTemplates;

public class UploadConfigTemplateServiceTest(DatabaseProvider databaseProvider)
    : ReleaseTemplateIntegrationTest(databaseProvider)
{
    private UploadConfigTemplateService service = null!;
    private ReleaseTemplateService releaseTemplateService = null!;
    private ArchiveConfigTemplateService archiveConfigTemplateService = null!;
    private UploadConfigLinkCrypterTemplateService uploadConfigLinkCrypterTemplateService = null!;

    [SetUp]
    public void Setup()
    {
        service = new UploadConfigTemplateService(CreateRepository());
        releaseTemplateService = new ReleaseTemplateService(CreateRepository());
        archiveConfigTemplateService = new ArchiveConfigTemplateService(CreateRepository());
        uploadConfigLinkCrypterTemplateService = new UploadConfigLinkCrypterTemplateService(
            CreateRepository()
        );
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
        var archiveConfigTemplateId = (
            await archiveConfigTemplateService.CreateAsync(
                releaseTemplate.Id,
                new ArchiveConfigTemplateSettings(
                    Name: "RAR Forum A",
                    ArchiveFilesBasePath: "/tmp/archives",
                    ArchiverName: "rar",
                    ArchivePassword: "archive-secret",
                    ArchiveFileSizeMb: 1024,
                    UseReleaseNameAsArchiveName: true,
                    PackReleaseFolderAsRootFolder: false,
                    CreateNonceFile: false,
                    AdditionalArchiveContentIds: []
                ),
                CancellationToken.None
            )
        )
            .ArchiveConfigTemplateId!
            .Value;
        var uploadConfigTemplateId = await service.CreateAsync(
            releaseTemplate.Id,
            new UploadConfigTemplateSettings(
                Name: " ",
                UploadTarget: UploadTarget.ForHosterRegistration(hosterRegistration.Id),
                ArchiveConfigTemplateId: archiveConfigTemplateId,
                PremiumOnlyDownload: true
            ),
            CancellationToken.None
        );
        await uploadConfigLinkCrypterTemplateService.CreateAsync(
            uploadConfigTemplateId,
            linkCrypterRegistration.Id,
            "container-secret",
            true,
            true,
            true,
            cancellationToken: CancellationToken.None
        );

        // Assert
        var template = await DbContext
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
        archiveConfigTemplate.PackReleaseFolderAsRootFolder.ShouldBeFalse();
        archiveConfigTemplate.CreateNonceFile.ShouldBeFalse();
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
    public async Task CreateAsync_UnmanagedTemplate_UsesFixedArchiveConfigTemplate()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync();
        var releaseTemplateId = await releaseTemplateService.CreateAsync(
            "Unmanaged template",
            ReleaseType.Unmanaged,
            ReleaseContentType.Movie,
            releaseGroup.Id,
            cancellationToken: CancellationToken.None
        );
        var hosterRegistration = await AddHosterRegistrationAsync();

        // Act
        await service.CreateAsync(
            releaseTemplateId,
            new UploadConfigTemplateSettings(
                Name: null,
                UploadTarget: UploadTarget.ForHosterRegistration(hosterRegistration.Id),
                ArchiveConfigTemplateId: 12345,
                PremiumOnlyDownload: true
            ),
            CancellationToken.None
        );

        // Assert
        var archiveConfigTemplate = await DbContext.ArchiveConfigTemplates.SingleAsync();
        var uploadConfigTemplate = await DbContext.UploadConfigTemplates.SingleAsync();

        uploadConfigTemplate.ArchiveConfigTemplateId.ShouldBe(archiveConfigTemplate.Id);
        uploadConfigTemplate.PremiumOnlyDownload.ShouldBeTrue();
    }

    [Test]
    public async Task UpdateAsync_TemplateExists_UpdatesUploadConfigTemplate()
    {
        // Arrange
        var seed = await AddReleaseTemplateWithChildrenAsync();
        var secondHosterRegistration = await AddHosterRegistrationAsync("Second hoster");
        var secondArchiveConfigTemplateId = (
            await archiveConfigTemplateService.CreateAsync(
                seed.ReleaseTemplateId,
                new ArchiveConfigTemplateSettings(
                    Name: "ZIP Forum B",
                    ArchiveFilesBasePath: "/tmp/second-archives",
                    ArchiverName: "zip",
                    ArchivePassword: null,
                    ArchiveFileSizeMb: 2048,
                    UseReleaseNameAsArchiveName: false,
                    PackReleaseFolderAsRootFolder: true,
                    CreateNonceFile: true,
                    AdditionalArchiveContentIds: []
                ),
                CancellationToken.None
            )
        )
            .ArchiveConfigTemplateId!
            .Value;

        // Act
        await service.UpdateAsync(
            seed.UploadConfigTemplateId,
            new UploadConfigTemplateSettings(
                Name: "  Mirror upload  ",
                UploadTarget: UploadTarget.ForHosterRegistration(secondHosterRegistration.Id),
                ArchiveConfigTemplateId: secondArchiveConfigTemplateId,
                PremiumOnlyDownload: true
            ),
            CancellationToken.None
        );

        // Assert
        var result = await DbContext.UploadConfigTemplates.SingleAsync(u =>
            u.Id == seed.UploadConfigTemplateId
        );

        result.Name.ShouldBe("Mirror upload");
        result.HosterRegistrationId.ShouldBe(secondHosterRegistration.Id);
        result.ArchiveConfigTemplateId.ShouldBe(secondArchiveConfigTemplateId);
        result.PremiumOnlyDownload.ShouldBeTrue();
    }

    [Test]
    public async Task DeleteAsync_TemplateExists_RemovesUploadConfigTemplateWithLinkCrypters()
    {
        // Arrange
        var seed = await AddReleaseTemplateWithChildrenAsync();

        // Act
        await service.DeleteAsync(seed.UploadConfigTemplateId, CancellationToken.None);

        // Assert
        (await DbContext.UploadConfigTemplates.AnyAsync()).ShouldBeFalse();
        (await DbContext.UploadConfigLinkCrypterTemplates.AnyAsync()).ShouldBeFalse();
        (
            await DbContext.ArchiveConfigTemplates.AnyAsync(a =>
                a.Id == seed.ArchiveConfigTemplateId
            )
        ).ShouldBeTrue();
    }
}

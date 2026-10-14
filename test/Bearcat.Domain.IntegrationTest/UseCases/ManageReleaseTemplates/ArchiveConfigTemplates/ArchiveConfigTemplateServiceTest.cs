using Bearcat.Domain.UseCases.ManageReleaseTemplates;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.ArchiveConfigTemplates;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleaseTemplates.ArchiveConfigTemplates;

public class ArchiveConfigTemplateServiceTest(DatabaseProvider databaseProvider)
    : ReleaseTemplateIntegrationTest(databaseProvider)
{
    private ArchiveConfigTemplateService service = null!;
    private ReleaseTemplateService releaseTemplateService = null!;

    [SetUp]
    public void Setup()
    {
        service = new ArchiveConfigTemplateService(CreateRepository());
        releaseTemplateService = new ReleaseTemplateService(CreateRepository());
    }

    [Test]
    public async Task UpdateAsync_TemplateExists_UpdatesArchiveConfigTemplate()
    {
        // Arrange
        var seed = await AddReleaseTemplateWithChildrenAsync();

        // Act
        await service.UpdateAsync(
            seed.ArchiveConfigTemplateId,
            new ArchiveConfigTemplateSettings(
                Name: "ZIP Forum B",
                ArchiveFilesBasePath: "/tmp/updated-archives",
                ArchiverName: "zip",
                ArchivePassword: " ",
                ArchiveFileSizeMb: 2048,
                UseReleaseNameAsArchiveName: false,
                PackReleaseFolderAsRootFolder: false,
                CreateNonceFile: false,
                AdditionalArchiveContentIds: []
            ),
            CancellationToken.None
        );

        // Assert
        var result = await DbContext.ArchiveConfigTemplates.SingleAsync();

        result.Name.ShouldBe("ZIP Forum B");
        result.ArchiveFilesBasePath.ShouldBe("/tmp/updated-archives");
        result.ArchiverName.ShouldBe("zip");
        result.ArchivePassword.ShouldBeNull();
        result.ArchiveFileSizeMb.ShouldBe(2048);
        result.UseReleaseNameAsArchiveName.ShouldBeFalse();
        result.PackReleaseFolderAsRootFolder.ShouldBeFalse();
        result.CreateNonceFile.ShouldBeFalse();
    }

    [Test]
    public async Task DeleteAsync_TemplateExists_RemovesArchiveConfigTemplate()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync();
        var releaseTemplate = await AddReleaseTemplateAsync(releaseGroup.Id);
        var archiveConfigTemplateId = (
            await service.CreateAsync(
                releaseTemplate.Id,
                new ArchiveConfigTemplateSettings(
                    Name: "RAR Forum A",
                    ArchiveFilesBasePath: "/tmp/archives",
                    ArchiverName: "rar",
                    ArchivePassword: "archive-secret",
                    ArchiveFileSizeMb: 1024,
                    UseReleaseNameAsArchiveName: true,
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
        await service.DeleteAsync(archiveConfigTemplateId, CancellationToken.None);

        // Assert
        (await DbContext.ArchiveConfigTemplates.AnyAsync()).ShouldBeFalse();
        (await DbContext.ReleaseTemplates.AnyAsync()).ShouldBeTrue();
    }

    [Test]
    public async Task CreateAsync_UnmanagedTemplate_ThrowsInvalidOperationException()
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

        // Act
        var result = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.CreateAsync(
                releaseTemplateId,
                new ArchiveConfigTemplateSettings(
                    Name: "RAR Forum A",
                    ArchiveFilesBasePath: "/tmp/archives",
                    ArchiverName: "rar",
                    ArchivePassword: "archive-secret",
                    ArchiveFileSizeMb: 1024,
                    UseReleaseNameAsArchiveName: true,
                    PackReleaseFolderAsRootFolder: true,
                    CreateNonceFile: true,
                    AdditionalArchiveContentIds: []
                ),
                CancellationToken.None
            )
        );

        // Assert
        result.Message.ShouldBe(
            "Archive config templates for unmanaged release templates cannot be changed."
        );
    }

    [Test]
    public async Task UpdateAsync_UnmanagedTemplate_ThrowsInvalidOperationException()
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
        var archiveConfigTemplate = await DbContext.ArchiveConfigTemplates.SingleAsync();

        // Act
        var result = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.UpdateAsync(
                archiveConfigTemplate.Id,
                new ArchiveConfigTemplateSettings(
                    Name: "RAR Forum A",
                    ArchiveFilesBasePath: "/tmp/archives",
                    ArchiverName: "rar",
                    ArchivePassword: "archive-secret",
                    ArchiveFileSizeMb: 1024,
                    UseReleaseNameAsArchiveName: true,
                    PackReleaseFolderAsRootFolder: true,
                    CreateNonceFile: true,
                    AdditionalArchiveContentIds: []
                ),
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
    public async Task DeleteAsync_UnmanagedTemplate_ThrowsInvalidOperationException()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync();
        await releaseTemplateService.CreateAsync(
            "Unmanaged template",
            ReleaseType.Unmanaged,
            ReleaseContentType.Movie,
            releaseGroup.Id,
            cancellationToken: CancellationToken.None
        );
        var archiveConfigTemplate = await DbContext.ArchiveConfigTemplates.SingleAsync();

        // Act
        var result = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.DeleteAsync(archiveConfigTemplate.Id, CancellationToken.None)
        );

        // Assert
        result.Message.ShouldBe(
            "Archive config templates for unmanaged release templates cannot be changed."
        );
    }

    [Test]
    public async Task CreateAsync_WithAdditionalArchiveContentIds_PersistsAssignments()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync();
        var releaseTemplate = await AddReleaseTemplateAsync(releaseGroup.Id);
        var additionalArchiveContents = await AddAdditionalArchiveContentsAsync();

        // Act
        var result = await service.CreateAsync(
            releaseTemplate.Id,
            new ArchiveConfigTemplateSettings(
                Name: "RAR Forum A",
                ArchiveFilesBasePath: "/tmp/archives",
                ArchiverName: "rar",
                ArchivePassword: null,
                ArchiveFileSizeMb: 1024,
                UseReleaseNameAsArchiveName: true,
                PackReleaseFolderAsRootFolder: true,
                CreateNonceFile: true,
                AdditionalArchiveContentIds: additionalArchiveContents
                    .Select(content => content.Id)
                    .ToList()
            ),
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var archiveConfigTemplate = await CreateDbContext()
            .ArchiveConfigTemplates.Include(template => template.AdditionalArchiveContents)
            .SingleAsync(template => template.Id == result.ArchiveConfigTemplateId);

        archiveConfigTemplate
            .AdditionalArchiveContents.Select(content => content.Id)
            .Order()
            .ShouldBe(additionalArchiveContents.Select(content => content.Id).Order());
    }

    [Test]
    public async Task CreateAsync_ContentsWithSameEntryName_ReturnsEntryNameCollisionAndDoesNotPersist()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync();
        var releaseTemplate = await AddReleaseTemplateAsync(releaseGroup.Id);
        var collidingAdditionalArchiveContents =
            await AddAdditionalArchiveContentsWithSameEntryNameAsync();

        // Act
        var result = await service.CreateAsync(
            releaseTemplate.Id,
            new ArchiveConfigTemplateSettings(
                Name: "RAR Forum A",
                ArchiveFilesBasePath: "/tmp/archives",
                ArchiverName: "rar",
                ArchivePassword: null,
                ArchiveFileSizeMb: 1024,
                UseReleaseNameAsArchiveName: true,
                PackReleaseFolderAsRootFolder: true,
                CreateNonceFile: true,
                AdditionalArchiveContentIds: collidingAdditionalArchiveContents
                    .Select(content => content.Id)
                    .ToList()
            ),
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ArchiveConfigTemplateId.ShouldBeNull();
        result.AdditionalArchiveContentEntryNameCollisions.ShouldHaveSingleItem();
        result
            .AdditionalArchiveContentEntryNameCollisions[0]
            .AdditionalArchiveContentNames.ShouldBe(["Premium ad file", "Premium ad text"]);
        (await CreateDbContext().ArchiveConfigTemplates.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task UpdateAsync_SameAdditionalArchiveContentIds_KeepsAssignedContents()
    {
        // Arrange
        var seed = await AddReleaseTemplateWithChildrenAsync();
        var additionalArchiveContents = await AddAdditionalArchiveContentsAsync();
        await AssignAdditionalArchiveContentsAsync(
            seed.ArchiveConfigTemplateId,
            additionalArchiveContents
        );

        // Act
        await service.UpdateAsync(
            seed.ArchiveConfigTemplateId,
            new ArchiveConfigTemplateSettings(
                Name: "ZIP Forum B",
                ArchiveFilesBasePath: "/tmp/updated-archives",
                ArchiverName: "zip",
                ArchivePassword: null,
                ArchiveFileSizeMb: 2048,
                UseReleaseNameAsArchiveName: false,
                PackReleaseFolderAsRootFolder: true,
                CreateNonceFile: true,
                AdditionalArchiveContentIds: additionalArchiveContents
                    .Select(content => content.Id)
                    .ToList()
            ),
            CancellationToken.None
        );

        // Assert
        var archiveConfigTemplate = await CreateDbContext()
            .ArchiveConfigTemplates.Include(template => template.AdditionalArchiveContents)
            .SingleAsync(template => template.Id == seed.ArchiveConfigTemplateId);

        archiveConfigTemplate.Name.ShouldBe("ZIP Forum B");
        archiveConfigTemplate
            .AdditionalArchiveContents.Select(content => content.Id)
            .Order()
            .ShouldBe(additionalArchiveContents.Select(content => content.Id).Order());
    }

    [Test]
    public async Task UpdateAsync_DifferentAdditionalArchiveContentIds_ReplacesAssignmentsAndKeepsContents()
    {
        // Arrange
        var seed = await AddReleaseTemplateWithChildrenAsync();
        var additionalArchiveContents = await AddAdditionalArchiveContentsAsync();
        await AssignAdditionalArchiveContentsAsync(
            seed.ArchiveConfigTemplateId,
            [additionalArchiveContents[0]]
        );

        // Act
        var result = await service.UpdateAsync(
            seed.ArchiveConfigTemplateId,
            new ArchiveConfigTemplateSettings(
                Name: "RAR Forum A",
                ArchiveFilesBasePath: "/tmp/archives",
                ArchiverName: "rar",
                ArchivePassword: null,
                ArchiveFileSizeMb: 1024,
                UseReleaseNameAsArchiveName: true,
                PackReleaseFolderAsRootFolder: true,
                CreateNonceFile: true,
                AdditionalArchiveContentIds: [additionalArchiveContents[1].Id]
            ),
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var verificationDbContext = CreateDbContext();
        var archiveConfigTemplate = await verificationDbContext
            .ArchiveConfigTemplates.Include(template => template.AdditionalArchiveContents)
            .SingleAsync(template => template.Id == seed.ArchiveConfigTemplateId);

        archiveConfigTemplate
            .AdditionalArchiveContents.Select(content => content.Id)
            .ShouldBe([additionalArchiveContents[1].Id]);
        (await verificationDbContext.AdditionalArchiveContents.CountAsync()).ShouldBe(
            additionalArchiveContents.Count
        );
    }

    [Test]
    public async Task UpdateAsync_EmptyAdditionalArchiveContentIds_RemovesAssignmentsAndKeepsContents()
    {
        // Arrange
        var seed = await AddReleaseTemplateWithChildrenAsync();
        var additionalArchiveContents = await AddAdditionalArchiveContentsAsync();
        await AssignAdditionalArchiveContentsAsync(
            seed.ArchiveConfigTemplateId,
            additionalArchiveContents
        );

        // Act
        var result = await service.UpdateAsync(
            seed.ArchiveConfigTemplateId,
            new ArchiveConfigTemplateSettings(
                Name: "RAR Forum A",
                ArchiveFilesBasePath: "/tmp/archives",
                ArchiverName: "rar",
                ArchivePassword: null,
                ArchiveFileSizeMb: 1024,
                UseReleaseNameAsArchiveName: true,
                PackReleaseFolderAsRootFolder: true,
                CreateNonceFile: true,
                AdditionalArchiveContentIds: []
            ),
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var verificationDbContext = CreateDbContext();
        var archiveConfigTemplate = await verificationDbContext
            .ArchiveConfigTemplates.Include(template => template.AdditionalArchiveContents)
            .SingleAsync(template => template.Id == seed.ArchiveConfigTemplateId);

        archiveConfigTemplate.AdditionalArchiveContents.ShouldBeEmpty();
        (await verificationDbContext.AdditionalArchiveContents.CountAsync()).ShouldBe(
            additionalArchiveContents.Count
        );
    }

    [Test]
    public async Task UpdateAsync_ContentsWithSameEntryName_ReturnsEntryNameCollisionAndKeepsTemplateUnchanged()
    {
        // Arrange
        var seed = await AddReleaseTemplateWithChildrenAsync();
        var collidingAdditionalArchiveContents =
            await AddAdditionalArchiveContentsWithSameEntryNameAsync();

        // Act
        var result = await service.UpdateAsync(
            seed.ArchiveConfigTemplateId,
            new ArchiveConfigTemplateSettings(
                Name: "ZIP Forum B",
                ArchiveFilesBasePath: "/tmp/updated-archives",
                ArchiverName: "zip",
                ArchivePassword: null,
                ArchiveFileSizeMb: 2048,
                UseReleaseNameAsArchiveName: false,
                PackReleaseFolderAsRootFolder: true,
                CreateNonceFile: true,
                AdditionalArchiveContentIds: collidingAdditionalArchiveContents
                    .Select(content => content.Id)
                    .ToList()
            ),
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.AdditionalArchiveContentEntryNameCollisions.ShouldHaveSingleItem();
        result
            .AdditionalArchiveContentEntryNameCollisions[0]
            .EntryName.ShouldBe("premium.txt", StringCompareShould.IgnoreCase);

        var archiveConfigTemplate = await CreateDbContext()
            .ArchiveConfigTemplates.Include(template => template.AdditionalArchiveContents)
            .SingleAsync(template => template.Id == seed.ArchiveConfigTemplateId);

        archiveConfigTemplate.Name.ShouldBe("RAR Forum A");
        archiveConfigTemplate.AdditionalArchiveContents.ShouldBeEmpty();
    }

    [Test]
    public async Task DeleteAsync_TemplateHasAdditionalArchiveContents_RemovesAssignmentsAndKeepsContents()
    {
        // Arrange
        var seed = await AddReleaseTemplateWithChildrenAsync();
        var additionalArchiveContents = await AddAdditionalArchiveContentsAsync();
        await AssignAdditionalArchiveContentsAsync(
            seed.ArchiveConfigTemplateId,
            additionalArchiveContents
        );

        // Act
        await service.DeleteAsync(seed.ArchiveConfigTemplateId, CancellationToken.None);

        // Assert
        var verificationDbContext = CreateDbContext();
        (await verificationDbContext.ArchiveConfigTemplates.AnyAsync()).ShouldBeFalse();
        (await verificationDbContext.AdditionalArchiveContents.CountAsync()).ShouldBe(
            additionalArchiveContents.Count
        );
    }
}

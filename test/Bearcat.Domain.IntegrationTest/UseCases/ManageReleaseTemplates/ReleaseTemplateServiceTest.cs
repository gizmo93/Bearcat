using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseTemplates;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.CollectionImageUploadConfigTemplates;
using Bearcat.Domain.UseCases.ManageUploadConfigs.UploadSources;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleaseTemplates;

public class ReleaseTemplateServiceTest(DatabaseProvider databaseProvider)
    : ReleaseTemplateIntegrationTest(databaseProvider)
{
    private ReleaseTemplateService service = null!;

    [SetUp]
    public void Setup()
    {
        service = new ReleaseTemplateService(CreateRepository());
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
            cancellationToken: CancellationToken.None
        );

        // Assert
        var template = await DbContext.ReleaseTemplates.SingleAsync();

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
            cancellationToken: CancellationToken.None
        );

        // Assert
        var template = await DbContext
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
            cancellationToken: CancellationToken.None
        );

        // Assert
        var result = await DbContext.ReleaseTemplates.SingleAsync();

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
                cancellationToken: CancellationToken.None
            )
        );

        // Assert
        result.Message.ShouldBe("Release template type cannot be changed.");
    }

    [Test]
    public async Task UpdateAsync_VideoUploadConfigTemplateAndNonVideoContentType_ThrowsInvalidUploadSourceException()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync();
        var releaseTemplate = await AddReleaseTemplateAsync(releaseGroup.Id);
        var hosterRegistration = await AddHosterRegistrationAsync();
        DbContext.UploadConfigTemplates.Add(
            new UploadConfigTemplate
            {
                ReleaseTemplateId = releaseTemplate.Id,
                UploadSource = UploadSource.VideoFiles,
                HosterRegistrationId = hosterRegistration.Id,
            }
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await Should.ThrowAsync<InvalidUploadSourceException>(async () =>
            await service.UpdateAsync(
                releaseTemplate.Id,
                "Updated template",
                ReleaseType.Managed,
                ReleaseContentType.Game,
                releaseGroup.Id,
                cancellationToken: CancellationToken.None
            )
        );

        // Assert
        result.Message.ShouldBe(
            "The content type must stay movie or TV show episode while the release template has video file upload config templates."
        );
        DbContext.ChangeTracker.Clear();
        (await DbContext.ReleaseTemplates.SingleAsync()).ReleaseContentType.ShouldBe(
            ReleaseContentType.Movie
        );
    }

    [Test]
    public async Task DeleteAsync_TemplateExists_RemovesTemplateWithChildren()
    {
        // Arrange
        var seed = await AddReleaseTemplateWithChildrenAsync();

        // Act
        await service.DeleteAsync(seed.ReleaseTemplateId, CancellationToken.None);

        // Assert
        (await DbContext.ReleaseTemplates.AnyAsync()).ShouldBeFalse();
        (await DbContext.ArchiveConfigTemplates.AnyAsync()).ShouldBeFalse();
        (await DbContext.UploadConfigTemplates.AnyAsync()).ShouldBeFalse();
        (await DbContext.UploadConfigLinkCrypterTemplates.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task UpdateAsync_ArchiveConfigTemplateHasAdditionalArchiveContents_KeepsAssignedContents()
    {
        // Arrange
        var seed = await AddReleaseTemplateWithChildrenAsync();
        var releaseGroup = await AddReleaseGroupAsync("Updated releases");
        var additionalArchiveContents = await AddAdditionalArchiveContentsAsync();
        await AssignAdditionalArchiveContentsAsync(
            seed.ArchiveConfigTemplateId,
            additionalArchiveContents
        );

        // Act
        await service.UpdateAsync(
            seed.ReleaseTemplateId,
            "Updated template",
            ReleaseType.Managed,
            ReleaseContentType.Movie,
            releaseGroup.Id,
            cancellationToken: CancellationToken.None
        );

        // Assert
        var archiveConfigTemplate = await CreateDbContext()
            .ArchiveConfigTemplates.Include(template => template.AdditionalArchiveContents)
            .SingleAsync(template => template.Id == seed.ArchiveConfigTemplateId);

        archiveConfigTemplate
            .AdditionalArchiveContents.Select(content => content.Id)
            .Order()
            .ShouldBe(additionalArchiveContents.Select(content => content.Id).Order());
    }

    [Test]
    public async Task UpdateAsync_DetectionDisabled_RemovesCollectionImageConfigTemplates()
    {
        // Arrange
        var collectionImageUploadConfigTemplateService =
            new CollectionImageUploadConfigTemplateService(CreateRepository());
        var releaseGroup = await AddReleaseGroupAsync();
        var releaseTemplate = await AddReleaseTemplateAsync(releaseGroup.Id);
        releaseTemplate.ReleaseCollectionDetectionMode =
            ReleaseCollectionDetectionMode.SeriesEpisodePattern;
        await DbContext.SaveChangesAsync();
        var imageHosterRegistration = await AddImageHosterRegistrationAsync();
        await collectionImageUploadConfigTemplateService.CreateAsync(
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
        var hasTemplate = await DbContext.CollectionImageUploadConfigTemplates.AnyAsync();
        hasTemplate.ShouldBeFalse();
    }
}

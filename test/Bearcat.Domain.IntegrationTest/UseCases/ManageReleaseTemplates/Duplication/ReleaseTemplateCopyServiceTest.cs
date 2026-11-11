using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Duplication;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleaseTemplates.Duplication;

public class ReleaseTemplateCopyServiceTest(DatabaseProvider databaseProvider)
    : ReleaseTemplateIntegrationTest(databaseProvider)
{
    private ReleaseTemplateCopyService service = null!;

    [SetUp]
    public void Setup()
    {
        service = new ReleaseTemplateCopyService(CreateRepository());
    }

    [Test]
    public async Task DuplicateAsync_TemplateWithChildren_CopiesTemplateAndAllChildrenIntoNewTemplate()
    {
        // Arrange
        var seed = await AddReleaseTemplateWithChildrenAsync();
        var additionalArchiveContents = await AddAdditionalArchiveContentsAsync();
        await AssignAdditionalArchiveContentsAsync(
            seed.ArchiveConfigTemplateId,
            additionalArchiveContents
        );
        var imageHosterRegistration = await AddImageHosterRegistrationAsync();
        var sourceTemplate = await DbContext.ReleaseTemplates.SingleAsync();
        sourceTemplate.ReleaseContentType = ReleaseContentType.TvShowEpisode;
        sourceTemplate.ReleaseCollectionDetectionMode =
            ReleaseCollectionDetectionMode.SeriesEpisodePattern;
        sourceTemplate.ReleaseCollectionPattern = "pattern";
        sourceTemplate.ReleaseCollectionKeyTemplate = "key";
        sourceTemplate.ReleaseCollectionNameTemplate = "name";
        sourceTemplate.ImageUploadConfigTemplates.Add(
            new ImageUploadConfigTemplate
            {
                ImageHosterRegistrationId = imageHosterRegistration.Id,
                Name = "Screenshots",
            }
        );
        sourceTemplate.CollectionImageUploadConfigTemplates.Add(
            new CollectionImageUploadConfigTemplate
            {
                ImageHosterRegistrationId = imageHosterRegistration.Id,
                Name = "Cover",
            }
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var duplicatedTemplateId = await service.CopyAsync(
            seed.ReleaseTemplateId,
            "  Managed template (copy)  ",
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var templates = await DbContext
            .ReleaseTemplates.AsSplitQuery()
            .Include(t => t.ArchiveConfigTemplates)
                .ThenInclude(a => a.AdditionalArchiveContents)
            .Include(t => t.UploadConfigTemplates)
                .ThenInclude(u => u.LinkCrypterTemplates)
            .Include(t => t.ImageUploadConfigTemplates)
            .Include(t => t.CollectionImageUploadConfigTemplates)
            .ToListAsync();
        templates.Count.ShouldBe(2);
        var unchangedSourceTemplate = templates.Single(t => t.Id == seed.ReleaseTemplateId);
        var duplicatedTemplate = templates.Single(t => t.Id == duplicatedTemplateId);

        unchangedSourceTemplate.Name.ShouldBe("Managed template");
        unchangedSourceTemplate
            .ArchiveConfigTemplates.Single()
            .Id.ShouldBe(seed.ArchiveConfigTemplateId);
        unchangedSourceTemplate
            .UploadConfigTemplates.Single()
            .Id.ShouldBe(seed.UploadConfigTemplateId);

        duplicatedTemplate.Name.ShouldBe("Managed template (copy)");
        duplicatedTemplate.ReleaseType.ShouldBe(ReleaseType.Managed);
        duplicatedTemplate.ReleaseContentType.ShouldBe(ReleaseContentType.TvShowEpisode);
        duplicatedTemplate.ReleaseGroupId.ShouldBe(unchangedSourceTemplate.ReleaseGroupId);
        duplicatedTemplate.ReleaseCollectionDetectionMode.ShouldBe(
            ReleaseCollectionDetectionMode.SeriesEpisodePattern
        );
        duplicatedTemplate.ReleaseCollectionPattern.ShouldBe("pattern");
        duplicatedTemplate.ReleaseCollectionKeyTemplate.ShouldBe("key");
        duplicatedTemplate.ReleaseCollectionNameTemplate.ShouldBe("name");

        var duplicatedArchiveConfigTemplate = duplicatedTemplate.ArchiveConfigTemplates.Single();
        duplicatedArchiveConfigTemplate.Id.ShouldNotBe(seed.ArchiveConfigTemplateId);
        duplicatedArchiveConfigTemplate.Name.ShouldBe("RAR Forum A");
        duplicatedArchiveConfigTemplate.ArchiveFilesBasePath.ShouldBe("/tmp/archives");
        duplicatedArchiveConfigTemplate.ArchiverName.ShouldBe("rar");
        duplicatedArchiveConfigTemplate.ArchivePassword.ShouldBe("archive-secret");
        duplicatedArchiveConfigTemplate.ArchiveFileSizeMb.ShouldBe(1024);
        duplicatedArchiveConfigTemplate.UseReleaseNameAsArchiveName.ShouldBeTrue();
        duplicatedArchiveConfigTemplate
            .AdditionalArchiveContents.Select(content => content.Id)
            .ShouldBe(additionalArchiveContents.Select(content => content.Id), ignoreOrder: true);

        var sourceUploadConfigTemplate = unchangedSourceTemplate.UploadConfigTemplates.Single();
        var duplicatedUploadConfigTemplate = duplicatedTemplate.UploadConfigTemplates.Single();
        duplicatedUploadConfigTemplate.Id.ShouldNotBe(seed.UploadConfigTemplateId);
        duplicatedUploadConfigTemplate.ArchiveConfigTemplateId.ShouldBe(
            duplicatedArchiveConfigTemplate.Id
        );
        duplicatedUploadConfigTemplate.UploadSource.ShouldBe(UploadSource.ArchiveFiles);
        duplicatedUploadConfigTemplate.HosterRegistrationId.ShouldBe(
            sourceUploadConfigTemplate.HosterRegistrationId
        );
        duplicatedUploadConfigTemplate.PremiumOnlyDownload.ShouldBeTrue();

        var sourceLinkCrypterTemplate = sourceUploadConfigTemplate.LinkCrypterTemplates.Single();
        var duplicatedLinkCrypterTemplate =
            duplicatedUploadConfigTemplate.LinkCrypterTemplates.Single();
        duplicatedLinkCrypterTemplate.Id.ShouldNotBe(seed.UploadConfigLinkCrypterTemplateId);
        duplicatedLinkCrypterTemplate.LinkCrypterRegistrationId.ShouldBe(
            sourceLinkCrypterTemplate.LinkCrypterRegistrationId
        );
        duplicatedLinkCrypterTemplate.Password.ShouldBe("container-secret");

        var duplicatedImageUploadConfigTemplate =
            duplicatedTemplate.ImageUploadConfigTemplates.Single();
        duplicatedImageUploadConfigTemplate.ImageHosterRegistrationId.ShouldBe(
            imageHosterRegistration.Id
        );
        duplicatedImageUploadConfigTemplate.Name.ShouldBe("Screenshots");

        var duplicatedCollectionImageUploadConfigTemplate =
            duplicatedTemplate.CollectionImageUploadConfigTemplates.Single();
        duplicatedCollectionImageUploadConfigTemplate.ImageHosterRegistrationId.ShouldBe(
            imageHosterRegistration.Id
        );
        duplicatedCollectionImageUploadConfigTemplate.Name.ShouldBe("Cover");
    }
}

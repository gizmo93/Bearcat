using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.ArchiveConfigTemplates;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.UploadConfigLinkCrypterTemplates;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.UploadConfigTemplates;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleaseTemplates;

public abstract class ReleaseTemplateIntegrationTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    protected async Task<List<AdditionalArchiveContent>> AddAdditionalArchiveContentsAsync()
    {
        List<AdditionalArchiveContent> additionalArchiveContents =
        [
            new AdditionalArchiveContent
            {
                Name = "Premium ad folder",
                Type = AdditionalArchiveContentType.Path,
                SourcePath = "/data/ads/premium",
            },
            new AdditionalArchiveContent
            {
                Name = "Premium ad text",
                Type = AdditionalArchiveContentType.TextFile,
                FileName = "buy premium via me.txt",
                TextContent = "Buy premium via my link.",
            },
        ];

        DbContext.AdditionalArchiveContents.AddRange(additionalArchiveContents);
        await DbContext.SaveChangesAsync();

        return additionalArchiveContents;
    }

    protected async Task<
        List<AdditionalArchiveContent>
    > AddAdditionalArchiveContentsWithSameEntryNameAsync()
    {
        List<AdditionalArchiveContent> additionalArchiveContents =
        [
            new AdditionalArchiveContent
            {
                Name = "Premium ad file",
                Type = AdditionalArchiveContentType.Path,
                SourcePath = "/data/ads/Premium.txt",
            },
            new AdditionalArchiveContent
            {
                Name = "Premium ad text",
                Type = AdditionalArchiveContentType.TextFile,
                FileName = "premium.TXT",
                TextContent = "Buy premium via my link.",
            },
        ];

        DbContext.AdditionalArchiveContents.AddRange(additionalArchiveContents);
        await DbContext.SaveChangesAsync();

        return additionalArchiveContents;
    }

    protected async Task AssignAdditionalArchiveContentsAsync(
        int archiveConfigTemplateId,
        List<AdditionalArchiveContent> additionalArchiveContents
    )
    {
        var archiveConfigTemplate = await DbContext.ArchiveConfigTemplates.SingleAsync(template =>
            template.Id == archiveConfigTemplateId
        );
        archiveConfigTemplate.AdditionalArchiveContents = additionalArchiveContents;
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
    }

    protected ReleaseTemplateRepository CreateRepository()
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
            DbContext,
            DbContext,
            archiverFactory.Object,
            linkCrypterFactory.Object
        );
    }

    protected async Task<ReleaseTemplateSeed> AddReleaseTemplateWithChildrenAsync()
    {
        var archiveConfigTemplateService = new ArchiveConfigTemplateService(CreateRepository());
        var uploadConfigTemplateService = new UploadConfigTemplateService(CreateRepository());
        var uploadConfigLinkCrypterTemplateService = new UploadConfigLinkCrypterTemplateService(
            CreateRepository()
        );
        var releaseGroup = await AddReleaseGroupAsync();
        var releaseTemplate = await AddReleaseTemplateAsync(releaseGroup.Id);
        var hosterRegistration = await AddHosterRegistrationAsync();
        var linkCrypterRegistration = await AddLinkCrypterRegistrationAsync();
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
                    PackReleaseFolderAsRootFolder: true,
                    CreateNonceFile: true,
                    AdditionalArchiveContentIds: []
                ),
                CancellationToken.None
            )
        )
            .ArchiveConfigTemplateId!
            .Value;
        var uploadConfigTemplateId = await uploadConfigTemplateService.CreateAsync(
            releaseTemplate.Id,
            new UploadConfigTemplateSettings(
                Name: null,
                UploadTarget: UploadTarget.ForHosterRegistration(hosterRegistration.Id),
                ArchiveConfigTemplateId: archiveConfigTemplateId,
                PremiumOnlyDownload: true
            ),
            CancellationToken.None
        );
        var uploadConfigLinkCrypterTemplateId =
            await uploadConfigLinkCrypterTemplateService.CreateAsync(
                uploadConfigTemplateId,
                linkCrypterRegistration.Id,
                "container-secret",
                true,
                true,
                true,
                cancellationToken: CancellationToken.None
            );

        return new ReleaseTemplateSeed(
            releaseTemplate.Id,
            archiveConfigTemplateId,
            uploadConfigTemplateId,
            uploadConfigLinkCrypterTemplateId
        );
    }

    protected async Task<ImageHosterRegistration> AddImageHosterRegistrationAsync(
        string name = "ImgBB"
    )
    {
        var imageHosterRegistration = new ImageHosterRegistration
        {
            Name = name,
            ImageHosterClassName = "ImgBb",
            SerializedConfig = "{}",
            IsActive = true,
        };

        DbContext.ImageHosterRegistrations.Add(imageHosterRegistration);
        await DbContext.SaveChangesAsync();

        return imageHosterRegistration;
    }

    protected async Task<ReleaseGroup> AddReleaseGroupAsync(string name = "Managed releases")
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = name,
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };

        DbContext.ReleaseGroups.Add(releaseGroup);
        await DbContext.SaveChangesAsync();

        return releaseGroup;
    }

    protected async Task<ReleaseTemplate> AddReleaseTemplateAsync(int releaseGroupId)
    {
        var releaseTemplate = new ReleaseTemplate
        {
            Name = "Managed template",
            ReleaseType = ReleaseType.Managed,
            ReleaseGroupId = releaseGroupId,
        };

        DbContext.ReleaseTemplates.Add(releaseTemplate);
        await DbContext.SaveChangesAsync();

        return releaseTemplate;
    }

    protected async Task<HosterRegistration> AddHosterRegistrationAsync(
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

        DbContext.HosterRegistrations.Add(hosterRegistration);
        await DbContext.SaveChangesAsync();

        return hosterRegistration;
    }

    protected async Task<LinkCrypterRegistration> AddLinkCrypterRegistrationAsync()
    {
        var linkCrypterRegistration = new LinkCrypterRegistration
        {
            Name = "Main crypter",
            LinkCrypterClassName = "TestCrypter",
            SerializedConfig = "{}",
            IsActive = true,
        };

        DbContext.LinkCrypterRegistrations.Add(linkCrypterRegistration);
        await DbContext.SaveChangesAsync();

        return linkCrypterRegistration;
    }

    protected sealed record ReleaseTemplateSeed(
        int ReleaseTemplateId,
        int ArchiveConfigTemplateId,
        int UploadConfigTemplateId,
        int UploadConfigLinkCrypterTemplateId
    );
}

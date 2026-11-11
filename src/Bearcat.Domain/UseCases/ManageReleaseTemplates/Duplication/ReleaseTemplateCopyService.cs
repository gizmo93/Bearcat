using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;

namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.Duplication;

public class ReleaseTemplateCopyService(IReleaseTemplateWriteRepository writeRepository)
{
    public async Task<int> CopyAsync(
        int releaseTemplateId,
        string name,
        CancellationToken cancellationToken = default
    )
    {
        var sourceTemplate = await writeRepository.GetByIdWithChildrenAsync(
            releaseTemplateId,
            cancellationToken
        );

        var duplicatedArchiveConfigTemplatesBySourceId =
            sourceTemplate.ArchiveConfigTemplates.ToDictionary(
                archiveConfigTemplate => archiveConfigTemplate.Id,
                DuplicateArchiveConfigTemplate
            );

        var duplicatedTemplate = new ReleaseTemplate
        {
            Name = name.Trim(),
            ReleaseType = sourceTemplate.ReleaseType,
            ReleaseContentType = sourceTemplate.ReleaseContentType,
            ReleaseGroupId = sourceTemplate.ReleaseGroupId,
            ReleaseCollectionDetectionMode = sourceTemplate.ReleaseCollectionDetectionMode,
            ReleaseCollectionPattern = sourceTemplate.ReleaseCollectionPattern,
            ReleaseCollectionKeyTemplate = sourceTemplate.ReleaseCollectionKeyTemplate,
            ReleaseCollectionNameTemplate = sourceTemplate.ReleaseCollectionNameTemplate,
            ArchiveConfigTemplates = duplicatedArchiveConfigTemplatesBySourceId.Values.ToList(),
            UploadConfigTemplates = sourceTemplate
                .UploadConfigTemplates.Select(uploadConfigTemplate =>
                    DuplicateUploadConfigTemplate(
                        uploadConfigTemplate,
                        uploadConfigTemplate.ArchiveConfigTemplateId is { } archiveConfigTemplateId
                            ? duplicatedArchiveConfigTemplatesBySourceId[archiveConfigTemplateId]
                            : null
                    )
                )
                .ToList(),
            ImageUploadConfigTemplates = sourceTemplate
                .ImageUploadConfigTemplates.Select(DuplicateImageUploadConfigTemplate)
                .ToList(),
            CollectionImageUploadConfigTemplates = sourceTemplate
                .CollectionImageUploadConfigTemplates.Select(
                    DuplicateCollectionImageUploadConfigTemplate
                )
                .ToList(),
        };

        writeRepository.Add(duplicatedTemplate);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return duplicatedTemplate.Id;
    }

    private static ArchiveConfigTemplate DuplicateArchiveConfigTemplate(
        ArchiveConfigTemplate sourceTemplate
    )
    {
        return new ArchiveConfigTemplate
        {
            Name = sourceTemplate.Name,
            ArchiveFilesBasePath = sourceTemplate.ArchiveFilesBasePath,
            ArchiverName = sourceTemplate.ArchiverName,
            ArchivePassword = sourceTemplate.ArchivePassword,
            ArchiveFileSizeMb = sourceTemplate.ArchiveFileSizeMb,
            UseReleaseNameAsArchiveName = sourceTemplate.UseReleaseNameAsArchiveName,
            PackReleaseFolderAsRootFolder = sourceTemplate.PackReleaseFolderAsRootFolder,
            CreateNonceFile = sourceTemplate.CreateNonceFile,
            AdditionalArchiveContents = sourceTemplate.AdditionalArchiveContents.ToList(),
        };
    }

    private static UploadConfigTemplate DuplicateUploadConfigTemplate(
        UploadConfigTemplate sourceTemplate,
        ArchiveConfigTemplate? duplicatedArchiveConfigTemplate
    )
    {
        return new UploadConfigTemplate
        {
            UploadSource = sourceTemplate.UploadSource,
            ArchiveConfigTemplate = duplicatedArchiveConfigTemplate,
            HosterRegistrationId = sourceTemplate.HosterRegistrationId,
            HosterAccountPoolId = sourceTemplate.HosterAccountPoolId,
            Name = sourceTemplate.Name,
            PremiumOnlyDownload = sourceTemplate.PremiumOnlyDownload,
            CollectionUploadSlotKey = sourceTemplate.CollectionUploadSlotKey,
            CollectionUploadSlotName = sourceTemplate.CollectionUploadSlotName,
            CollectionUploadSlotIsRequired = sourceTemplate.CollectionUploadSlotIsRequired,
            CollectionUploadSlotPasswordPolicy = sourceTemplate.CollectionUploadSlotPasswordPolicy,
            CollectionUploadSlotExpectedArchivePassword =
                sourceTemplate.CollectionUploadSlotExpectedArchivePassword,
            LinkCrypterTemplates = sourceTemplate
                .LinkCrypterTemplates.Select(DuplicateLinkCrypterTemplate)
                .ToList(),
        };
    }

    private static UploadConfigLinkCrypterTemplate DuplicateLinkCrypterTemplate(
        UploadConfigLinkCrypterTemplate sourceTemplate
    )
    {
        return new UploadConfigLinkCrypterTemplate
        {
            LinkCrypterRegistrationId = sourceTemplate.LinkCrypterRegistrationId,
            ContainerScope = sourceTemplate.ContainerScope,
            Password = sourceTemplate.Password,
            EnableCaptcha = sourceTemplate.EnableCaptcha,
            EnableContainerDownload = sourceTemplate.EnableContainerDownload,
            EnableClickAndLoad = sourceTemplate.EnableClickAndLoad,
        };
    }

    private static ImageUploadConfigTemplate DuplicateImageUploadConfigTemplate(
        ImageUploadConfigTemplate sourceTemplate
    )
    {
        return new ImageUploadConfigTemplate
        {
            ImageHosterRegistrationId = sourceTemplate.ImageHosterRegistrationId,
            Name = sourceTemplate.Name,
        };
    }

    private static CollectionImageUploadConfigTemplate DuplicateCollectionImageUploadConfigTemplate(
        CollectionImageUploadConfigTemplate sourceTemplate
    )
    {
        return new CollectionImageUploadConfigTemplate
        {
            ImageHosterRegistrationId = sourceTemplate.ImageHosterRegistrationId,
            Name = sourceTemplate.Name,
        };
    }
}

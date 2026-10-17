using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.CreationFromRelease;

public class ReleaseTemplateFromReleaseCreationService(
    IReleaseTemplateWriteRepository writeRepository
)
{
    public async Task<int> CreateReleaseTemplateFromReleaseAsync(
        int releaseId,
        string name,
        CancellationToken cancellationToken = default
    )
    {
        var release = await writeRepository.GetReleaseForTemplateCreationAsync(
            releaseId,
            cancellationToken
        );

        var releaseTemplate = new ReleaseTemplate
        {
            Name = name,
            ReleaseType = release.ReleaseType,
            ReleaseContentType = release.ReleaseContentType,
            ReleaseGroupId = release.ReleaseGroupId,
        };

        if (release.ReleaseType is ReleaseType.Managed)
        {
            var archiveConfigTemplatesByArchiveConfigId =
                CreateArchiveConfigTemplatesByArchiveConfigId(release);
            releaseTemplate.ArchiveConfigTemplates =
                archiveConfigTemplatesByArchiveConfigId.Values.ToList();
            releaseTemplate.UploadConfigTemplates = release
                .UploadConfigs.Select(uploadConfig =>
                    CreateUploadConfigTemplate(
                        uploadConfig,
                        uploadConfig.ArchiveConfigId is { } archiveConfigId
                            ? archiveConfigTemplatesByArchiveConfigId[archiveConfigId]
                            : null
                    )
                )
                .ToList();
        }
        else
        {
            var unmanagedArchiveConfigTemplate = UnmanagedArchiveConfigTemplates.Create();
            releaseTemplate.ArchiveConfigTemplates = [unmanagedArchiveConfigTemplate];
            releaseTemplate.UploadConfigTemplates = release
                .UploadConfigs.Select(uploadConfig =>
                    CreateUploadConfigTemplate(uploadConfig, unmanagedArchiveConfigTemplate)
                )
                .ToList();
        }

        releaseTemplate.ImageUploadConfigTemplates = release
            .ImageUploadConfigs.Select(CreateImageUploadConfigTemplate)
            .ToList();

        writeRepository.Add(releaseTemplate);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return releaseTemplate.Id;
    }

    private static Dictionary<
        int,
        ArchiveConfigTemplate
    > CreateArchiveConfigTemplatesByArchiveConfigId(Release release)
    {
        return release.ArchiveConfigs.ToDictionary(
            archiveConfig => archiveConfig.Id,
            archiveConfig => CreateArchiveConfigTemplate(archiveConfig, release.Name)
        );
    }

    private static ArchiveConfigTemplate CreateArchiveConfigTemplate(
        ArchiveConfig archiveConfig,
        string releaseName
    )
    {
        return new ArchiveConfigTemplate
        {
            Name = archiveConfig.Name,
            ArchiveFilesBasePath = archiveConfig.ArchiveFilesBasePath,
            ArchiverName = archiveConfig.ArchiverName,
            ArchivePassword = archiveConfig.ArchivePassword,
            ArchiveFileSizeMb = archiveConfig.ArchiveFileSizeMb,
            UseReleaseNameAsArchiveName = archiveConfig.ArchiveNamePrefix == releaseName,
            PackReleaseFolderAsRootFolder = archiveConfig.PackReleaseFolderAsRootFolder,
            CreateNonceFile = archiveConfig.CreateNonceFile,
            AdditionalArchiveContents = archiveConfig.AdditionalArchiveContents.ToList(),
        };
    }

    private static UploadConfigTemplate CreateUploadConfigTemplate(
        UploadConfig uploadConfig,
        ArchiveConfigTemplate? archiveConfigTemplate
    )
    {
        return new UploadConfigTemplate
        {
            UploadSource = uploadConfig.UploadSource,
            ArchiveConfigTemplate = archiveConfigTemplate,
            HosterRegistrationId = uploadConfig.HosterRegistrationId,
            HosterAccountPoolId = uploadConfig.HosterAccountPoolId,
            Name = string.Equals(
                uploadConfig.Name,
                uploadConfig.GetUploadTargetName(),
                StringComparison.Ordinal
            )
                ? null
                : uploadConfig.Name,
            PremiumOnlyDownload = uploadConfig.PremiumOnlyDownload,
            CollectionUploadSlotKey = uploadConfig.CollectionUploadSlot?.Key,
            CollectionUploadSlotName = uploadConfig.CollectionUploadSlot?.Name,
            CollectionUploadSlotIsRequired = uploadConfig.CollectionUploadSlot?.IsRequired ?? false,
            CollectionUploadSlotPasswordPolicy =
                uploadConfig.CollectionUploadSlot?.PasswordPolicy
                ?? CollectionUploadSlotPasswordPolicy.Ignore,
            CollectionUploadSlotExpectedArchivePassword = uploadConfig
                .CollectionUploadSlot
                ?.ExpectedArchivePassword,
            LinkCrypterTemplates = uploadConfig
                .LinkCrypters.Select(CreateLinkCrypterTemplate)
                .ToList(),
        };
    }

    private static UploadConfigLinkCrypterTemplate CreateLinkCrypterTemplate(
        UploadConfigLinkCrypter linkCrypter
    )
    {
        return new UploadConfigLinkCrypterTemplate
        {
            LinkCrypterRegistrationId = linkCrypter.LinkCrypterRegistrationId,
            ContainerScope = linkCrypter.ContainerScope,
            Password = linkCrypter.Password.TrimOrNullWhenWhitespace(),
            EnableCaptcha = linkCrypter.EnableCaptcha,
            EnableContainerDownload = linkCrypter.EnableContainerDownload,
            EnableClickAndLoad = linkCrypter.EnableClickAndLoad,
        };
    }

    private static ImageUploadConfigTemplate CreateImageUploadConfigTemplate(
        ImageUploadConfig imageUploadConfig
    )
    {
        return new ImageUploadConfigTemplate
        {
            ImageHosterRegistrationId = imageUploadConfig.ImageHosterRegistrationId,
            Name = string.Equals(
                imageUploadConfig.Name,
                imageUploadConfig.ImageHosterRegistration.Name,
                StringComparison.Ordinal
            )
                ? null
                : imageUploadConfig.Name,
        };
    }
}

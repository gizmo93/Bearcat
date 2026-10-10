using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.CollectionAssignment;
using Bearcat.Domain.Shared.UnmanagedReleases;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.ReleaseInfoResolution;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.Creation;

public class ReleaseFromFolderCreationService(
    ReleaseInfoResolutionService releaseInfoResolutionService,
    MediaMetadataService mediaMetadataService,
    UnmanagedReleaseArchiveInitializationService unmanagedReleaseArchiveInitializationService,
    IReleaseCollectionAssigner releaseCollectionAssigner
)
{
    public async Task<Release> CreateAsync(
        ReleaseTemplate releaseTemplate,
        string folderPath,
        string? name,
        string? primaryLanguageCode,
        DateTime localNow,
        CancellationToken cancellationToken
    )
    {
        var releaseData = CreateReleaseFromTemplate(
            releaseTemplate: releaseTemplate,
            releaseFolderPath: folderPath,
            name: name,
            localNow: localNow
        );
        var release = releaseData.Release;

        release.CreatedAt = localNow;
        release.PrimaryLanguageCode = primaryLanguageCode;

        await releaseCollectionAssigner.AssignFromTemplateAsync(
            release: release,
            releaseTemplate: releaseTemplate,
            uploadConfigMatches: releaseData.UploadConfigMatches,
            cancellationToken: cancellationToken
        );

        await releaseInfoResolutionService.TryResolveAsync(release, cancellationToken);

        await mediaMetadataService.TryExtractAsync(release, cancellationToken);

        return release;
    }

    private ReleaseFromTemplateData CreateReleaseFromTemplate(
        ReleaseTemplate releaseTemplate,
        string releaseFolderPath,
        string? name,
        DateTime localNow
    )
    {
        var releaseType = releaseTemplate.ReleaseType;
        var releaseName = CleanOptional(name) ?? FolderPathHelper.GetFolderName(releaseFolderPath);
        var isUnmanaged = releaseType is ReleaseType.Unmanaged;

        var release = new Release
        {
            Name = releaseName,
            ReleaseFolderPath = isUnmanaged ? null : releaseFolderPath,
            ReleaseType = releaseType,
            ReleaseContentType = releaseTemplate.ReleaseContentType,
            ReleaseGroupId = releaseTemplate.ReleaseGroupId,
            ArchiveConfigs = [],
            UploadConfigs = [],
            ImageUploadConfigs = [],
        };

        var archiveConfigsByTemplateId = new Dictionary<int, ArchiveConfig>();
        ArchiveConfig? unmanagedArchiveConfig = null;

        if (releaseType is ReleaseType.Managed)
        {
            archiveConfigsByTemplateId = releaseTemplate
                .ArchiveConfigTemplates.Select(template => new
                {
                    template.Id,
                    Config = new ArchiveConfig
                    {
                        Name = template.Name,
                        ArchiveFilesBasePath = template.ArchiveFilesBasePath,
                        ArchiverName = template.ArchiverName,
                        ArchivePassword = template.ArchivePassword,
                        ArchiveFileSizeMb = template.ArchiveFileSizeMb,
                        PackReleaseFolderAsRootFolder = template.PackReleaseFolderAsRootFolder,
                        CreateNonceFile = template.CreateNonceFile,
                        ArchiveNamePrefix = template.UseReleaseNameAsArchiveName
                            ? releaseName
                            : null,
                        Archives = [],
                        UploadConfigs = [],
                        AdditionalArchiveContents = template.AdditionalArchiveContents.ToList(),
                    },
                })
                .ToDictionary(item => item.Id, item => item.Config);
            release.ArchiveConfigs = archiveConfigsByTemplateId.Values.ToList();
        }
        else
        {
            unmanagedArchiveConfig =
                unmanagedReleaseArchiveInitializationService.CreateArchiveConfig(
                    release: release,
                    archiveFolderPath: releaseFolderPath,
                    createdAt: localNow
                );
            release.ArchiveConfigs.Add(unmanagedArchiveConfig);
        }

        var uploadConfigTemplates = releaseTemplate
            .UploadConfigTemplates.OrderBy(template => template.Id)
            .ToList();

        var uploadConfigMatches = new List<ReleaseUploadConfigMatch>(uploadConfigTemplates.Count);
        release.UploadConfigs = [];

        foreach (var template in uploadConfigTemplates)
        {
            var uploadConfig = new UploadConfig
            {
                Name = CleanOptional(template.Name) ?? template.HosterRegistration.Name,
                HosterRegistrationId = template.HosterRegistrationId,
                PremiumOnlyDownload = template.PremiumOnlyDownload,
                ArchiveConfig =
                    releaseType is ReleaseType.Managed
                        ? archiveConfigsByTemplateId[template.ArchiveConfigTemplateId]
                        : unmanagedArchiveConfig!,
                Uploads = [],
                LinkCrypters = template
                    .LinkCrypterTemplates.Select(linkCrypter => new UploadConfigLinkCrypter
                    {
                        LinkCrypterRegistrationId = linkCrypter.LinkCrypterRegistrationId,
                        ContainerScope = linkCrypter.ContainerScope,
                        Password = CleanOptional(linkCrypter.Password),
                        EnableCaptcha = linkCrypter.EnableCaptcha,
                        EnableContainerDownload = linkCrypter.EnableContainerDownload,
                        EnableClickAndLoad = linkCrypter.EnableClickAndLoad,
                        LinkCrypterContainers = [],
                    })
                    .ToList(),
            };

            release.UploadConfigs.Add(uploadConfig);
            uploadConfigMatches.Add(new ReleaseUploadConfigMatch(template, uploadConfig));
        }

        release.ImageUploadConfigs = releaseTemplate
            .ImageUploadConfigTemplates.Select(template => new ImageUploadConfig
            {
                Name = CleanOptional(template.Name) ?? template.ImageHosterRegistration.Name,
                ImageHosterRegistrationId = template.ImageHosterRegistrationId,
                ImageUploads = [],
            })
            .ToList();

        return new ReleaseFromTemplateData(release, uploadConfigMatches);
    }

    private static string? CleanOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

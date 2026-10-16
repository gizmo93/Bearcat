using Bearcat.Abstractions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.FirstStartSetup.Exceptions;
using Bearcat.Domain.UseCases.ManageReleaseGroups;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.ArchiveConfigTemplates;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.UploadConfigTemplates;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.FirstStartSetup.ReleaseTemplates;

public class FirstStartReleaseTemplateService(
    IReleaseTemplateWriteRepository releaseTemplateWriteRepository,
    IArchiverFactory archiverFactory,
    IFileSystemService fileSystemService
)
{
    private const ReleaseContentType DefaultReleaseContentType = ReleaseContentType.Movie;

    public async Task<int> CreateReleaseGroupAndTemplateAsync(
        FirstStartReleaseTemplateInput input,
        CancellationToken cancellationToken = default
    )
    {
        var releaseGroup = ReleaseGroupService.CreateValidatedReleaseGroup(
            input.ReleaseGroupName,
            input.EnableAutomaticReuploads,
            input.NumberOfHoursUntilReupload,
            qualityProfileId: null
        );

        if (!fileSystemService.DirectoryExists(input.ArchiveOutputFolderPath))
        {
            throw new ArchiveOutputFolderNotFoundException(input.ArchiveOutputFolderPath);
        }

        var archiveConfigTemplate = CreateArchiveConfigTemplate(input);

        var releaseTemplate = new ReleaseTemplate
        {
            Name = input.ReleaseTemplateName,
            ReleaseType = ReleaseType.Managed,
            ReleaseContentType = DefaultReleaseContentType,
            ReleaseGroup = releaseGroup,
            ArchiveConfigTemplates = [archiveConfigTemplate],
            UploadConfigTemplates = input
                .HosterRegistrationIds.Select(hosterRegistrationId =>
                    CreateUploadConfigTemplate(hosterRegistrationId, archiveConfigTemplate)
                )
                .ToList(),
        };

        releaseTemplateWriteRepository.Add(releaseTemplate);
        await releaseTemplateWriteRepository.SaveChangesAsync(cancellationToken);

        return releaseTemplate.Id;
    }

    private ArchiveConfigTemplate CreateArchiveConfigTemplate(FirstStartReleaseTemplateInput input)
    {
        var archiverDisplayName = archiverFactory
            .GetArchivers()
            .Single(archiver => archiver.ClassName == input.ArchiverName)
            .Name;

        var archiveConfigTemplate = new ArchiveConfigTemplate();
        ArchiveConfigTemplateService.ApplySettings(
            archiveConfigTemplate,
            new ArchiveConfigTemplateSettings(
                Name: archiverDisplayName,
                ArchiveFilesBasePath: input.ArchiveOutputFolderPath,
                ArchiverName: input.ArchiverName,
                ArchivePassword: input.ArchivePassword,
                ArchiveFileSizeMb: input.ArchiveFileSizeMb,
                UseReleaseNameAsArchiveName: true,
                PackReleaseFolderAsRootFolder: archiveConfigTemplate.PackReleaseFolderAsRootFolder,
                CreateNonceFile: archiveConfigTemplate.CreateNonceFile,
                AdditionalArchiveContentIds: []
            ),
            additionalArchiveContents: []
        );

        return archiveConfigTemplate;
    }

    private static UploadConfigTemplate CreateUploadConfigTemplate(
        int hosterRegistrationId,
        ArchiveConfigTemplate archiveConfigTemplate
    )
    {
        var uploadConfigTemplate = new UploadConfigTemplate();
        UploadConfigTemplateService.ApplySettings(
            uploadConfigTemplate,
            new UploadConfigTemplateSettings(
                Name: null,
                UploadTarget: UploadTarget.ForHosterRegistration(hosterRegistrationId),
                ArchiveConfigTemplateId: archiveConfigTemplate.Id,
                PremiumOnlyDownload: false
            ),
            unmanagedArchiveConfigTemplate: null
        );
        uploadConfigTemplate.ArchiveConfigTemplate = archiveConfigTemplate;

        return uploadConfigTemplate;
    }
}

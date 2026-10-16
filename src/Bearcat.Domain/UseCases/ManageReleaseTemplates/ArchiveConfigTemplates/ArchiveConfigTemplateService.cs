using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.Assignment;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.ArchiveConfigTemplates;

public class ArchiveConfigTemplateService(IReleaseTemplateWriteRepository writeRepository)
{
    public async Task<ArchiveConfigTemplateSaveResult> CreateAsync(
        int releaseTemplateId,
        ArchiveConfigTemplateSettings settings,
        CancellationToken cancellationToken = default
    )
    {
        var releaseTemplate = await writeRepository.GetByIdWithChildrenAsync(
            releaseTemplateId,
            cancellationToken
        );

        ThrowIfReleaseTemplateIsNotManaged(releaseTemplate);

        var additionalArchiveContentsWithDuplicatedEntryNames =
            await GetAdditionalArchiveContentsWithDuplicatedEntryNamesAsync(
                settings.AdditionalArchiveContentIds,
                cancellationToken
            );

        if (additionalArchiveContentsWithDuplicatedEntryNames.DuplicatedEntryNames.Count > 0)
        {
            return ArchiveConfigTemplateSaveResult.Invalid(
                additionalArchiveContentsWithDuplicatedEntryNames.DuplicatedEntryNames
            );
        }

        var archiveConfigTemplate = new ArchiveConfigTemplate();
        ApplySettings(
            archiveConfigTemplate,
            settings,
            additionalArchiveContentsWithDuplicatedEntryNames.AdditionalArchiveContents
        );

        releaseTemplate.ArchiveConfigTemplates.Add(archiveConfigTemplate);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return ArchiveConfigTemplateSaveResult.Saved(archiveConfigTemplate.Id);
    }

    public async Task<ArchiveConfigTemplateSaveResult> UpdateAsync(
        int archiveConfigTemplateId,
        ArchiveConfigTemplateSettings settings,
        CancellationToken cancellationToken = default
    )
    {
        var archiveConfigTemplate = await writeRepository.GetArchiveConfigTemplateAsync(
            archiveConfigTemplateId,
            cancellationToken
        );

        ThrowIfReleaseTemplateIsNotManaged(archiveConfigTemplate.ReleaseTemplate);

        var additionalArchiveContentsWithDuplicatedEntryNames =
            await GetAdditionalArchiveContentsWithDuplicatedEntryNamesAsync(
                settings.AdditionalArchiveContentIds,
                cancellationToken
            );

        if (additionalArchiveContentsWithDuplicatedEntryNames.DuplicatedEntryNames.Count > 0)
        {
            return ArchiveConfigTemplateSaveResult.Invalid(
                additionalArchiveContentsWithDuplicatedEntryNames.DuplicatedEntryNames
            );
        }

        ApplySettings(
            archiveConfigTemplate,
            settings,
            additionalArchiveContentsWithDuplicatedEntryNames.AdditionalArchiveContents
        );

        await writeRepository.SaveChangesAsync(cancellationToken);

        return ArchiveConfigTemplateSaveResult.Saved(archiveConfigTemplate.Id);
    }

    public async Task DeleteAsync(
        int archiveConfigTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        var archiveConfigTemplate = await writeRepository.GetArchiveConfigTemplateAsync(
            archiveConfigTemplateId,
            cancellationToken
        );

        ThrowIfReleaseTemplateIsNotManaged(archiveConfigTemplate.ReleaseTemplate);

        writeRepository.Remove(archiveConfigTemplate);
        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task<AdditionalArchiveContentsWithDuplicatedEntryNames> GetAdditionalArchiveContentsWithDuplicatedEntryNamesAsync(
        IReadOnlyList<int> additionalArchiveContentIds,
        CancellationToken cancellationToken
    )
    {
        var additionalArchiveContents = await writeRepository.GetAdditionalArchiveContentsAsync(
            additionalArchiveContentIds,
            cancellationToken
        );

        var duplicatedEntryNames =
            AdditionalArchiveContentAssignmentValidation.FindDuplicatedEntryNames(
                additionalArchiveContents
            );

        return new AdditionalArchiveContentsWithDuplicatedEntryNames(
            additionalArchiveContents,
            duplicatedEntryNames
        );
    }

    private static void ApplySettings(
        ArchiveConfigTemplate archiveConfigTemplate,
        ArchiveConfigTemplateSettings settings,
        IReadOnlyList<AdditionalArchiveContent> additionalArchiveContents
    )
    {
        archiveConfigTemplate.Name = settings.Name;
        archiveConfigTemplate.ArchiveFilesBasePath = settings.ArchiveFilesBasePath;
        archiveConfigTemplate.ArchiverName = settings.ArchiverName;
        archiveConfigTemplate.ArchivePassword = settings.ArchivePassword.TrimOrNullWhenWhitespace();
        archiveConfigTemplate.ArchiveFileSizeMb = settings.ArchiveFileSizeMb;
        archiveConfigTemplate.UseReleaseNameAsArchiveName = settings.UseReleaseNameAsArchiveName;
        archiveConfigTemplate.PackReleaseFolderAsRootFolder =
            settings.PackReleaseFolderAsRootFolder;
        archiveConfigTemplate.CreateNonceFile = settings.CreateNonceFile;
        archiveConfigTemplate.AdditionalArchiveContents.Clear();
        archiveConfigTemplate.AdditionalArchiveContents.AddRange(additionalArchiveContents);
    }

    private static void ThrowIfReleaseTemplateIsNotManaged(ReleaseTemplate releaseTemplate)
    {
        if (releaseTemplate.ReleaseType is ReleaseType.Managed)
        {
            return;
        }

        throw new InvalidOperationException(
            "Archive config templates for unmanaged release templates cannot be changed."
        );
    }

    private sealed record AdditionalArchiveContentsWithDuplicatedEntryNames(
        IReadOnlyList<AdditionalArchiveContent> AdditionalArchiveContents,
        IReadOnlyList<DuplicatedEntryName> DuplicatedEntryNames
    );
}

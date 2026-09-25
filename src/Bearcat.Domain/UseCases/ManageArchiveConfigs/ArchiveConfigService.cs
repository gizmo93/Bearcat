using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.UnmanagedReleases;
using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.Assignment;
using Bearcat.Domain.UseCases.ManageArchiveConfigs.Validation;
using Bearcat.Domain.ValueObjects;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageArchiveConfigs;

public class ArchiveConfigService(
    IArchiveConfigWriteRepository writeRepository,
    IArchiverFactory archiverFactory,
    TimeProvider timeProvider
)
{
    public async Task<ArchiveConfigSaveResult> CreateAsync(
        int releaseId,
        string archiveFilesBasePath,
        string archiverName,
        string archiveNamePrefix,
        string? archivePassword,
        string name,
        int? archiveFileSizeMb,
        IReadOnlyList<int> additionalArchiveContentIds
    )
    {
        var additionalArchiveContents = await writeRepository.GetAdditionalArchiveContentsAsync(
            additionalArchiveContentIds
        );

        var duplicatedEntryNames =
            AdditionalArchiveContentAssignmentValidation.FindDuplicatedEntryNames(
                additionalArchiveContents
            );

        if (duplicatedEntryNames.Count > 0)
        {
            return ArchiveConfigSaveResult.Invalid(duplicatedEntryNames);
        }

        var archiveConfig = new ArchiveConfig
        {
            ReleaseId = releaseId,
            Name = name,
            ArchiveFilesBasePath = archiveFilesBasePath,
            ArchiverName = archiverName,
            ArchiveNamePrefix = archiveNamePrefix,
            ArchivePassword = archivePassword,
            ArchiveFileSizeMb = archiveFileSizeMb ?? 0,
            AdditionalArchiveContents = additionalArchiveContents.ToList(),
        };

        writeRepository.Add(archiveConfig);
        await writeRepository.SaveChangesAsync();

        return ArchiveConfigSaveResult.Saved(archiveConfig.Id);
    }

    public async Task DeleteAsync(int archiveConfigId)
    {
        var archiveConfig = await writeRepository.GetByIdAsync(archiveConfigId);
        if (archiveConfig == null)
        {
            throw new InvalidOperationException(
                $"ArchiveConfig with ID {archiveConfigId} not found"
            );
        }

        EnsureManagedRelease(archiveConfig);

        writeRepository.Remove(archiveConfig);
        await writeRepository.SaveChangesAsync();
    }

    public async Task<ArchiveConfigSaveResult> UpdateAsync(
        int archiveConfigId,
        string archiveFilesBasePath,
        string archiveNamePrefix,
        string? archivePassword,
        string name,
        int? archiveFileSizeMb,
        IReadOnlyList<int> additionalArchiveContentIds
    )
    {
        var archiveConfig = await writeRepository.GetByIdAsync(archiveConfigId);
        if (archiveConfig == null)
        {
            throw new InvalidOperationException(
                $"ArchiveConfig with ID {archiveConfigId} not found"
            );
        }

        EnsureManagedRelease(archiveConfig);

        var additionalArchiveContents = await writeRepository.GetAdditionalArchiveContentsAsync(
            additionalArchiveContentIds
        );

        var entryNameCollisions =
            AdditionalArchiveContentAssignmentValidation.FindDuplicatedEntryNames(
                additionalArchiveContents
            );

        if (entryNameCollisions.Count > 0)
        {
            return ArchiveConfigSaveResult.Invalid(entryNameCollisions);
        }

        archiveConfig.ArchiveFilesBasePath = archiveFilesBasePath;
        archiveConfig.ArchiveNamePrefix = archiveNamePrefix;
        archiveConfig.ArchivePassword = archivePassword;
        archiveConfig.ArchiveFileSizeMb = archiveFileSizeMb ?? 0;
        archiveConfig.Name = name;
        archiveConfig.AdditionalArchiveContents.Clear();
        archiveConfig.AdditionalArchiveContents.AddRange(additionalArchiveContents);

        await writeRepository.SaveChangesAsync();

        return ArchiveConfigSaveResult.Saved(archiveConfig.Id);
    }

    public async Task<ArchiveFolderChangeResult> SetArchiveFolderAsync(
        int archiveConfigId,
        string archiveFolderPath,
        bool confirmContentChange,
        CancellationToken cancellationToken = default
    )
    {
        var archiveConfig = await writeRepository.GetByIdAsync(
            id: archiveConfigId,
            cancellationToken: cancellationToken
        );

        if (archiveConfig == null)
        {
            throw new InvalidOperationException(
                $"ArchiveConfig with ID {archiveConfigId} not found"
            );
        }

        EnsureUnmanagedRelease(archiveConfig);

        var result = UnmanagedReleaseArchiveInitializer.ApplyArchiveFolder(
            archiveConfig: archiveConfig,
            archiveFolderPath: archiveFolderPath,
            archiver: archiverFactory.GetByName(archiveConfig.ArchiverName),
            createdAt: timeProvider.GetLocalNow(),
            confirmContentChange: confirmContentChange
        );

        if (result is not ArchiveFolderChangeResult.ConfirmationRequired)
        {
            await writeRepository.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    private static void EnsureManagedRelease(ArchiveConfig archiveConfig)
    {
        if (archiveConfig.Release.ReleaseType is ReleaseType.Managed)
        {
            return;
        }

        throw new InvalidOperationException(
            "Archive configs for unmanaged releases cannot be changed."
        );
    }

    private static void EnsureUnmanagedRelease(ArchiveConfig archiveConfig)
    {
        if (archiveConfig.Release.ReleaseType is ReleaseType.Unmanaged)
        {
            return;
        }

        throw new InvalidOperationException(
            "Archives can only be refreshed for unmanaged releases."
        );
    }
}

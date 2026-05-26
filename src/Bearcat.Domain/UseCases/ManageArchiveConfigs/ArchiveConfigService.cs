using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.ValueObjects;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageArchiveConfigs;

public class ArchiveConfigService(
    IArchiveConfigWriteRepository writeRepository,
    IArchiverFactory archiverFactory,
    TimeProvider timeProvider
)
{
    public async Task<int> CreateAsync(
        int releaseId,
        string archiveFilesBasePath,
        string archiverName,
        string archiveNamePrefix,
        string? archivePassword,
        string name,
        int? archiveFileSizeMb
    )
    {
        var archiveConfig = new ArchiveConfig
        {
            ReleaseId = releaseId,
            Name = name,
            ArchiveFilesBasePath = archiveFilesBasePath,
            ArchiverName = archiverName,
            ArchiveNamePrefix = archiveNamePrefix,
            ArchivePassword = archivePassword,
            ArchiveFileSizeMb = archiveFileSizeMb ?? 0,
        };

        writeRepository.Add(archiveConfig);
        await writeRepository.SaveChangesAsync();

        return archiveConfig.Id;
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

    public async Task UpdateAsync(
        int archiveConfigId,
        string archiveFilesBasePath,
        string archiveNamePrefix,
        string? archivePassword,
        string name,
        int? archiveFileSizeMb
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

        archiveConfig.ArchiveFilesBasePath = archiveFilesBasePath;
        archiveConfig.ArchiveNamePrefix = archiveNamePrefix;
        archiveConfig.ArchivePassword = archivePassword;
        archiveConfig.ArchiveFileSizeMb = archiveFileSizeMb ?? 0;
        archiveConfig.Name = name;

        await writeRepository.SaveChangesAsync();
    }

    public async Task RefreshUnmanagedArchiveAsync(
        int archiveConfigId,
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

        UnmanagedReleaseArchiveInitializer.RefreshArchiveConfig(
            archiveConfig: archiveConfig,
            archivers: archiverFactory.GetArchivers(),
            createdAt: timeProvider.GetLocalNow()
        );

        await writeRepository.SaveChangesAsync(cancellationToken);
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

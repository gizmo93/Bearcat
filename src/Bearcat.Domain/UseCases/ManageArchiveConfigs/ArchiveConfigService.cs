using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageArchiveConfigs;

public class ArchiveConfigService(IArchiveConfigWriteRepository writeRepository)
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

        archiveConfig.ArchiveFilesBasePath = archiveFilesBasePath;
        archiveConfig.ArchiveNamePrefix = archiveNamePrefix;
        archiveConfig.ArchivePassword = archivePassword;
        archiveConfig.ArchiveFileSizeMb = archiveFileSizeMb ?? 0;
        archiveConfig.Name = name;

        await writeRepository.SaveChangesAsync();
    }
}

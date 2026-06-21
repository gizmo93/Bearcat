using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Repositories;

namespace Bearcat.Domain.UseCases.ManageUploadConfigs;

public class UploadConfigService(IUploadConfigWriteRepository writeRepository)
{
    public async Task<int> CreateAsync(
        int releaseId,
        string name,
        int hosterRegistrationId,
        int archiveConfigId,
        bool premiumOnlyDownload,
        CancellationToken cancellationToken = default
    )
    {
        var uploadConfig = new UploadConfig
        {
            ReleaseId = releaseId,
            Name = name,
            HosterRegistrationId = hosterRegistrationId,
            ArchiveConfigId = archiveConfigId,
            PremiumOnlyDownload = premiumOnlyDownload,
        };

        writeRepository.Add(uploadConfig);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return uploadConfig.Id;
    }

    public async Task UpdateAsync(
        int uploadConfigId,
        string name,
        int hosterRegistrationId,
        int archiveConfigId,
        bool premiumOnlyDownload,
        CancellationToken cancellationToken = default
    )
    {
        var uploadConfig = await writeRepository.GetByIdAsync(uploadConfigId, cancellationToken);

        uploadConfig.Name = name;
        uploadConfig.HosterRegistrationId = hosterRegistrationId;
        uploadConfig.ArchiveConfigId = archiveConfigId;
        uploadConfig.PremiumOnlyDownload = premiumOnlyDownload;

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int uploadConfigId, CancellationToken cancellationToken = default)
    {
        var uploadConfig = await writeRepository.GetByIdAsync(uploadConfigId, cancellationToken);

        writeRepository.Remove(uploadConfig);
        await writeRepository.SaveChangesAsync(cancellationToken);
    }
}

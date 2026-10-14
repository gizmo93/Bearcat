using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Repositories;

namespace Bearcat.Domain.UseCases.ManageUploadConfigs;

public class UploadConfigService(IUploadConfigWriteRepository writeRepository)
{
    public async Task<int> CreateAsync(
        int releaseId,
        string name,
        UploadTarget uploadTarget,
        int archiveConfigId,
        bool premiumOnlyDownload,
        CancellationToken cancellationToken = default
    )
    {
        var uploadConfig = new UploadConfig
        {
            ReleaseId = releaseId,
            Name = name,
            HosterRegistrationId = uploadTarget.HosterRegistrationId,
            HosterAccountPoolId = uploadTarget.HosterAccountPoolId,
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
        UploadTarget uploadTarget,
        int archiveConfigId,
        bool premiumOnlyDownload,
        CancellationToken cancellationToken = default
    )
    {
        var uploadConfig = await writeRepository.GetByIdAsync(uploadConfigId, cancellationToken);

        if (
            uploadConfig.HosterRegistrationId != uploadTarget.HosterRegistrationId
            || uploadConfig.HosterAccountPoolId != uploadTarget.HosterAccountPoolId
        )
        {
            var uploadsNotStartedOnHoster =
                await writeRepository.GetWaitingOrPendingUploadsWithoutUploadedFilesAsync(
                    uploadConfigId,
                    cancellationToken
                );

            foreach (var upload in uploadsNotStartedOnHoster)
            {
                upload.HosterRegistrationId = uploadTarget.HosterRegistrationId;
            }
        }

        uploadConfig.Name = name;
        uploadConfig.HosterRegistrationId = uploadTarget.HosterRegistrationId;
        uploadConfig.HosterAccountPoolId = uploadTarget.HosterAccountPoolId;
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

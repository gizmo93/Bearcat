using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Repositories;
using Bearcat.Domain.UseCases.ManageUploadConfigs.UploadSources;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageUploadConfigs;

public class UploadConfigService(
    IUploadConfigWriteRepository writeRepository,
    UploadSourceValidator uploadSourceValidator
)
{
    public async Task<int> CreateAsync(
        int releaseId,
        string name,
        UploadTarget uploadTarget,
        UploadSource uploadSource,
        int? archiveConfigId,
        bool premiumOnlyDownload,
        CancellationToken cancellationToken = default
    )
    {
        var release = await writeRepository.GetReleaseAsync(releaseId, cancellationToken);

        await uploadSourceValidator.ThrowIfInvalidAsync(
            uploadSource: uploadSource,
            releaseType: release.ReleaseType,
            releaseContentType: release.ReleaseContentType,
            uploadTarget: uploadTarget,
            cancellationToken: cancellationToken
        );

        var uploadConfig = new UploadConfig
        {
            UploadSource = uploadSource,
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
        int? archiveConfigId,
        bool premiumOnlyDownload,
        CancellationToken cancellationToken = default
    )
    {
        var uploadConfig = await writeRepository.GetByIdAsync(uploadConfigId, cancellationToken);

        await uploadSourceValidator.ThrowIfUploadTargetDoesNotMatchAsync(
            uploadSource: uploadConfig.UploadSource,
            uploadTarget: uploadTarget,
            cancellationToken: cancellationToken
        );

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

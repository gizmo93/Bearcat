using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;

namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.UploadConfigTemplates;

public class UploadConfigTemplateService(IReleaseTemplateWriteRepository writeRepository)
{
    public async Task<int> CreateAsync(
        int releaseTemplateId,
        UploadConfigTemplateSettings settings,
        CancellationToken cancellationToken = default
    )
    {
        var releaseTemplate = await writeRepository.GetByIdWithChildrenAsync(
            releaseTemplateId,
            cancellationToken
        );

        var unmanagedArchiveConfigTemplate =
            UnmanagedArchiveConfigTemplates.GetForUnmanagedReleaseTemplateOrNull(releaseTemplate);

        var uploadConfigTemplate = new UploadConfigTemplate();
        ApplySettings(uploadConfigTemplate, settings, unmanagedArchiveConfigTemplate);

        releaseTemplate.UploadConfigTemplates.Add(uploadConfigTemplate);

        await writeRepository.SaveChangesAsync(cancellationToken);

        return uploadConfigTemplate.Id;
    }

    public async Task UpdateAsync(
        int uploadConfigTemplateId,
        UploadConfigTemplateSettings settings,
        CancellationToken cancellationToken = default
    )
    {
        var uploadConfigTemplate = await writeRepository.GetUploadConfigTemplateAsync(
            uploadConfigTemplateId,
            cancellationToken
        );

        var unmanagedArchiveConfigTemplate =
            UnmanagedArchiveConfigTemplates.GetForUnmanagedReleaseTemplateOrNull(
                uploadConfigTemplate.ReleaseTemplate
            );

        ApplySettings(uploadConfigTemplate, settings, unmanagedArchiveConfigTemplate);

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        int uploadConfigTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        var uploadConfigTemplate = await writeRepository.GetUploadConfigTemplateAsync(
            uploadConfigTemplateId,
            cancellationToken
        );

        writeRepository.Remove(uploadConfigTemplate);
        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    internal static void ApplySettings(
        UploadConfigTemplate uploadConfigTemplate,
        UploadConfigTemplateSettings settings,
        ArchiveConfigTemplate? unmanagedArchiveConfigTemplate
    )
    {
        uploadConfigTemplate.Name = settings.Name.TrimOrNullWhenWhitespace();
        uploadConfigTemplate.HosterRegistrationId = settings.UploadTarget.HosterRegistrationId;
        uploadConfigTemplate.HosterAccountPoolId = settings.UploadTarget.HosterAccountPoolId;
        uploadConfigTemplate.ArchiveConfigTemplateId =
            unmanagedArchiveConfigTemplate?.Id ?? settings.ArchiveConfigTemplateId;
        uploadConfigTemplate.PremiumOnlyDownload = settings.PremiumOnlyDownload;
        uploadConfigTemplate.CollectionUploadSlotKey =
            settings.CollectionUploadSlotKey.TrimOrNullWhenWhitespace();
        uploadConfigTemplate.CollectionUploadSlotName =
            settings.CollectionUploadSlotName.TrimOrNullWhenWhitespace();
        uploadConfigTemplate.CollectionUploadSlotIsRequired =
            settings.CollectionUploadSlotIsRequired;
        uploadConfigTemplate.CollectionUploadSlotPasswordPolicy =
            settings.CollectionUploadSlotPasswordPolicy;
        uploadConfigTemplate.CollectionUploadSlotExpectedArchivePassword =
            settings.CollectionUploadSlotExpectedArchivePassword.TrimOrNullWhenWhitespace();

        if (unmanagedArchiveConfigTemplate is not null)
        {
            uploadConfigTemplate.ArchiveConfigTemplate = unmanagedArchiveConfigTemplate;
        }
    }
}

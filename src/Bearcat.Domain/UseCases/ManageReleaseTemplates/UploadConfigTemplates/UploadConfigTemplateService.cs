using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;
using Bearcat.Domain.UseCases.ManageUploadConfigs.UploadSources;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.UploadConfigTemplates;

public class UploadConfigTemplateService(
    IReleaseTemplateWriteRepository writeRepository,
    UploadSourceValidator uploadSourceValidator
)
{
    public async Task<int> CreateAsync(
        int releaseTemplateId,
        UploadSource uploadSource,
        UploadConfigTemplateSettings settings,
        CancellationToken cancellationToken = default
    )
    {
        var releaseTemplate = await writeRepository.GetByIdWithChildrenAsync(
            releaseTemplateId,
            cancellationToken
        );

        await uploadSourceValidator.ThrowIfInvalidAsync(
            uploadSource,
            releaseTemplate.ReleaseType,
            releaseTemplate.ReleaseContentType,
            settings.UploadTarget,
            cancellationToken
        );
        ThrowIfVideoTemplateUsesCollectionUploadSlot(uploadSource, settings);

        var unmanagedArchiveConfigTemplate =
            UnmanagedArchiveConfigTemplates.GetForUnmanagedReleaseTemplateOrNull(releaseTemplate);

        var uploadConfigTemplate = new UploadConfigTemplate { UploadSource = uploadSource };
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

        await uploadSourceValidator.ThrowIfUploadTargetDoesNotMatchAsync(
            uploadConfigTemplate.UploadSource,
            settings.UploadTarget,
            cancellationToken
        );
        ThrowIfVideoTemplateUsesCollectionUploadSlot(uploadConfigTemplate.UploadSource, settings);

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

    private static void ThrowIfVideoTemplateUsesCollectionUploadSlot(
        UploadSource uploadSource,
        UploadConfigTemplateSettings settings
    )
    {
        if (
            uploadSource == UploadSource.VideoFiles
            && settings.CollectionUploadSlotKey.TrimOrNullWhenWhitespace() is not null
        )
        {
            throw new InvalidUploadSourceException(
                "Video upload configuration templates cannot use a collection upload slot."
            );
        }
    }

    private static void ApplySettings(
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

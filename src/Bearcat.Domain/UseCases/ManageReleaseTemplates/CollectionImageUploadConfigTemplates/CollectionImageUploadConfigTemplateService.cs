using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.CollectionImageUploadConfigTemplates;

public class CollectionImageUploadConfigTemplateService(
    IReleaseTemplateWriteRepository writeRepository
)
{
    public async Task<int> CreateAsync(
        int releaseTemplateId,
        string? name,
        int imageHosterRegistrationId,
        CancellationToken cancellationToken = default
    )
    {
        var releaseTemplate = await writeRepository.GetByIdWithChildrenAsync(
            releaseTemplateId,
            cancellationToken
        );

        if (
            releaseTemplate.ReleaseCollectionDetectionMode
            == ReleaseCollectionDetectionMode.Disabled
        )
        {
            throw new InvalidOperationException(
                "Collection image upload configurations require release collection detection to be enabled."
            );
        }

        var collectionImageUploadConfigTemplate = new CollectionImageUploadConfigTemplate
        {
            Name = name.TrimOrNullWhenWhitespace(),
            ImageHosterRegistrationId = imageHosterRegistrationId,
        };

        releaseTemplate.CollectionImageUploadConfigTemplates.Add(
            collectionImageUploadConfigTemplate
        );

        await writeRepository.SaveChangesAsync(cancellationToken);

        return collectionImageUploadConfigTemplate.Id;
    }

    public async Task UpdateAsync(
        int collectionImageUploadConfigTemplateId,
        string? name,
        int imageHosterRegistrationId,
        CancellationToken cancellationToken = default
    )
    {
        var collectionImageUploadConfigTemplate =
            await writeRepository.GetCollectionImageUploadConfigTemplateAsync(
                collectionImageUploadConfigTemplateId,
                cancellationToken
            );

        collectionImageUploadConfigTemplate.Name = name.TrimOrNullWhenWhitespace();
        collectionImageUploadConfigTemplate.ImageHosterRegistrationId = imageHosterRegistrationId;

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        int collectionImageUploadConfigTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        var collectionImageUploadConfigTemplate =
            await writeRepository.GetCollectionImageUploadConfigTemplateAsync(
                collectionImageUploadConfigTemplateId,
                cancellationToken
            );

        writeRepository.Remove(collectionImageUploadConfigTemplate);
        await writeRepository.SaveChangesAsync(cancellationToken);
    }
}

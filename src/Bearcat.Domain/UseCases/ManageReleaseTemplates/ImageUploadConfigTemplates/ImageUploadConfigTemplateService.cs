using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;

namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.ImageUploadConfigTemplates;

public class ImageUploadConfigTemplateService(IReleaseTemplateWriteRepository writeRepository)
{
    public async Task<int> CreateAsync(
        int releaseTemplateId,
        string? name,
        int imageHosterRegistrationId,
        bool uploadCover,
        bool uploadScreenshots,
        bool uploadScreenshotGrid,
        CancellationToken cancellationToken = default
    )
    {
        var releaseTemplate = await writeRepository.GetByIdWithChildrenAsync(
            releaseTemplateId,
            cancellationToken
        );

        var imageUploadConfigTemplate = new ImageUploadConfigTemplate
        {
            Name = name.TrimOrNullWhenWhitespace(),
            ImageHosterRegistrationId = imageHosterRegistrationId,
            UploadCover = uploadCover,
            UploadScreenshots = uploadScreenshots,
            UploadScreenshotGrid = uploadScreenshotGrid,
        };

        releaseTemplate.ImageUploadConfigTemplates.Add(imageUploadConfigTemplate);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return imageUploadConfigTemplate.Id;
    }

    public async Task UpdateAsync(
        int imageUploadConfigTemplateId,
        string? name,
        int imageHosterRegistrationId,
        bool uploadCover,
        bool uploadScreenshots,
        bool uploadScreenshotGrid,
        CancellationToken cancellationToken = default
    )
    {
        var imageUploadConfigTemplate = await writeRepository.GetImageUploadConfigTemplateAsync(
            imageUploadConfigTemplateId,
            cancellationToken
        );

        imageUploadConfigTemplate.Name = name.TrimOrNullWhenWhitespace();
        imageUploadConfigTemplate.ImageHosterRegistrationId = imageHosterRegistrationId;
        imageUploadConfigTemplate.UploadCover = uploadCover;
        imageUploadConfigTemplate.UploadScreenshots = uploadScreenshots;
        imageUploadConfigTemplate.UploadScreenshotGrid = uploadScreenshotGrid;

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        int imageUploadConfigTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        var imageUploadConfigTemplate = await writeRepository.GetImageUploadConfigTemplateAsync(
            imageUploadConfigTemplateId,
            cancellationToken
        );

        writeRepository.Remove(imageUploadConfigTemplate);
        await writeRepository.SaveChangesAsync(cancellationToken);
    }
}

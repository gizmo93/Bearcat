using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageImageUploadConfigs.Repositories;

namespace Bearcat.Domain.UseCases.ManageImageUploadConfigs;

public class ImageUploadConfigService(IImageUploadConfigWriteRepository writeRepository)
{
    public async Task<int> CreateAsync(
        int releaseId,
        string? name,
        int imageHosterRegistrationId,
        bool uploadCover,
        bool uploadScreenshots,
        bool uploadScreenshotGrid,
        CancellationToken cancellationToken = default
    )
    {
        var imageUploadConfig = new ImageUploadConfig
        {
            ReleaseId = releaseId,
            Name = await ResolveNameAsync(name, imageHosterRegistrationId, cancellationToken),
            ImageHosterRegistrationId = imageHosterRegistrationId,
            UploadCover = uploadCover,
            UploadScreenshots = uploadScreenshots,
            UploadScreenshotGrid = uploadScreenshotGrid,
        };

        writeRepository.Add(imageUploadConfig);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return imageUploadConfig.Id;
    }

    public async Task<int> CreateForCollectionAsync(
        int releaseCollectionId,
        string? name,
        int imageHosterRegistrationId,
        CancellationToken cancellationToken = default
    )
    {
        var imageUploadConfig = new ImageUploadConfig
        {
            ReleaseCollectionId = releaseCollectionId,
            Name = await ResolveNameAsync(name, imageHosterRegistrationId, cancellationToken),
            ImageHosterRegistrationId = imageHosterRegistrationId,
            UploadCover = true,
            UploadScreenshots = false,
            UploadScreenshotGrid = false,
        };

        writeRepository.Add(imageUploadConfig);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return imageUploadConfig.Id;
    }

    public async Task UpdateAsync(
        int imageUploadConfigId,
        string? name,
        int imageHosterRegistrationId,
        bool uploadCover,
        bool uploadScreenshots,
        bool uploadScreenshotGrid,
        CancellationToken cancellationToken = default
    )
    {
        var imageUploadConfig = await writeRepository.GetByIdAsync(
            imageUploadConfigId,
            cancellationToken
        );

        imageUploadConfig.Name = await ResolveNameAsync(
            name,
            imageHosterRegistrationId,
            cancellationToken
        );

        imageUploadConfig.ImageHosterRegistrationId = imageHosterRegistrationId;
        imageUploadConfig.UploadCover = uploadCover;
        imageUploadConfig.UploadScreenshots = uploadScreenshots;
        imageUploadConfig.UploadScreenshotGrid = uploadScreenshotGrid;

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateForCollectionAsync(
        int imageUploadConfigId,
        string? name,
        int imageHosterRegistrationId,
        CancellationToken cancellationToken = default
    )
    {
        var imageUploadConfig = await writeRepository.GetByIdAsync(
            imageUploadConfigId,
            cancellationToken
        );

        imageUploadConfig.Name = await ResolveNameAsync(
            name,
            imageHosterRegistrationId,
            cancellationToken
        );

        imageUploadConfig.ImageHosterRegistrationId = imageHosterRegistrationId;

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> ResolveNameAsync(
        string? name,
        int imageHosterRegistrationId,
        CancellationToken cancellationToken
    )
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name.Trim();
        }

        return await writeRepository.GetImageHosterRegistrationNameAsync(
            imageHosterRegistrationId,
            cancellationToken
        );
    }

    public async Task DeleteAsync(
        int imageUploadConfigId,
        CancellationToken cancellationToken = default
    )
    {
        var imageUploadConfig = await writeRepository.GetByIdAsync(
            imageUploadConfigId,
            cancellationToken
        );

        writeRepository.Remove(imageUploadConfig);
        await writeRepository.SaveChangesAsync(cancellationToken);
    }
}

using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageImageUploadConfigs.Repositories;

namespace Bearcat.Domain.UseCases.ManageImageUploadConfigs;

public class ImageUploadConfigService(IImageUploadConfigWriteRepository writeRepository)
{
    public async Task<int> CreateAsync(
        int releaseId,
        string name,
        int imageHosterRegistrationId,
        CancellationToken cancellationToken = default
    )
    {
        var imageUploadConfig = new ImageUploadConfig
        {
            ReleaseId = releaseId,
            Name = name,
            ImageHosterRegistrationId = imageHosterRegistrationId,
        };

        writeRepository.Add(imageUploadConfig);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return imageUploadConfig.Id;
    }

    public async Task UpdateAsync(
        int imageUploadConfigId,
        string name,
        int imageHosterRegistrationId,
        CancellationToken cancellationToken = default
    )
    {
        var imageUploadConfig = await writeRepository.GetByIdAsync(
            imageUploadConfigId,
            cancellationToken
        );

        imageUploadConfig.Name = name;
        imageUploadConfig.ImageHosterRegistrationId = imageHosterRegistrationId;

        await writeRepository.SaveChangesAsync(cancellationToken);
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

using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageImageUploadConfigs.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ImageUploadConfigWriteRepository(IBearcatWriteDbContext dbWrite)
    : IImageUploadConfigWriteRepository
{
    public async Task<ImageUploadConfig> GetByIdAsync(
        int imageUploadConfigId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite.ImageUploadConfigs.FirstAsync(
            config => config.Id == imageUploadConfigId,
            cancellationToken
        );
    }

    public async Task<string> GetImageHosterRegistrationNameAsync(
        int imageHosterRegistrationId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .ImageHosterRegistrations.Where(registration =>
                registration.Id == imageHosterRegistrationId
            )
            .Select(registration => registration.Name)
            .FirstAsync(cancellationToken);
    }

    public void Add(ImageUploadConfig imageUploadConfig)
    {
        dbWrite.Add(imageUploadConfig);
    }

    public void Remove(ImageUploadConfig imageUploadConfig)
    {
        dbWrite.Remove(imageUploadConfig);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await dbWrite.SaveChangesAsync(cancellationToken);
    }
}

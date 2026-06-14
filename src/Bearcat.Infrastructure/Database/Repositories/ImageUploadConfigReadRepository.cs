using Bearcat.Domain.UseCases.ManageImageUploadConfigs.ReadModels;
using Bearcat.Domain.UseCases.ManageImageUploadConfigs.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ImageUploadConfigReadRepository(IBearcatReadDbContext dbRead)
    : IImageUploadConfigReadRepository
{
    public async Task<IReadOnlyList<ImageUploadConfigReadModel>> GetImageUploadConfigsAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ImageUploadConfigs.Where(config => config.ReleaseId == releaseId)
            .OrderBy(config => config.Id)
            .Select(config => new ImageUploadConfigReadModel(
                config.Id,
                config.Name,
                config.ImageHosterRegistrationId,
                config.ImageHosterRegistration.Name,
                config.Release!.Name,
                config.ImageUploads.Count
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<ImageUploadConfigReadModel> GetReadModelByIdAsync(
        int imageUploadConfigId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ImageUploadConfigs.Where(config => config.Id == imageUploadConfigId)
            .Select(config => new ImageUploadConfigReadModel(
                config.Id,
                config.Name,
                config.ImageHosterRegistrationId,
                config.ImageHosterRegistration.Name,
                config.Release!.Name,
                config.ImageUploads.Count
            ))
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, string>> GetImageHosterRegistrationOptionsAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ImageHosterRegistrations.Where(registration => registration.IsActive)
            .ToDictionaryAsync(
                registration => registration.Id,
                registration => registration.Name,
                cancellationToken
            );
    }
}

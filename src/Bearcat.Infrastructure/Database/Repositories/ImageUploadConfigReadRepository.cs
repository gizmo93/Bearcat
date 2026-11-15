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
                ImageUploadConfigId: config.Id,
                Name: config.Name,
                ImageHosterRegistrationId: config.ImageHosterRegistrationId,
                ImageHosterRegistrationName: config.ImageHosterRegistration.Name,
                ReleaseName: config.Release!.Name,
                ImageUploadCount: config.ImageUploads.Count,
                UploadCover: config.UploadCover,
                UploadScreenshots: config.UploadScreenshots,
                UploadScreenshotGrid: config.UploadScreenshotGrid
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
                ImageUploadConfigId: config.Id,
                Name: config.Name,
                ImageHosterRegistrationId: config.ImageHosterRegistrationId,
                ImageHosterRegistrationName: config.ImageHosterRegistration.Name,
                ReleaseName: config.Release!.Name,
                ImageUploadCount: config.ImageUploads.Count,
                UploadCover: config.UploadCover,
                UploadScreenshots: config.UploadScreenshots,
                UploadScreenshotGrid: config.UploadScreenshotGrid
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

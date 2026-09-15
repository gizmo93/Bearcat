using Bearcat.Domain.UseCases.ManageImageUploads.ReadModels;
using Bearcat.Domain.UseCases.ManageImageUploads.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ImageUploadReadRepository(IBearcatReadDbContext dbRead) : IImageUploadReadRepository
{
    public async Task<IReadOnlyList<ReleaseImageUploadReadModel>> GetImageUploadsAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ImageUploads.Where(upload => upload.ImageUploadConfig.ReleaseId == releaseId)
            .OrderByDescending(upload => upload.UploadedAt ?? upload.CreatedAt)
            .ThenByDescending(upload => upload.Id)
            .Select(upload => new ReleaseImageUploadReadModel(
                upload.Id,
                upload.ImageUploadConfig.Name,
                upload.ImageUploadConfig.ImageHosterRegistration.Name,
                upload.CreatedAt,
                upload.UploadedAt,
                upload.UploadState,
                upload.ImageUrls.Count,
                upload.ErrorMessages.ToList()
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReleaseImageUploadUrlReadModel>> GetImageUploadUrlsAsync(
        int releaseId,
        int imageUploadId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ImageUploadUrls.Where(url =>
                url.ImageUploadId == imageUploadId
                && url.ImageUpload.ImageUploadConfig.ReleaseId == releaseId
            )
            .OrderBy(url => url.ImageSize)
            .ThenBy(url => url.Id)
            .Select(url => new ReleaseImageUploadUrlReadModel(url.ImageSize, url.Url))
            .ToListAsync(cancellationToken);
    }
}

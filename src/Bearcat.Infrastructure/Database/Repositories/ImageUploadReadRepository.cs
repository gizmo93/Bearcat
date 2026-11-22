using Bearcat.Domain.UseCases.ManageImageUploads.ReadModels;
using Bearcat.Domain.UseCases.ManageImageUploads.Repositories;
using Bearcat.Domain.ValueObjects;
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
                ImageUploadId: upload.Id,
                ImageUploadConfigName: upload.ImageUploadConfig.Name,
                ScreenshotKind: upload.ReleaseScreenshot == null
                    ? null
                    : (ReleaseScreenshotKind?)upload.ReleaseScreenshot.Kind,
                ScreenshotIndex: upload.ReleaseScreenshot == null
                    ? null
                    : (int?)upload.ReleaseScreenshot.Index,
                ImageHosterRegistrationName: upload.ImageUploadConfig.ImageHosterRegistration.Name,
                CreatedAt: upload.CreatedAt,
                UploadedAt: upload.UploadedAt,
                UploadState: upload.UploadState,
                OnlineState: upload.OnlineState,
                OnlineCheckedAt: upload.OnlineCheckedAt,
                OfflineSince: upload.OfflineSince,
                UrlCount: upload.ImageUrls.Count,
                ErrorMessages: upload.ErrorMessages
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

using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageImageUploads.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ImageUploadRepository(IBearcatWriteDbContext dbWrite, ISecretProtector secretProtector)
    : IImageUploadRepository
{
    public async Task CreateMissingImageUploadsAsync(
        DateTime createdAt,
        CancellationToken cancellationToken = default
    )
    {
        var configIdsWithoutUpload = await dbWrite
            .ImageUploadConfigs.Where(config =>
                !config.ImageUploads.Any()
                && config.ImageHosterRegistration.IsActive
                && (
                    (
                        config.Release != null
                        && config.Release.Metadata != null
                        && config.Release.Metadata.CoverUrl != null
                    )
                    || (
                        config.ReleaseCollection != null
                        && config.ReleaseCollection.Metadata != null
                        && config.ReleaseCollection.Metadata.CoverUrl != null
                    )
                )
            )
            .OrderBy(config => config.Id)
            .Select(config => config.Id)
            .ToListAsync(cancellationToken);

        if (configIdsWithoutUpload.Count == 0)
        {
            return;
        }

        dbWrite.ImageUploads.AddRange(
            configIdsWithoutUpload.Select(configId => new ImageUpload
            {
                ImageUploadConfigId = configId,
                CreatedAt = createdAt,
                UploadState = UploadState.Pending,
                ImageUrls = [],
                ErrorMessages = [],
            })
        );

        await dbWrite.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ImageUpload>> GetPendingImageUploadsAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .ImageUploads.AsSplitQuery()
            .Include(upload => upload.ImageUrls)
            .Include(upload => upload.ImageUploadConfig)
                .ThenInclude(config => config.ImageHosterRegistration)
            .Include(upload => upload.ImageUploadConfig)
                .ThenInclude(config => config.Release)
                    .ThenInclude(release => release!.Metadata)
            .Include(upload => upload.ImageUploadConfig)
                .ThenInclude(config => config.ReleaseCollection)
                    .ThenInclude(collection => collection!.Metadata)
            .Where(upload =>
                upload.UploadState == UploadState.Pending
                && upload.ImageUploadConfig.ImageHosterRegistration.IsActive
            )
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, string>> GetConfigByImageHosterRegistrationIdAsync(
        CancellationToken cancellationToken = default
    )
    {
        var configs = await dbWrite
            .ImageHosterRegistrations.Where(registration => registration.IsActive)
            .ToDictionaryAsync(
                registration => registration.Id,
                registration => registration.SerializedConfig,
                cancellationToken
            );

        return configs.ToDictionary(
            config => config.Key,
            config => secretProtector.Unprotect(config.Value)
        );
    }

    public async Task<ImageUpload> GetByIdAsync(
        int imageUploadId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite.ImageUploads.FirstAsync(
            upload => upload.Id == imageUploadId,
            cancellationToken
        );
    }

    public void Remove(ImageUpload imageUpload)
    {
        dbWrite.Remove(imageUpload);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await dbWrite.SaveChangesAsync(cancellationToken);
    }
}

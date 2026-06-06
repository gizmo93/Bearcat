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
        var configIdsWithUpload = await dbWrite
            .ImageUploads.Select(upload => upload.ImageUploadConfigId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var configIdsWithUploadSet = configIdsWithUpload.ToHashSet();

        var configs = await dbWrite
            .ImageUploadConfigs.Include(config => config.Release)
                .ThenInclude(release => release.ReleaseInfo)
            .Include(config => config.ImageHosterRegistration)
            .Where(config =>
                config.ImageHosterRegistration.IsActive
                && config.Release.ReleaseInfo != null
                && config.Release.ReleaseInfo.CoverUrl != null
            )
            .ToListAsync(cancellationToken);

        foreach (var config in configs.Where(config => !configIdsWithUploadSet.Contains(config.Id)))
        {
            config.ImageUploads.Add(
                new ImageUpload
                {
                    CreatedAt = createdAt,
                    UploadState = UploadState.Pending,
                    ImageUrls = [],
                    ErrorMessages = [],
                }
            );
        }

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
                    .ThenInclude(release => release.ReleaseInfo)
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

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await dbWrite.SaveChangesAsync(cancellationToken);
    }
}

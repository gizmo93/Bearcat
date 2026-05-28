using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class UploadFilesRepository(
    IBearcatWriteDbContext dbWrite,
    IBearcatReadDbContext dbRead,
    ISecretProtector secretProtector
) : IUploadFilesRepository
{
    public async Task<IReadOnlyList<Upload>> GetPendingUploadsAsync(
        IReadOnlySet<int> uploadIdsToExclude,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .Uploads.AsSplitQuery()
            .Include(u => u.UploadedFiles)
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.HosterRegistration)
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.Release)
            .Include(u => u.Archive)
                .ThenInclude(a => a!.ArchiveFiles)
            .Where(u =>
                !uploadIdsToExclude.Contains(u.Id)
                && u.UploadState == UploadState.Pending
                && u.UploadConfig.HosterRegistration.IsActive
            )
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Upload>> GetOrphanedUploadsAsync(
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .Uploads.Where(u => u.UploadState == UploadState.Uploading)
            .ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetCancellationRequestedUploadIdsAsync(
        CancellationToken cancellationToken
    )
    {
        return await dbRead
            .Uploads.Where(u => u.UploadState == UploadState.CancellationRequested)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IsCancellationRequestedAsync(
        int uploadId,
        CancellationToken cancellationToken
    )
    {
        return await dbRead.Uploads.AnyAsync(
            u => u.Id == uploadId && u.UploadState == UploadState.CancellationRequested,
            cancellationToken
        );
    }

    public async Task<Upload?> GetUploadByIdAsync(int uploadId, CancellationToken cancellationToken)
    {
        var trackedUpload = dbWrite
            .ChangeTracker.Entries<Upload>()
            .FirstOrDefault(e => e.Entity.Id == uploadId);

        if (trackedUpload is not null)
        {
            await trackedUpload.ReloadAsync(cancellationToken);
            return trackedUpload.State == EntityState.Detached ? null : trackedUpload.Entity;
        }

        return await dbWrite.Uploads.FirstOrDefaultAsync(u => u.Id == uploadId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, string>> GetConfigByHosterRegistrationId(
        CancellationToken cancellationToken
    )
    {
        var configs = await dbWrite
            .HosterRegistrations.Where(h => h.IsActive)
            .ToDictionaryAsync(h => h.Id, h => h.SerializedConfig, cancellationToken);

        return configs.ToDictionary(
            config => config.Key,
            config => secretProtector.Unprotect(config.Value)
        );
    }

    public async Task<IReadOnlyDictionary<string, string>> GetConfigByHosterClassName(
        CancellationToken cancellationToken
    )
    {
        var registrations = await dbWrite
            .HosterRegistrations.Where(h => h.IsActive)
            .Select(h => new { h.HosterClassName, h.SerializedConfig })
            .ToListAsync(cancellationToken);

        return registrations
            .DistinctBy(r => r.HosterClassName)
            .ToDictionary(
                r => r.HosterClassName,
                r => secretProtector.Unprotect(r.SerializedConfig)
            );
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await dbWrite.SaveChangesAsync(cancellationToken);
    }

    public void ClearChangeTracker()
    {
        dbWrite.ChangeTracker.Clear();
    }
}

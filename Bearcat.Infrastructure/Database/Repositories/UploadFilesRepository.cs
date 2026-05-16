using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class UploadFilesRepository(IBearcatWriteDbContext dbWrite) : IUploadFilesRepository
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
            .Include(u => u.Archive)
                .ThenInclude(a => a!.ArchiveFiles)
            .Where(u => !uploadIdsToExclude.Contains(u.Id) && u.UploadState == UploadState.Pending)
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

    public async Task<IReadOnlyDictionary<int, string>> GetConfigByHosterRegistrationId(
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .HosterRegistrations.Where(h => h.IsActive)
            .ToDictionaryAsync(h => h.Id, h => h.SerializedConfig, cancellationToken);
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
            .ToDictionary(r => r.HosterClassName, r => r.SerializedConfig);
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

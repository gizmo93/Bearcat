using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ArchiveCleanupRepository(IBearcatWriteDbContext dbWrite) : IArchiveCleanupRepository
{
    public async Task<IReadOnlyList<Archive>> GetDeletableArchivesAsync(
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .Archives.Include(a => a.Uploads)
            .Include(a => a.ArchiveConfig)
                .ThenInclude(c => c.Release)
            .Where(a =>
                a.ArchiveState == ArchiveState.Created
                && a.Uploads.Any()
                && a.Uploads.All(u => u.UploadedAt != null)
                && a.ArchiveConfig.Release.ReleaseType == ReleaseType.Managed
            )
            .OrderBy(a => a.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await dbWrite.SaveChangesAsync(cancellationToken);
    }
}

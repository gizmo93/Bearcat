using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageArchiveConfigs;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ArchiveConfigWriteRepository(IBearcatWriteDbContext dbWrite)
    : IArchiveConfigWriteRepository
{
    public void Add(ArchiveConfig archiveConfig)
    {
        dbWrite.Add(archiveConfig);
    }

    public void Remove(ArchiveConfig archiveConfig)
    {
        dbWrite.Remove(archiveConfig);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await dbWrite.SaveChangesAsync(cancellationToken);
    }

    public async Task<ArchiveConfig?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .ArchiveConfigs.AsSplitQuery()
            .Include(a => a.Release)
            .Include(a => a.Archives)
                .ThenInclude(a => a.ArchiveFiles)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken: cancellationToken);
    }
}

using BearCat.Core.Domain.Entities;
using BearCat.Core.Domain.UseCases.ManageArchiveConfigs;
using Microsoft.EntityFrameworkCore;

namespace BearCat.Core.Infrastructure.Database.Repositories;

public class ArchiveConfigWriteRepository(IBearcatWriteDbContext dbWrite) : IArchiveConfigWriteRepository
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
    
    public async Task<ArchiveConfig?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await dbWrite.ArchiveConfigs
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken: cancellationToken);
    }
}

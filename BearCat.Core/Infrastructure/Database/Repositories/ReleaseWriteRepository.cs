using BearCat.Core.Domain.Entities;
using BearCat.Core.Domain.UseCases.ManageReleases.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BearCat.Core.Infrastructure.Database.Repositories;

public class ReleaseWriteRepository(IBearcatWriteDbContext dbWrite)
    : IReleaseWriteRepository
{
    public async Task<Release> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await dbWrite.Releases
            .AsSplitQuery()
            .Include(r => r.UploadConfigs)
            .Include(r => r.ArchiveConfigs)
            .FirstAsync(r => r.Id == id, cancellationToken);
    }
    
    public void Add(Release release)
    {
        dbWrite.Add(release);
    }
    
    public void Remove(Release release)
    {
        dbWrite.Remove(release);
    }
    
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}

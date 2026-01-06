using BearCat.Core.Domain.Entities;
using BearCat.Core.Domain.UseCases.ManageReleases.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BearCat.Core.Infrastructure.Database.Repositories;

public class ReleaseRepository(IBearcatWriteDbContext dbWrite)
    : IReleaseWriteRepository
{
    public async Task<Release> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await dbWrite.Releases
            .AsSplitQuery()
            .Include(r => r.Distributions)
            .ThenInclude(d => d.Archives)
            .ThenInclude(a => a.ArchiveUpload)
            .ThenInclude(a => a!.HosterFiles)
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

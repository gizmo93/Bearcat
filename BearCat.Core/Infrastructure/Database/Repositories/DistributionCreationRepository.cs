using BearCat.Core.Domain.Entities;
using BearCat.Core.Domain.UseCases.ManageDistributions.Repositories;
using BearCat.Core.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace BearCat.Core.Infrastructure.Database.Repositories;

public class DistributionCreationRepository(IBearcatWriteDbContext dbWrite)
    : IDistributionCreationWriteRepository, IDistributionCreationReadRepository
{
    public async Task<Distribution> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await dbWrite.Distributions
            .AsSplitQuery()
            .Include(d => d.HosterRegistration)
            .Include(d => d.Release)
            .Include(d => d.Archives)
            .ThenInclude(a => a.ArchiveUpload)
            .ThenInclude(au => au!.HosterFiles)
            .FirstAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetDistributionIdsToPackAsync(CancellationToken cancellationToken)
    {
        return await dbWrite.Distributions
            .Include(d => d.Release)
            .Where(d => !d.Archives.Any()
                        || d.Archives
                            .OrderByDescending(a => a.Id)
                            .First()
                            .ArchiveUpload!
                            .HosterFiles.Any(h => h.State == HosterFileState.Offline))
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetDistributionIdsToUploadAsync(CancellationToken cancellationToken)
    {
        return await dbWrite.Distributions
            .Include(d => d.Release)
            .Where(d => d.Archives.Any()
                        && d.Archives.OrderByDescending(a=> a.Id)
                            .First()
                            .ArchiveUpload == null)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }

    public void Add(Distribution distribution)
    {
        dbWrite.Add(distribution);
    }
}

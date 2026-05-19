using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ReleaseWriteRepository(IBearcatWriteDbContext dbWrite) : IReleaseWriteRepository
{
    public async Task<Release> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await dbWrite
            .Releases.AsSplitQuery()
            .Include(r => r.ReleaseGroup)
            .Include(r => r.UploadConfigs)
            .Include(r => r.ArchiveConfigs)
            .FirstAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Release>> GetByIdsAsync(
        IReadOnlyCollection<int> ids,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite.Releases.Where(r => ids.Contains(r.Id)).ToListAsync(cancellationToken);
    }

    public async Task<ReleaseTemplate> GetTemplateForReleaseCreationAsync(
        int releaseTemplateId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .ReleaseTemplates.AsSplitQuery()
            .Include(t => t.ArchiveConfigTemplates)
            .Include(t => t.UploadConfigTemplates)
                .ThenInclude(u => u.HosterRegistration)
            .Include(t => t.UploadConfigTemplates)
                .ThenInclude(u => u.LinkCrypterTemplates)
            .FirstAsync(t => t.Id == releaseTemplateId, cancellationToken);
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

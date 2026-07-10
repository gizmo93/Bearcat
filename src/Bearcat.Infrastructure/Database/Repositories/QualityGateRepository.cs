using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class QualityGateRepository(IBearcatWriteDbContext dbWrite) : IQualityGateRepository
{
    public async Task<Release?> GetForEvaluationAsync(
        int releaseId,
        CancellationToken cancellationToken
    )
    {
        return await BuildEvaluationQuery()
            .FirstOrDefaultAsync(r => r.Id == releaseId, cancellationToken);
    }

    public async Task<IReadOnlyList<Release>> GetPendingReleasesAsync(
        CancellationToken cancellationToken
    )
    {
        return await BuildEvaluationQuery()
            .Where(r =>
                r.QualityGateState == QualityGateState.Failed
                && r.ReleaseType == ReleaseType.Managed
            )
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<Release> BuildEvaluationQuery()
    {
        return dbWrite
            .Releases.AsSplitQuery()
            .Include(r => r.ReleaseGroup)
                .ThenInclude(g => g.QualityProfile!)
                    .ThenInclude(p => p.Rules)
            .Include(r => r.ReleaseInfo)
            .Include(r => r.ReleaseNfo)
            .Include(r => r.MediaFiles)
            .Include(r => r.QualityIssues);
    }
}

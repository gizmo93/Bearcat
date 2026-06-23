using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageQualityProfiles.ReadModels;
using Bearcat.Domain.UseCases.ManageQualityProfiles.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class QualityProfileRepository(IBearcatReadDbContext dbRead, IBearcatWriteDbContext dbWrite)
    : IQualityProfileReadRepository,
        IQualityProfileWriteRepository
{
    public async Task<IReadOnlyList<QualityProfileReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .QualityProfiles.OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
            .Select(p => new QualityProfileReadModel(
                p.Id,
                p.Name,
                p.Rules.Count,
                p.ReleaseGroups.Count
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<QualityProfileDetailReadModel?> GetDetailAsync(
        int qualityProfileId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .QualityProfiles.Where(p => p.Id == qualityProfileId)
            .Select(p => new QualityProfileDetailReadModel(
                p.Id,
                p.Name,
                p.Rules.OrderBy(r => r.Id)
                    .Select(r => new QualityCheckRuleReadModel(r.RuleType, r.ParametersJson))
                    .ToList()
            ))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<QualityProfile> GetByIdAsync(
        int qualityProfileId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .QualityProfiles.Include(p => p.Rules)
            .FirstAsync(p => p.Id == qualityProfileId, cancellationToken);
    }

    public void Add(QualityProfile qualityProfile)
    {
        dbWrite.Add(qualityProfile);
    }

    public void Remove(QualityProfile qualityProfile)
    {
        dbWrite.Remove(qualityProfile);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}

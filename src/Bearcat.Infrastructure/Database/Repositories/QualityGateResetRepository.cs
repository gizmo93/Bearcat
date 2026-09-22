using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.QualityGate;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class QualityGateResetRepository(IBearcatWriteDbContext dbWrite)
    : IQualityGateResetRepository
{
    public async Task ResetForQualityProfileAsync(
        int qualityProfileId,
        CancellationToken cancellationToken
    )
    {
        await ResetAsync(
            dbWrite.Releases.Where(r => r.ReleaseGroup.QualityProfileId == qualityProfileId),
            cancellationToken
        );
    }

    public async Task ResetForReleaseGroupAsync(
        int releaseGroupId,
        CancellationToken cancellationToken
    )
    {
        await ResetAsync(
            dbWrite.Releases.Where(r => r.ReleaseGroupId == releaseGroupId),
            cancellationToken
        );
    }

    private async Task ResetAsync(IQueryable<Release> releases, CancellationToken cancellationToken)
    {
        var resettableReleases = releases.Where(r =>
            r.QualityGateState != QualityGateState.ManuallyApproved
        );

        var resettableReleaseIds = resettableReleases.Select(r => r.Id);

        await dbWrite
            .ReleaseQualityIssues.Where(i => resettableReleaseIds.Contains(i.ReleaseId))
            .ExecuteDeleteAsync(cancellationToken);

        await resettableReleases.ExecuteUpdateAsync(
            updates =>
                updates
                    .SetProperty(r => r.QualityGateState, QualityGateState.NotEvaluated)
                    .SetProperty(r => r.QualityGateEvaluatedAt, (DateTime?)null),
            cancellationToken
        );
    }
}

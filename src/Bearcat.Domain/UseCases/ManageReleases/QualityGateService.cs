using Bearcat.Domain.Shared.QualityGate;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageReleases;

public class QualityGateService(
    IQualityGateRepository repository,
    QualityGateEvaluator evaluator,
    TimeProvider timeProvider
)
{
    private const int ReleaseBatchSize = 500;

    public async Task RefreshAsync(int releaseId, CancellationToken cancellationToken = default)
    {
        var release = await repository.GetForEvaluationAsync(releaseId, cancellationToken);

        if (release is null)
        {
            return;
        }

        evaluator.EvaluateAndApply(release, timeProvider.GetLocalNow());
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task ApproveAsync(int releaseId, CancellationToken cancellationToken = default)
    {
        var release = await repository.GetForEvaluationAsync(releaseId, cancellationToken);

        if (release is null)
        {
            return;
        }

        release.QualityGateState = QualityGateState.ManuallyApproved;
        release.QualityGateEvaluatedAt = timeProvider.GetLocalNow();
        release.QualityIssues.Clear();

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task ReevaluatePendingReleasesAsync(CancellationToken cancellationToken = default)
    {
        var evaluatedAt = timeProvider.GetLocalNow();
        var afterReleaseId = 0;
        var hasMoreReleases = true;

        while (hasMoreReleases)
        {
            var releases = await repository.GetPendingReleasesAsync(
                afterReleaseId: afterReleaseId,
                maximumReleaseCount: ReleaseBatchSize,
                cancellationToken: cancellationToken
            );

            if (releases.Count > 0)
            {
                foreach (var release in releases)
                {
                    evaluator.EvaluateAndApply(release, evaluatedAt);
                }

                await repository.SaveChangesAsync(cancellationToken);
                afterReleaseId = releases[^1].Id;
                repository.ClearChangeTracker();
            }

            hasMoreReleases = releases.Count == ReleaseBatchSize;
        }
    }
}

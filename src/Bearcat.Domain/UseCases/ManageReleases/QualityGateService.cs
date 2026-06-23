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
        var releases = await repository.GetPendingReleasesAsync(cancellationToken);
        var evaluatedAt = timeProvider.GetLocalNow();

        foreach (var release in releases)
        {
            evaluator.EvaluateAndApply(release, evaluatedAt);
        }

        await repository.SaveChangesAsync(cancellationToken);
    }
}

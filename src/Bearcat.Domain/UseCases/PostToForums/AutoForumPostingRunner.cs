using Bearcat.Domain.UseCases.PostToForums.Repositories;

namespace Bearcat.Domain.UseCases.PostToForums;

public class AutoForumPostingRunner(
    IAutoForumPostingRepository repository,
    AutoForumPostingPlanService planService,
    AutoForumPostingExecutionService executionService
)
{
    public async Task<AutoPostRunResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var releaseIds = await repository.GetQueuedReleaseIdsAsync(cancellationToken);

        if (releaseIds.Count == 0)
        {
            return new AutoPostRunResult(PostedCount: 0, Failures: []);
        }

        var plans = await planService.GetPlansAsync(releaseIds, cancellationToken);

        var postedCount = 0;
        var failures = new List<AutoPostRunFailure>();

        foreach (var plan in plans.Where(plan => !plan.IsBlocked))
        {
            foreach (var site in plan.PendingSites.Where(site => site.AutomaticPostingEnabled))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await ExecuteAsync(plan, site, cancellationToken);

                if (result.Status == AutoPostExecutionStatus.Posted)
                {
                    postedCount++;
                }
                else if (result.Status == AutoPostExecutionStatus.Failed)
                {
                    failures.Add(
                        new AutoPostRunFailure(
                            ReleaseId: plan.ReleaseId,
                            ReleaseName: plan.ReleaseName,
                            DistributionSiteRegistrationId: site.DistributionSiteRegistrationId,
                            DistributionSiteRegistrationName: site.DistributionSiteRegistrationName,
                            Messages: result.Errors
                        )
                    );
                }
            }
        }

        return new AutoPostRunResult(postedCount, failures);
    }

    private async Task<AutoPostExecutionResult> ExecuteAsync(
        ReleaseAutoPostPlan plan,
        AutoPostSiteEntry site,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await executionService.ExecuteAsync(
                plan: plan,
                entry: site,
                cancellationToken: cancellationToken
            );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return AutoPostExecutionResult.Failed([exception.Message]);
        }
    }
}

using Bearcat.Domain.UseCases.PostToForums.Models;
using Bearcat.Domain.UseCases.PostToForums.Repositories;

namespace Bearcat.Domain.UseCases.PostToForums;

public class AutoForumPostingRunner(
    IAutoForumPostingRepository repository,
    AutoForumPostingPlanService planService,
    AutoForumPostingService postingService
)
{
    public async Task<AutoPostRunResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var releaseIds = await repository.GetQueuedReleaseIdsAsync(cancellationToken);

        if (releaseIds.Count == 0)
        {
            return new AutoPostRunResult(PostedCount: 0, UpdatedCount: 0, Failures: []);
        }

        var plans = await planService.GetPlansAsync(releaseIds, cancellationToken);

        var postedCount = 0;
        var updatedCount = 0;
        var failures = new List<AutoPostRunFailure>();

        foreach (var plan in plans.Where(plan => !plan.IsBlocked))
        {
            var planResult = await ProcessPlanAsync(plan, cancellationToken);

            postedCount += planResult.PostedCount;
            updatedCount += planResult.UpdatedCount;
            failures.AddRange(planResult.Failures);
        }

        return new AutoPostRunResult(postedCount, updatedCount, failures);
    }

    private async Task<AutoPostRunResult> ProcessPlanAsync(
        ReleaseAutoPostPlan plan,
        CancellationToken cancellationToken
    )
    {
        var postedCount = 0;
        var updatedCount = 0;
        var failures = new List<AutoPostRunFailure>();

        foreach (var site in plan.PendingSites.Where(site => site.AutomaticPostingEnabled))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await PostAsync(plan, site, cancellationToken);

            if (result.Status == AutoPostExecutionStatus.Posted)
            {
                postedCount++;
            }
            else if (result.Status == AutoPostExecutionStatus.Failed)
            {
                failures.Add(Failure(plan, site, result.Errors));
            }
        }

        foreach (var site in plan.AutomaticallyUpdatableSites)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await UpdateAsync(plan, site, cancellationToken);

            if (result.Status == AutoPostUpdateStatus.Updated)
            {
                updatedCount++;
            }
            else if (result.Status == AutoPostUpdateStatus.Failed)
            {
                failures.Add(Failure(plan, site, result.Errors));
            }
        }

        return new AutoPostRunResult(postedCount, updatedCount, failures);
    }

    private static AutoPostRunFailure Failure(
        ReleaseAutoPostPlan plan,
        AutoPostSiteEntry site,
        IReadOnlyList<string> messages
    )
    {
        return new AutoPostRunFailure(
            ReleaseId: plan.ReleaseId,
            ReleaseName: plan.ReleaseName,
            DistributionSiteRegistrationId: site.DistributionSiteRegistrationId,
            DistributionSiteRegistrationName: site.DistributionSiteRegistrationName,
            Messages: messages
        );
    }

    private async Task<AutoPostExecutionResult> PostAsync(
        ReleaseAutoPostPlan plan,
        AutoPostSiteEntry site,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await postingService.PostAsync(
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

    private async Task<AutoPostUpdateResult> UpdateAsync(
        ReleaseAutoPostPlan plan,
        AutoPostSiteEntry site,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await postingService.UpdateAsync(
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
            return AutoPostUpdateResult.Failed([exception.Message]);
        }
    }
}

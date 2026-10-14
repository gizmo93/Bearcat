using Bearcat.Domain.UseCases.PostToForums.Models;
using Bearcat.Domain.UseCases.PostToForums.PostCreation;
using Bearcat.Domain.UseCases.PostToForums.PostUpdate;
using Bearcat.Domain.UseCases.PostToForums.Repositories;

namespace Bearcat.Domain.UseCases.PostToForums;

public class AutoForumPostingRunner(
    IAutoForumPostingRepository repository,
    AutoForumPostingPlanService planService,
    AutoForumPostCreationService postCreationService,
    AutoForumPostUpdateService postUpdateService
)
{
    private const int ReleaseBatchSize = 500;

    public async Task<AutoPostRunResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var releaseIds = await repository.GetQueuedReleaseIdsAsync(cancellationToken);

        var postedCount = 0;
        var updatedCount = 0;
        var failures = new List<AutoPostRunFailure>();

        foreach (var releaseIdBatch in releaseIds.Chunk(ReleaseBatchSize))
        {
            var plans = await planService.GetPlansAsync(releaseIdBatch, cancellationToken);

            foreach (var plan in plans.Where(plan => !plan.IsBlocked))
            {
                var planResult = await ProcessPlanAsync(plan, cancellationToken);

                postedCount += planResult.PostedCount;
                updatedCount += planResult.UpdatedCount;
                failures.AddRange(planResult.Failures);
            }
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

            var result = await CreatePostForSiteAsync(plan, site, cancellationToken);

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

            var result = await UpdatePostOfSiteAsync(plan, site, cancellationToken);

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

    private async Task<AutoPostExecutionResult> CreatePostForSiteAsync(
        ReleaseAutoPostPlan plan,
        AutoPostSiteEntry site,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await postCreationService.CreatePostForPlannedSiteAsync(
                plan: plan,
                site: site,
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

    private async Task<AutoPostUpdateResult> UpdatePostOfSiteAsync(
        ReleaseAutoPostPlan plan,
        AutoPostSiteEntry site,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await postUpdateService.UpdatePostOfPlannedSiteAsync(
                plan: plan,
                site: site,
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

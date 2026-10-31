using Bearcat.Domain.UseCases.PostToForums.Models;
using Bearcat.Domain.UseCases.PostToForums.PostCreation;
using Bearcat.Domain.UseCases.PostToForums.PostUpdate;
using Bearcat.Domain.UseCases.PostToForums.Repositories;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.PostToForums;

public class AutoForumPostingRunner(
    IAutoForumPostingRepository repository,
    AutoForumPostingPlanService planService,
    AutoForumPostCreationService postCreationService,
    AutoForumPostUpdateService postUpdateService
)
{
    private const int EntityBatchSize = 500;

    public async Task<AutoPostRunResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var releaseIds = await repository.GetQueuedReleaseIdsAsync(cancellationToken);
        var releaseResult = await ProcessQueueAsync(
            entityType: ForumPostTemplateType.Release,
            entityIds: releaseIds,
            cancellationToken: cancellationToken
        );

        var releaseCollectionIds = await repository.GetQueuedReleaseCollectionIdsAsync(
            cancellationToken
        );
        var releaseCollectionResult = await ProcessQueueAsync(
            entityType: ForumPostTemplateType.ReleaseCollection,
            entityIds: releaseCollectionIds,
            cancellationToken: cancellationToken
        );

        return new AutoPostRunResult(
            PostedCount: releaseResult.PostedCount + releaseCollectionResult.PostedCount,
            UpdatedCount: releaseResult.UpdatedCount + releaseCollectionResult.UpdatedCount,
            Failures: [.. releaseResult.Failures, .. releaseCollectionResult.Failures]
        );
    }

    private async Task<AutoPostRunResult> ProcessQueueAsync(
        ForumPostTemplateType entityType,
        IReadOnlyList<int> entityIds,
        CancellationToken cancellationToken
    )
    {
        var postedCount = 0;
        var updatedCount = 0;
        var failures = new List<AutoPostRunFailure>();

        foreach (var entityIdBatch in entityIds.Chunk(EntityBatchSize))
        {
            var plans = await planService.GetPlansAsync(
                entityType: entityType,
                entityIds: entityIdBatch,
                cancellationToken: cancellationToken
            );

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
        AutoPostPlan plan,
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
        AutoPostPlan plan,
        AutoPostSiteEntry site,
        IReadOnlyList<string> messages
    )
    {
        return new AutoPostRunFailure(
            EntityType: plan.EntityType,
            EntityId: plan.EntityId,
            EntityName: plan.EntityName,
            DistributionSiteRegistrationId: site.DistributionSiteRegistrationId,
            DistributionSiteRegistrationName: site.DistributionSiteRegistrationName,
            Messages: messages
        );
    }

    private async Task<AutoPostExecutionResult> CreatePostForSiteAsync(
        AutoPostPlan plan,
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
        AutoPostPlan plan,
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

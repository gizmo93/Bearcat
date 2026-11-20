using System.Diagnostics;
using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.PostToForums.Models;
using Bearcat.Domain.UseCases.PostToForums.PostCreation;
using Bearcat.Domain.UseCases.PostToForums.PostUpdate;
using Bearcat.Domain.UseCases.PostToForums.Repositories;
using Bearcat.Domain.ValueObjects;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.PostToForums;

public class AutoForumPostingRunner(
    IAutoForumPostingRepository repository,
    AutoForumPostingPlanService planService,
    AutoForumPostCreationService postCreationService,
    AutoForumPostUpdateService postUpdateService,
    INotificationService notificationService,
    TimeProvider timeProvider
)
{
    private const int EntityBatchSize = 500;

    private const int FailedAttemptCountBeforeGivingUp = 5;

    private const int MaximumErrorMessageLength = 2000;

    private static readonly IReadOnlyList<TimeSpan> RetryDelaysByFailedAttemptCount =
    [
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(4),
        TimeSpan.FromHours(12),
    ];

    public async Task<AutoPostRunResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var lastAttemptTimestampsByRegistrationId = new Dictionary<int, long>();

        var releaseIds = await repository.GetQueuedReleaseIdsAsync(cancellationToken);
        var releaseResult = await ProcessQueueAsync(
            entityType: ForumPostTemplateType.Release,
            entityIds: releaseIds,
            lastAttemptTimestampsByRegistrationId: lastAttemptTimestampsByRegistrationId,
            cancellationToken: cancellationToken
        );

        var releaseCollectionIds = await repository.GetQueuedReleaseCollectionIdsAsync(
            cancellationToken
        );
        var releaseCollectionResult = await ProcessQueueAsync(
            entityType: ForumPostTemplateType.ReleaseCollection,
            entityIds: releaseCollectionIds,
            lastAttemptTimestampsByRegistrationId: lastAttemptTimestampsByRegistrationId,
            cancellationToken: cancellationToken
        );

        return new AutoPostRunResult(
            PostedCount: releaseResult.PostedCount + releaseCollectionResult.PostedCount,
            UpdatedCount: releaseResult.UpdatedCount + releaseCollectionResult.UpdatedCount,
            Failures: releaseResult.Failures.Concat(releaseCollectionResult.Failures).ToList()
        );
    }

    private async Task<AutoPostRunResult> ProcessQueueAsync(
        ForumPostTemplateType entityType,
        IReadOnlyList<int> entityIds,
        Dictionary<int, long> lastAttemptTimestampsByRegistrationId,
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
                var planResult = await ProcessPlanAsync(
                    plan: plan,
                    lastAttemptTimestampsByRegistrationId: lastAttemptTimestampsByRegistrationId,
                    cancellationToken: cancellationToken
                );

                postedCount += planResult.PostedCount;
                updatedCount += planResult.UpdatedCount;
                failures.AddRange(planResult.Failures);
            }
        }

        return new AutoPostRunResult(postedCount, updatedCount, failures);
    }

    private async Task<AutoPostRunResult> ProcessPlanAsync(
        AutoPostPlan plan,
        Dictionary<int, long> lastAttemptTimestampsByRegistrationId,
        CancellationToken cancellationToken
    )
    {
        var postedCount = 0;
        var updatedCount = 0;
        var failures = new List<AutoPostRunFailure>();

        var pendingSites = plan
            .PendingSites.Where(site =>
                site.Registration.EnableAutomaticPosting && !IsPausedAfterFailures(site)
            )
            .ToList();

        foreach (var site in pendingSites)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await WaitForMinimumIntervalAsync(
                site: site,
                lastAttemptTimestampsByRegistrationId: lastAttemptTimestampsByRegistrationId,
                cancellationToken: cancellationToken
            );

            var result = await postCreationService.CreatePostForPlannedSiteAsync(
                plan: plan,
                site: site,
                cancellationToken: cancellationToken
            );

            if (result.Status is AutoPostResultStatus.Posted or AutoPostResultStatus.Failed)
            {
                lastAttemptTimestampsByRegistrationId[site.DistributionSiteRegistrationId] =
                    Stopwatch.GetTimestamp();
            }

            if (result.Status == AutoPostResultStatus.Posted)
            {
                postedCount++;
            }
            else if (result.Status == AutoPostResultStatus.Failed)
            {
                failures.Add(Failure(plan, site, result.Errors));

                await RecordFailedAttemptAsync(
                    plan: plan,
                    site: site,
                    errors: result.Errors,
                    gaveUpNotificationKind: NotificationKind.AutomaticForumPostFailed,
                    actionDescription: $"posting {EntityLabel(plan)} '{plan.EntityName}' automatically to '{site.Registration.Name}'",
                    cancellationToken: cancellationToken
                );
            }
            else if (result.Status == AutoPostResultStatus.ThreadSelectionNeeded)
            {
                await RecordWaitingForThreadSelectionAsync(
                    plan: plan,
                    site: site,
                    threadCandidates: result.ThreadCandidates,
                    cancellationToken: cancellationToken
                );
            }
        }

        var updatableSites = plan
            .AutomaticallyUpdatableSites.Where(site => !IsPausedAfterFailures(site))
            .ToList();

        foreach (var site in updatableSites)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await WaitForMinimumIntervalAsync(
                site: site,
                lastAttemptTimestampsByRegistrationId: lastAttemptTimestampsByRegistrationId,
                cancellationToken: cancellationToken
            );

            var result = await postUpdateService.UpdatePostOfPlannedSiteAsync(
                plan: plan,
                site: site,
                cancellationToken: cancellationToken
            );

            if (result.Status != AutoPostResultStatus.Skipped)
            {
                lastAttemptTimestampsByRegistrationId[site.DistributionSiteRegistrationId] =
                    Stopwatch.GetTimestamp();
            }

            if (result.Status == AutoPostResultStatus.Updated)
            {
                updatedCount++;
            }
            else if (result.Status == AutoPostResultStatus.Failed)
            {
                failures.Add(Failure(plan, site, result.Errors));

                await RecordFailedAttemptAsync(
                    plan: plan,
                    site: site,
                    errors: result.Errors,
                    gaveUpNotificationKind: NotificationKind.AutomaticForumPostUpdateFailed,
                    actionDescription: $"updating the post of '{plan.EntityName}' in '{site.Registration.Name}'",
                    cancellationToken: cancellationToken
                );
            }
        }

        return new AutoPostRunResult(postedCount, updatedCount, failures);
    }

    private bool IsPausedAfterFailures(AutoPostSiteEntry site)
    {
        return site.Failure switch
        {
            { State: ForumPostingFailureState.GaveUp } => true,
            { State: ForumPostingFailureState.WaitingForThreadSelection } => true,
            { State: ForumPostingFailureState.RetryScheduled } failure => failure.NextAttemptAt
                > timeProvider.GetLocalNow(),
            _ => false,
        };
    }

    private static async Task WaitForMinimumIntervalAsync(
        AutoPostSiteEntry site,
        Dictionary<int, long> lastAttemptTimestampsByRegistrationId,
        CancellationToken cancellationToken
    )
    {
        if (
            !lastAttemptTimestampsByRegistrationId.TryGetValue(
                site.DistributionSiteRegistrationId,
                out var lastAttemptTimestamp
            )
        )
        {
            return;
        }

        var remaining =
            TimeSpan.FromSeconds(site.Registration.MinimumSecondsBetweenPosts)
            - Stopwatch.GetElapsedTime(lastAttemptTimestamp);

        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining, cancellationToken);
        }
    }

    private async Task RecordFailedAttemptAsync(
        AutoPostPlan plan,
        AutoPostSiteEntry site,
        IReadOnlyList<string> errors,
        NotificationKind gaveUpNotificationKind,
        string actionDescription,
        CancellationToken cancellationToken
    )
    {
        var failedAttemptCount = (site.Failure?.FailedAttemptCount ?? 0) + 1;
        var hasGivenUp = failedAttemptCount >= FailedAttemptCountBeforeGivingUp;
        var errorMessage = TruncateErrorMessage(string.Join(" ", errors));

        await repository.SaveForumPostingFailureAsync(
            entityType: plan.EntityType,
            entityId: plan.EntityId,
            distributionSiteRegistrationId: site.DistributionSiteRegistrationId,
            failedAttemptCount: failedAttemptCount,
            nextAttemptAt: hasGivenUp
                ? null
                : timeProvider.GetLocalNow()
                    + RetryDelaysByFailedAttemptCount[failedAttemptCount - 1],
            lastErrorMessage: errorMessage,
            state: hasGivenUp
                ? ForumPostingFailureState.GaveUp
                : ForumPostingFailureState.RetryScheduled,
            cancellationToken: cancellationToken
        );

        if (hasGivenUp)
        {
            await notificationService.CreateAsync(
                kind: gaveUpNotificationKind,
                message: $"Gave up {actionDescription} after {failedAttemptCount} failed attempts: {errorMessage}",
                cancellationToken: cancellationToken
            );
        }
    }

    private async Task RecordWaitingForThreadSelectionAsync(
        AutoPostPlan plan,
        AutoPostSiteEntry site,
        IReadOnlyList<ExistingThread> threadCandidates,
        CancellationToken cancellationToken
    )
    {
        var threadTitles = string.Join(" | ", threadCandidates.Select(thread => thread.Title));

        await repository.SaveForumPostingFailureAsync(
            entityType: plan.EntityType,
            entityId: plan.EntityId,
            distributionSiteRegistrationId: site.DistributionSiteRegistrationId,
            failedAttemptCount: site.Failure?.FailedAttemptCount ?? 0,
            nextAttemptAt: null,
            lastErrorMessage: TruncateErrorMessage(
                $"Several matching threads found: {threadTitles}"
            ),
            state: ForumPostingFailureState.WaitingForThreadSelection,
            cancellationToken: cancellationToken
        );

        await notificationService.CreateAsync(
            kind: NotificationKind.AutomaticForumPostFailed,
            message: $"Several matching threads were found for {EntityLabel(plan)} '{plan.EntityName}' in '{site.Registration.Name}'. Choose the thread in the post dialog.",
            cancellationToken: cancellationToken
        );
    }

    private static string TruncateErrorMessage(string errorMessage)
    {
        return errorMessage.Length <= MaximumErrorMessageLength
            ? errorMessage
            : errorMessage[..MaximumErrorMessageLength];
    }

    private static string EntityLabel(AutoPostPlan plan)
    {
        return plan.EntityType == ForumPostTemplateType.Release ? "release" : "release collection";
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
            DistributionSiteRegistrationName: site.Registration.Name,
            Messages: messages
        );
    }
}

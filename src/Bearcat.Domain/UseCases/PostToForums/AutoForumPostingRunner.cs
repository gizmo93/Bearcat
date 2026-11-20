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
        var progress = new RunProgress();

        var releaseIds = await repository.GetQueuedReleaseIdsAsync(cancellationToken);

        await ProcessQueueAsync(
            entityType: ForumPostTemplateType.Release,
            entityIds: releaseIds,
            progress: progress,
            cancellationToken: cancellationToken
        );

        var releaseCollectionIds = await repository.GetQueuedReleaseCollectionIdsAsync(
            cancellationToken
        );

        await ProcessQueueAsync(
            entityType: ForumPostTemplateType.ReleaseCollection,
            entityIds: releaseCollectionIds,
            progress: progress,
            cancellationToken: cancellationToken
        );

        return new AutoPostRunResult(
            PostedCount: progress.PostedCount,
            UpdatedCount: progress.UpdatedCount,
            Failures: progress.Failures
        );
    }

    private async Task ProcessQueueAsync(
        ForumPostTemplateType entityType,
        IReadOnlyList<int> entityIds,
        RunProgress progress,
        CancellationToken cancellationToken
    )
    {
        foreach (var entityIdBatch in entityIds.Chunk(EntityBatchSize))
        {
            var plans = await planService.GetPlansAsync(
                entityType: entityType,
                entityIds: entityIdBatch,
                cancellationToken: cancellationToken
            );

            foreach (var plan in plans.Where(plan => !plan.IsBlocked))
            {
                await ProcessPlanAsync(plan, progress, cancellationToken);
            }
        }
    }

    private async Task ProcessPlanAsync(
        AutoPostPlan plan,
        RunProgress progress,
        CancellationToken cancellationToken
    )
    {
        var pendingSites = plan.PendingSites.Where(site =>
            site.Registration.EnableAutomaticPosting && !IsPausedAfterFailures(site)
        );

        var updatableSites = plan.AutomaticallyUpdatableSites.Where(site =>
            !IsPausedAfterFailures(site)
        );

        var sites = pendingSites.Concat(updatableSites).ToList();

        foreach (var site in sites)
        {
            await PostOrUpdateSiteAsync(plan, site, progress, cancellationToken);
        }
    }

    private async Task PostOrUpdateSiteAsync(
        AutoPostPlan plan,
        AutoPostSiteEntry site,
        RunProgress progress,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        await WaitForMinimumIntervalAsync(site, progress, cancellationToken);

        var isUpdate = site.PostedLocation is not null;

        var result = isUpdate
            ? await postUpdateService.UpdatePostOfPlannedSiteAsync(
                plan: plan,
                site: site,
                cancellationToken: cancellationToken
            )
            : await postCreationService.CreatePostForPlannedSiteAsync(
                plan: plan,
                site: site,
                cancellationToken: cancellationToken
            );

        if (
            result.Status
            is AutoPostResultStatus.Posted
                or AutoPostResultStatus.Updated
                or AutoPostResultStatus.Failed
        )
        {
            progress.LastAttemptTimestampsByRegistrationId[site.DistributionSiteRegistrationId] =
                Stopwatch.GetTimestamp();
        }

        if (result.Status == AutoPostResultStatus.Posted)
        {
            progress.PostedCount++;
        }
        else if (result.Status == AutoPostResultStatus.Updated)
        {
            progress.UpdatedCount++;
        }
        else if (result.Status == AutoPostResultStatus.Failed)
        {
            progress.Failures.Add(BuildFailure(plan, site, result.Errors));

            await RecordFailedAttemptAsync(
                plan: plan,
                site: site,
                errors: result.Errors,
                gaveUpNotificationKind: isUpdate
                    ? NotificationKind.AutomaticForumPostUpdateFailed
                    : NotificationKind.AutomaticForumPostFailed,
                actionDescription: isUpdate
                    ? $"updating the post of '{plan.EntityName}' in '{site.Registration.Name}'"
                    : $"posting {EntityLabel(plan)} '{plan.EntityName}' automatically to '{site.Registration.Name}'",
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
        RunProgress progress,
        CancellationToken cancellationToken
    )
    {
        if (
            !progress.LastAttemptTimestampsByRegistrationId.TryGetValue(
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

    private static AutoPostRunFailure BuildFailure(
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

    private sealed class RunProgress
    {
        public int PostedCount { get; set; }

        public int UpdatedCount { get; set; }

        public List<AutoPostRunFailure> Failures { get; } = [];

        public Dictionary<int, long> LastAttemptTimestampsByRegistrationId { get; } = [];
    }
}

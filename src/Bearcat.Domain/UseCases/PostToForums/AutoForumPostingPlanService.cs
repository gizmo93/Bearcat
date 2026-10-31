using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ForumPostingRules;
using Bearcat.Domain.UseCases.PostToForums.Models;
using Bearcat.Domain.UseCases.PostToForums.Repositories;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.PostToForums;

public class AutoForumPostingPlanService(IAutoForumPostingRepository repository)
{
    public async Task<AutoPostPlan> GetPlanAsync(
        ForumPostTemplateType entityType,
        int entityId,
        CancellationToken cancellationToken = default
    )
    {
        var plans = await GetPlansAsync(entityType, [entityId], cancellationToken);

        return plans[0];
    }

    public async Task<IReadOnlyList<AutoPostPlan>> GetPlansAsync(
        ForumPostTemplateType entityType,
        IReadOnlyList<int> entityIds,
        CancellationToken cancellationToken = default
    )
    {
        var entitiesToPlanById =
            entityType == ForumPostTemplateType.Release
                ? await GetReleasesToPlanAsync(entityIds, cancellationToken)
                : await GetReleaseCollectionsToPlanAsync(entityIds, cancellationToken);

        var registrations = await repository.GetForumRegistrationsWithEnabledRulesAsync(
            cancellationToken
        );

        var postedLocations = await repository.GetPostedLocationsAsync(
            entityType: entityType,
            entityIds: entityIds,
            cancellationToken: cancellationToken
        );

        var latestUploadTimes = await repository.GetLatestUploadCompletionTimesAsync(
            entityType: entityType,
            entityIds: entityIds,
            cancellationToken: cancellationToken
        );

        return entityIds
            .Select(entityId =>
                BuildPlan(
                    entityType: entityType,
                    entityToPlan: entitiesToPlanById[entityId],
                    registrations: registrations,
                    postedLocations: postedLocations
                        .Where(location => location.EntityId == entityId)
                        .ToList(),
                    latestUploadCompletedAt: latestUploadTimes.TryGetValue(
                        entityId,
                        out var latestUploadCompletedAt
                    )
                        ? latestUploadCompletedAt
                        : null
                )
            )
            .ToList();
    }

    private async Task<Dictionary<int, EntityToPlan>> GetReleasesToPlanAsync(
        IReadOnlyList<int> releaseIds,
        CancellationToken cancellationToken
    )
    {
        var releases = await repository.GetReleasesForRoutingAsync(releaseIds, cancellationToken);

        return releases.ToDictionary(
            release => release.Id,
            release => new EntityToPlan(
                Id: release.Id,
                Name: release.Name,
                BlockedReason: FindBlockedReason(release),
                RoutingContext: ReleaseRoutingContext.FromRelease(release)
            )
        );
    }

    private async Task<Dictionary<int, EntityToPlan>> GetReleaseCollectionsToPlanAsync(
        IReadOnlyList<int> releaseCollectionIds,
        CancellationToken cancellationToken
    )
    {
        var collections = await repository.GetReleaseCollectionsForRoutingAsync(
            releaseCollectionIds,
            cancellationToken
        );

        return collections.ToDictionary(
            collection => collection.Id,
            collection => new EntityToPlan(
                Id: collection.Id,
                Name: collection.Name,
                BlockedReason: FindBlockedReason(collection),
                RoutingContext: ReleaseRoutingContext.FromReleaseCollection(collection)
            )
        );
    }

    private static AutoPostPlan BuildPlan(
        ForumPostTemplateType entityType,
        EntityToPlan entityToPlan,
        IReadOnlyList<AutoPostRegistration> registrations,
        IReadOnlyList<AutoPostPostedLocation> postedLocations,
        DateTime? latestUploadCompletedAt
    )
    {
        if (entityToPlan.BlockedReason is not null)
        {
            return new AutoPostPlan(
                EntityType: entityType,
                EntityId: entityToPlan.Id,
                EntityName: entityToPlan.Name,
                BlockedReason: entityToPlan.BlockedReason,
                Sites: []
            );
        }

        var sites = registrations
            .Select(registration =>
                BuildSiteEntry(
                    registration: registration,
                    entityType: entityType,
                    context: entityToPlan.RoutingContext,
                    postedLocations: postedLocations,
                    latestUploadCompletedAt: latestUploadCompletedAt
                )
            )
            .ToList();

        return new AutoPostPlan(
            EntityType: entityType,
            EntityId: entityToPlan.Id,
            EntityName: entityToPlan.Name,
            BlockedReason: null,
            Sites: sites
        );
    }

    private static AutoPostBlockedReason? FindBlockedReason(Release release)
    {
        if (!HasPassedQualityGate(release))
        {
            return AutoPostBlockedReason.QualityGateNotPassed;
        }

        return release.Classification is null ? AutoPostBlockedReason.ClassificationMissing : null;
    }

    private static AutoPostBlockedReason? FindBlockedReason(ReleaseCollection collection)
    {
        if (!collection.Releases.All(HasPassedQualityGate))
        {
            return AutoPostBlockedReason.QualityGateNotPassed;
        }

        return collection.Releases.Any(release => release.Classification is not null)
            ? null
            : AutoPostBlockedReason.ClassificationMissing;
    }

    private static bool HasPassedQualityGate(Release release)
    {
        return release.QualityGateState
            is QualityGateState.Passed
                or QualityGateState.ManuallyApproved;
    }

    private static AutoPostSiteEntry BuildSiteEntry(
        AutoPostRegistration registration,
        ForumPostTemplateType entityType,
        ReleaseRoutingContext context,
        IReadOnlyList<AutoPostPostedLocation> postedLocations,
        DateTime? latestUploadCompletedAt
    )
    {
        var rule = ForumPostingRuleMatcher.FindFirstMatch(
            rules: registration.EnabledRules,
            entityType: entityType,
            context: context
        );

        var match = rule is null
            ? null
            : new AutoPostRuleMatch(
                ForumPostingRuleId: rule.Id,
                RuleName: rule.Name,
                TargetNodeId: rule.TargetNodeId,
                TargetPathSnapshot: rule.TargetPathSnapshot,
                ThreadPrefixId: rule.ThreadPrefixId,
                ForumPostTemplateId: rule.ForumPostTemplateId,
                PostMode: rule.PostMode
            );

        var postedLocation = postedLocations.FirstOrDefault(location =>
            location.DistributionSiteRegistrationId == registration.DistributionSiteRegistrationId
        );

        if (postedLocation is not null)
        {
            return new AutoPostSiteEntry(
                DistributionSiteRegistrationId: registration.DistributionSiteRegistrationId,
                DistributionSiteRegistrationName: registration.Name,
                AutomaticPostingEnabled: registration.EnableAutomaticPosting,
                StripDotsForThreadSearch: registration.StripDotsForThreadSearch,
                Status: AutoPostSiteStatus.AlreadyPosted,
                PostedUrl: postedLocation.Url,
                Match: match,
                PostedLocationId: postedLocation.PostedLocationId,
                StoredForumPostTemplateId: postedLocation.ForumPostTemplateId,
                NeedsContentUpdate: IsOutdated(postedLocation, latestUploadCompletedAt)
            );
        }

        return new AutoPostSiteEntry(
            DistributionSiteRegistrationId: registration.DistributionSiteRegistrationId,
            DistributionSiteRegistrationName: registration.Name,
            AutomaticPostingEnabled: registration.EnableAutomaticPosting,
            StripDotsForThreadSearch: registration.StripDotsForThreadSearch,
            Status: match is null ? AutoPostSiteStatus.NoMatch : AutoPostSiteStatus.Matched,
            PostedUrl: null,
            Match: match,
            PostedLocationId: null,
            StoredForumPostTemplateId: null,
            NeedsContentUpdate: false
        );
    }

    private static bool IsOutdated(
        AutoPostPostedLocation postedLocation,
        DateTime? latestUploadCompletedAt
    )
    {
        return latestUploadCompletedAt is { } uploadedAt
            && (postedLocation.ContentUpdatedAt ?? postedLocation.CreatedAt) < uploadedAt;
    }

    private sealed record EntityToPlan(
        int Id,
        string Name,
        AutoPostBlockedReason? BlockedReason,
        ReleaseRoutingContext RoutingContext
    );
}

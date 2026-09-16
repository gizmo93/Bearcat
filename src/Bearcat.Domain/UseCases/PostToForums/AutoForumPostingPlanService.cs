using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ForumPostingRules;
using Bearcat.Domain.UseCases.PostToForums.Models;
using Bearcat.Domain.UseCases.PostToForums.Repositories;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.PostToForums;

public class AutoForumPostingPlanService(IAutoForumPostingRepository repository)
{
    public async Task<ReleaseAutoPostPlan> GetPlanAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var plans = await GetPlansAsync([releaseId], cancellationToken);

        return plans[0];
    }

    public async Task<IReadOnlyList<ReleaseAutoPostPlan>> GetPlansAsync(
        IReadOnlyList<int> releaseIds,
        CancellationToken cancellationToken = default
    )
    {
        var releases = await repository.GetReleasesForRoutingAsync(releaseIds, cancellationToken);
        var releasesById = releases.ToDictionary(release => release.Id);

        var registrations = await repository.GetForumRegistrationsWithEnabledRulesAsync(
            cancellationToken
        );

        var postedLocations = await repository.GetPostedLocationsAsync(
            releaseIds,
            cancellationToken
        );

        var latestUploadTimes = await repository.GetLatestUploadCompletionTimesAsync(
            releaseIds,
            cancellationToken
        );

        return releaseIds
            .Select(releaseId =>
                BuildPlan(
                    release: releasesById[releaseId],
                    registrations: registrations,
                    postedLocations: postedLocations
                        .Where(location => location.ReleaseId == releaseId)
                        .ToList(),
                    latestUploadCompletedAt: latestUploadTimes.TryGetValue(
                        releaseId,
                        out var latestUploadCompletedAt
                    )
                        ? latestUploadCompletedAt
                        : null
                )
            )
            .ToList();
    }

    private static ReleaseAutoPostPlan BuildPlan(
        Release release,
        IReadOnlyList<AutoPostRegistration> registrations,
        IReadOnlyList<AutoPostPostedLocation> postedLocations,
        DateTime? latestUploadCompletedAt
    )
    {
        var blockedReason = FindBlockedReason(release);

        if (blockedReason is not null)
        {
            return new ReleaseAutoPostPlan(
                ReleaseId: release.Id,
                ReleaseName: release.Name,
                BlockedReason: blockedReason,
                Sites: []
            );
        }

        var context = ReleaseRoutingContext.FromRelease(release);

        var sites = registrations
            .Select(registration =>
                BuildSiteEntry(registration, context, postedLocations, latestUploadCompletedAt)
            )
            .ToList();

        return new ReleaseAutoPostPlan(
            ReleaseId: release.Id,
            ReleaseName: release.Name,
            BlockedReason: null,
            Sites: sites
        );
    }

    private static AutoPostBlockedReason? FindBlockedReason(Release release)
    {
        if (
            release.QualityGateState
            is not (QualityGateState.Passed or QualityGateState.ManuallyApproved)
        )
        {
            return AutoPostBlockedReason.QualityGateNotPassed;
        }

        return release.Classification is null ? AutoPostBlockedReason.ClassificationMissing : null;
    }

    private static AutoPostSiteEntry BuildSiteEntry(
        AutoPostRegistration registration,
        ReleaseRoutingContext context,
        IReadOnlyList<AutoPostPostedLocation> postedLocations,
        DateTime? latestUploadCompletedAt
    )
    {
        var rule = ForumPostingRuleMatcher.FindFirstMatch(registration.EnabledRules, context);

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
}

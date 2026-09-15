using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ForumPostingRules;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Shared.AutoForumPosting;

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

        return releaseIds
            .Select(releaseId =>
                BuildPlan(
                    release: releasesById[releaseId],
                    registrations: registrations,
                    postedLocations: postedLocations
                        .Where(location => location.ReleaseId == releaseId)
                        .ToList()
                )
            )
            .ToList();
    }

    private static ReleaseAutoPostPlan BuildPlan(
        Release release,
        IReadOnlyList<AutoPostRegistration> registrations,
        IReadOnlyList<AutoPostPostedLocation> postedLocations
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
            .Select(registration => BuildSiteEntry(registration, context, postedLocations))
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
        IReadOnlyList<AutoPostPostedLocation> postedLocations
    )
    {
        var postedLocation = postedLocations.FirstOrDefault(location =>
            location.DistributionSiteRegistrationId == registration.DistributionSiteRegistrationId
        );

        if (postedLocation is not null)
        {
            return new AutoPostSiteEntry(
                DistributionSiteRegistrationId: registration.DistributionSiteRegistrationId,
                DistributionSiteRegistrationName: registration.Name,
                Status: AutoPostSiteStatus.AlreadyPosted,
                PostedUrl: postedLocation.Url,
                Match: null
            );
        }

        var rule = ForumPostingRuleMatcher.FindFirstMatch(registration.EnabledRules, context);

        if (rule is null)
        {
            return new AutoPostSiteEntry(
                DistributionSiteRegistrationId: registration.DistributionSiteRegistrationId,
                DistributionSiteRegistrationName: registration.Name,
                Status: AutoPostSiteStatus.NoMatch,
                PostedUrl: null,
                Match: null
            );
        }

        return new AutoPostSiteEntry(
            DistributionSiteRegistrationId: registration.DistributionSiteRegistrationId,
            DistributionSiteRegistrationName: registration.Name,
            Status: AutoPostSiteStatus.Matched,
            PostedUrl: null,
            Match: new AutoPostRuleMatch(
                ForumPostingRuleId: rule.Id,
                RuleName: rule.Name,
                TargetNodeId: rule.TargetNodeId,
                TargetPathSnapshot: rule.TargetPathSnapshot,
                ThreadPrefixId: rule.ThreadPrefixId,
                ForumPostTemplateId: rule.ForumPostTemplateId,
                PostMode: rule.PostMode
            )
        );
    }
}

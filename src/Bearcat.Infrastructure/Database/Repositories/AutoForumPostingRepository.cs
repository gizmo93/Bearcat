using Bearcat.Abstractions.DistributionSite;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.PostToForums;
using Bearcat.Domain.UseCases.PostToForums.Repositories;
using Microsoft.EntityFrameworkCore;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Infrastructure.Database.Repositories;

public class AutoForumPostingRepository(
    IBearcatReadDbContext dbRead,
    IBearcatWriteDbContext dbWrite,
    IDistributionSiteFactory distributionSiteFactory,
    TimeProvider timeProvider
) : IAutoForumPostingRepository
{
    public async Task<
        IReadOnlyList<AutoPostRegistration>
    > GetForumRegistrationsWithEnabledRulesAsync(CancellationToken cancellationToken = default)
    {
        var forumClassNames = distributionSiteFactory
            .GetDistributionSites()
            .Where(distributionSite => distributionSite.Kind == DistributionSiteKind.Forum)
            .Select(distributionSite => distributionSite.ClassName)
            .ToList();

        var registrations = await dbRead
            .DistributionSiteRegistrations.Where(registration =>
                registration.IsActive
                && forumClassNames.Contains(registration.DistributionSiteClassName)
                && registration.PostingRules.Any(rule => rule.IsEnabled)
            )
            .OrderBy(registration => registration.Name)
            .ThenBy(registration => registration.Id)
            .Select(registration => new
            {
                registration.Id,
                registration.Name,
                registration.EnableAutomaticPosting,
                registration.StripDotsForThreadSearch,
                EnabledRules = registration
                    .PostingRules.Where(rule => rule.IsEnabled)
                    .OrderBy(rule => rule.SortOrder)
                    .ThenBy(rule => rule.Id)
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        return registrations
            .Select(registration => new AutoPostRegistration(
                DistributionSiteRegistrationId: registration.Id,
                Name: registration.Name,
                EnableAutomaticPosting: registration.EnableAutomaticPosting,
                StripDotsForThreadSearch: registration.StripDotsForThreadSearch,
                EnabledRules: registration.EnabledRules
            ))
            .ToList();
    }

    public async Task<IReadOnlyList<int>> GetQueuedReleaseIdsAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .Releases.Where(ReleaseReadRepository.IsReadyForPostQueue)
            .OrderBy(release => release.Id)
            .Select(release => release.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Release>> GetReleasesForRoutingAsync(
        IReadOnlyList<int> releaseIds,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .Releases.Where(release => releaseIds.Contains(release.Id))
            .Include(release => release.Classification)
            .Include(release => release.ReleaseGroup)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AutoPostPostedLocation>> GetPostedLocationsAsync(
        IReadOnlyList<int> releaseIds,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .PostedLocations.Where(location =>
                location.ReleaseId != null && releaseIds.Contains(location.ReleaseId.Value)
            )
            .OrderBy(location => location.Id)
            .Select(location => new AutoPostPostedLocation(
                location.ReleaseId!.Value,
                location.DistributionSiteRegistrationId,
                location.Url
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task RecordPostedLocationAsync(
        int releaseId,
        int distributionSiteRegistrationId,
        string url,
        CancellationToken cancellationToken = default
    )
    {
        dbWrite.Add(
            new PostedLocation
            {
                ReleaseId = releaseId,
                DistributionSiteRegistrationId = distributionSiteRegistrationId,
                Url = url.Trim(),
                CreatedAt = timeProvider.GetLocalNow(),
            }
        );

        await dbWrite.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkReleasePostedAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var release = await dbWrite.Releases.FirstAsync(
            candidate => candidate.Id == releaseId,
            cancellationToken
        );

        release.UploadsPostedAt = timeProvider.GetLocalNow();

        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}

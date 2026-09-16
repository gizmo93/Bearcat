using Bearcat.Abstractions.DistributionSite;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.PostToForums.Models;
using Bearcat.Domain.UseCases.PostToForums.Repositories;
using Bearcat.Domain.ValueObjects;
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
                location.Id,
                location.ReleaseId!.Value,
                location.DistributionSiteRegistrationId,
                location.ForumPostTemplateId,
                location.Url,
                location.CreatedAt,
                location.ContentUpdatedAt
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, DateTime>> GetLatestUploadCompletionTimesAsync(
        IReadOnlyList<int> releaseIds,
        CancellationToken cancellationToken = default
    )
    {
        var completionTimes = await dbRead
            .Uploads.Where(upload =>
                releaseIds.Contains(upload.UploadConfig.ReleaseId)
                && upload.UploadConfig.CollectionUploadSlotId == null
                && upload.UploadConfig.HosterRegistration.IsActive
                && upload.UploadState == UploadState.Completed
                && upload.UploadedAt != null
            )
            .GroupBy(upload => upload.UploadConfig.ReleaseId)
            .Select(group => new
            {
                ReleaseId = group.Key,
                UploadedAt = group.Max(upload => upload.UploadedAt!.Value),
            })
            .ToListAsync(cancellationToken);

        return completionTimes.ToDictionary(entry => entry.ReleaseId, entry => entry.UploadedAt);
    }

    public async Task<AutoPostUpdateTarget> GetUpdateTargetAsync(
        int postedLocationId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .PostedLocations.Where(location =>
                location.Id == postedLocationId && location.DistributionSiteRegistrationId != null
            )
            .Select(location => new AutoPostUpdateTarget(
                location.Id,
                location.ReleaseId ?? location.ReleaseCollectionId!.Value,
                location.ReleaseId != null
                    ? location.Release!.Name
                    : location.ReleaseCollection!.Name,
                location.ReleaseId,
                location.DistributionSiteRegistrationId!.Value,
                location.DistributionSiteRegistration!.Name,
                location.Url,
                location.ForumPostTemplateId
            ))
            .FirstAsync(cancellationToken);
    }

    public async Task RecordPostedLocationAsync(
        int releaseId,
        int distributionSiteRegistrationId,
        string url,
        int forumPostTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        var trimmedUrl = url.Trim();
        var now = timeProvider.GetLocalNow();

        dbWrite.Add(
            new PostedLocation
            {
                ReleaseId = releaseId,
                DistributionSiteRegistrationId = distributionSiteRegistrationId,
                ForumPostTemplateId = forumPostTemplateId,
                Url = trimmedUrl,
                CreatedAt = now,
                ContentUpdatedAt = now,
            }
        );

        await dbWrite.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkPostedLocationUpdatedAsync(
        int postedLocationId,
        string url,
        int forumPostTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        var postedLocation = await dbWrite.PostedLocations.FirstAsync(
            location => location.Id == postedLocationId,
            cancellationToken
        );

        postedLocation.Url = url.Trim();
        postedLocation.ForumPostTemplateId = forumPostTemplateId;
        postedLocation.ContentUpdatedAt = timeProvider.GetLocalNow();

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

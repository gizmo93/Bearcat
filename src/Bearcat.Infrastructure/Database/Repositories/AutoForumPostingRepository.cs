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
            })
            .ToListAsync(cancellationToken);

        var registrationIds = registrations.Select(registration => registration.Id).ToList();
        var enabledRules = await dbRead
            .ForumPostingRules.Include(rule => rule.ForumPostTemplate)
            .Where(rule =>
                rule.IsEnabled && registrationIds.Contains(rule.DistributionSiteRegistrationId)
            )
            .OrderBy(rule => rule.SortOrder)
            .ThenBy(rule => rule.Id)
            .ToListAsync(cancellationToken);

        return registrations
            .Select(registration => new AutoPostRegistration(
                DistributionSiteRegistrationId: registration.Id,
                Name: registration.Name,
                EnableAutomaticPosting: registration.EnableAutomaticPosting,
                StripDotsForThreadSearch: registration.StripDotsForThreadSearch,
                EnabledRules: enabledRules
                    .Where(rule => rule.DistributionSiteRegistrationId == registration.Id)
                    .ToList()
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

    public async Task<IReadOnlyList<int>> GetQueuedReleaseCollectionIdsAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ReleaseCollections.Where(ReleaseCollectionRepository.IsReadyForPostQueue(dbRead))
            .OrderBy(collection => collection.Id)
            .Select(collection => collection.Id)
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

    public async Task<IReadOnlyList<ReleaseCollection>> GetReleaseCollectionsForRoutingAsync(
        IReadOnlyList<int> releaseCollectionIds,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ReleaseCollections.Where(collection => releaseCollectionIds.Contains(collection.Id))
            .Include(collection => collection.ReleaseGroup)
            .Include(collection => collection.Releases)
                .ThenInclude(release => release.Classification)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AutoPostPostedLocation>> GetPostedLocationsAsync(
        ForumPostTemplateType entityType,
        IReadOnlyList<int> entityIds,
        CancellationToken cancellationToken = default
    )
    {
        var postedLocations =
            entityType == ForumPostTemplateType.Release
                ? dbRead.PostedLocations.Where(location =>
                    location.ReleaseId != null && entityIds.Contains(location.ReleaseId.Value)
                )
                : dbRead.PostedLocations.Where(location =>
                    location.ReleaseCollectionId != null
                    && entityIds.Contains(location.ReleaseCollectionId.Value)
                );

        return await postedLocations
            .OrderBy(location => location.Id)
            .Select(location => new AutoPostPostedLocation(
                PostedLocationId: location.Id,
                EntityId: location.ReleaseId ?? location.ReleaseCollectionId!.Value,
                DistributionSiteRegistrationId: location.DistributionSiteRegistrationId,
                ForumPostTemplateId: location.ForumPostTemplateId,
                Url: location.Url,
                CreatedAt: location.CreatedAt,
                ContentUpdatedAt: location.ContentUpdatedAt
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, DateTime>> GetLatestUploadCompletionTimesAsync(
        ForumPostTemplateType entityType,
        IReadOnlyList<int> entityIds,
        CancellationToken cancellationToken = default
    )
    {
        var completedUploads = dbRead.Uploads.Where(upload =>
            upload.HosterRegistration!.IsActive
            && upload.UploadState == UploadState.Completed
            && upload.UploadedAt != null
        );

        var uploadsByEntityId =
            entityType == ForumPostTemplateType.Release
                ? completedUploads
                    .Where(upload =>
                        upload.UploadConfig.CollectionUploadSlotId == null
                        && entityIds.Contains(upload.UploadConfig.ReleaseId)
                    )
                    .GroupBy(upload => upload.UploadConfig.ReleaseId)
                : completedUploads
                    .Where(upload =>
                        upload.UploadConfig.CollectionUploadSlotId != null
                        && entityIds.Contains(
                            upload.UploadConfig.CollectionUploadSlot!.ReleaseCollectionId
                        )
                    )
                    .GroupBy(upload =>
                        upload.UploadConfig.CollectionUploadSlot!.ReleaseCollectionId
                    );

        var completionTimes = await uploadsByEntityId
            .Select(group => new
            {
                EntityId = group.Key,
                UploadedAt = group.Max(upload => upload.UploadedAt!.Value),
            })
            .ToListAsync(cancellationToken);

        return completionTimes.ToDictionary(entry => entry.EntityId, entry => entry.UploadedAt);
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
                PostedLocationId: location.Id,
                EntityType: location.ReleaseId != null
                    ? ForumPostTemplateType.Release
                    : ForumPostTemplateType.ReleaseCollection,
                EntityId: location.ReleaseId ?? location.ReleaseCollectionId!.Value,
                EntityName: location.ReleaseId != null
                    ? location.Release!.Name
                    : location.ReleaseCollection!.Name,
                DistributionSiteRegistrationId: location.DistributionSiteRegistrationId!.Value,
                DistributionSiteRegistrationName: location.DistributionSiteRegistration!.Name,
                PostedUrl: location.Url,
                ForumPostTemplateId: location.ForumPostTemplateId
            ))
            .FirstAsync(cancellationToken);
    }

    public async Task RecordPostedLocationAsync(
        ForumPostTemplateType entityType,
        int entityId,
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
                ReleaseId = entityType == ForumPostTemplateType.Release ? entityId : null,
                ReleaseCollectionId =
                    entityType == ForumPostTemplateType.ReleaseCollection ? entityId : null,
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

    public async Task MarkUploadsPostedAsync(
        ForumPostTemplateType entityType,
        int entityId,
        CancellationToken cancellationToken = default
    )
    {
        var now = timeProvider.GetLocalNow();

        if (entityType == ForumPostTemplateType.Release)
        {
            var release = await dbWrite.Releases.FirstAsync(
                candidate => candidate.Id == entityId,
                cancellationToken
            );
            release.UploadsPostedAt = now;
        }
        else
        {
            var collection = await dbWrite.ReleaseCollections.FirstAsync(
                candidate => candidate.Id == entityId,
                cancellationToken
            );
            collection.UploadsPostedAt = now;
        }

        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}

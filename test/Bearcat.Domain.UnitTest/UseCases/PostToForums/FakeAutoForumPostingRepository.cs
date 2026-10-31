using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.PostToForums.Models;
using Bearcat.Domain.UseCases.PostToForums.Repositories;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UnitTest.UseCases.PostToForums;

public sealed class FakeAutoForumPostingRepository : IAutoForumPostingRepository
{
    public List<AutoPostRegistration> Registrations { get; init; } = [];

    public List<Release> Releases { get; init; } = [];

    public List<ReleaseCollection> ReleaseCollections { get; init; } = [];

    public List<AutoPostPostedLocation> PostedLocations { get; init; } = [];

    public List<AutoPostPostedLocation> ReleaseCollectionPostedLocations { get; init; } = [];

    public Dictionary<int, DateTime> LatestUploadCompletionTimes { get; init; } = [];

    public Dictionary<int, DateTime> ReleaseCollectionLatestUploadCompletionTimes { get; init; } =
    [];

    public List<AutoPostUpdateTarget> UpdateTargets { get; init; } = [];

    public List<AutoPostPostedLocation> RecordedPostedLocations { get; } = [];

    public List<UpdatedPostedLocation> UpdatedPostedLocations { get; } = [];

    public List<int> MarkedReleaseIds { get; } = [];

    public List<int> MarkedReleaseCollectionIds { get; } = [];

    public int PlanningRoundCount { get; private set; }

    public DateTime Now { get; set; } = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Unspecified);

    public Task<IReadOnlyList<AutoPostRegistration>> GetForumRegistrationsWithEnabledRulesAsync(
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult<IReadOnlyList<AutoPostRegistration>>(Registrations);
    }

    public Task<IReadOnlyList<int>> GetQueuedReleaseIdsAsync(
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult<IReadOnlyList<int>>(Releases.Select(release => release.Id).ToList());
    }

    public Task<IReadOnlyList<int>> GetQueuedReleaseCollectionIdsAsync(
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult<IReadOnlyList<int>>(
            ReleaseCollections.Select(collection => collection.Id).ToList()
        );
    }

    public Task<IReadOnlyList<Release>> GetReleasesForRoutingAsync(
        IReadOnlyList<int> releaseIds,
        CancellationToken cancellationToken = default
    )
    {
        PlanningRoundCount++;

        return Task.FromResult<IReadOnlyList<Release>>(
            Releases.Where(release => releaseIds.Contains(release.Id)).ToList()
        );
    }

    public Task<IReadOnlyList<ReleaseCollection>> GetReleaseCollectionsForRoutingAsync(
        IReadOnlyList<int> releaseCollectionIds,
        CancellationToken cancellationToken = default
    )
    {
        PlanningRoundCount++;

        return Task.FromResult<IReadOnlyList<ReleaseCollection>>(
            ReleaseCollections
                .Where(collection => releaseCollectionIds.Contains(collection.Id))
                .ToList()
        );
    }

    public Task<IReadOnlyList<AutoPostPostedLocation>> GetPostedLocationsAsync(
        ForumPostTemplateType entityType,
        IReadOnlyList<int> entityIds,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult<IReadOnlyList<AutoPostPostedLocation>>(
            PostedLocationsOf(entityType)
                .Where(location => entityIds.Contains(location.EntityId))
                .ToList()
        );
    }

    public Task<IReadOnlyDictionary<int, DateTime>> GetLatestUploadCompletionTimesAsync(
        ForumPostTemplateType entityType,
        IReadOnlyList<int> entityIds,
        CancellationToken cancellationToken = default
    )
    {
        var latestUploadCompletionTimes =
            entityType == ForumPostTemplateType.Release
                ? LatestUploadCompletionTimes
                : ReleaseCollectionLatestUploadCompletionTimes;

        return Task.FromResult<IReadOnlyDictionary<int, DateTime>>(
            latestUploadCompletionTimes
                .Where(entry => entityIds.Contains(entry.Key))
                .ToDictionary(entry => entry.Key, entry => entry.Value)
        );
    }

    public Task<AutoPostUpdateTarget> GetUpdateTargetAsync(
        int postedLocationId,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult(
            UpdateTargets.First(target => target.PostedLocationId == postedLocationId)
        );
    }

    public Task RecordPostedLocationAsync(
        ForumPostTemplateType entityType,
        int entityId,
        int distributionSiteRegistrationId,
        string url,
        int forumPostTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        var postedLocations = PostedLocationsOf(entityType);
        var postedLocation = new AutoPostPostedLocation(
            PostedLocationId: postedLocations.Count + 1,
            EntityId: entityId,
            DistributionSiteRegistrationId: distributionSiteRegistrationId,
            ForumPostTemplateId: forumPostTemplateId,
            Url: url,
            CreatedAt: Now,
            ContentUpdatedAt: Now
        );

        RecordedPostedLocations.Add(postedLocation);
        postedLocations.Add(postedLocation);

        return Task.CompletedTask;
    }

    public Task MarkPostedLocationUpdatedAsync(
        int postedLocationId,
        string url,
        int forumPostTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        UpdatedPostedLocations.Add(
            new UpdatedPostedLocation(postedLocationId, url, forumPostTemplateId)
        );

        var postedLocations = PostedLocations.Any(location =>
            location.PostedLocationId == postedLocationId
        )
            ? PostedLocations
            : ReleaseCollectionPostedLocations;

        var index = postedLocations.FindIndex(location =>
            location.PostedLocationId == postedLocationId
        );

        postedLocations[index] = postedLocations[index] with
        {
            Url = url,
            ForumPostTemplateId = forumPostTemplateId,
            ContentUpdatedAt = Now,
        };

        return Task.CompletedTask;
    }

    public Task MarkUploadsPostedAsync(
        ForumPostTemplateType entityType,
        int entityId,
        CancellationToken cancellationToken = default
    )
    {
        var markedIds =
            entityType == ForumPostTemplateType.Release
                ? MarkedReleaseIds
                : MarkedReleaseCollectionIds;

        markedIds.Add(entityId);

        return Task.CompletedTask;
    }

    private List<AutoPostPostedLocation> PostedLocationsOf(ForumPostTemplateType entityType)
    {
        return entityType == ForumPostTemplateType.Release
            ? PostedLocations
            : ReleaseCollectionPostedLocations;
    }
}

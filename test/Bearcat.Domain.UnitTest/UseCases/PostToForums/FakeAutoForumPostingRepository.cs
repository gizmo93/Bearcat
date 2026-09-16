using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.PostToForums.Models;
using Bearcat.Domain.UseCases.PostToForums.Repositories;

namespace Bearcat.Domain.UnitTest.UseCases.PostToForums;

public sealed class FakeAutoForumPostingRepository : IAutoForumPostingRepository
{
    public List<AutoPostRegistration> Registrations { get; init; } = [];

    public List<Release> Releases { get; init; } = [];

    public List<AutoPostPostedLocation> PostedLocations { get; init; } = [];

    public Dictionary<int, DateTime> LatestUploadCompletionTimes { get; init; } = [];

    public List<AutoPostUpdateTarget> UpdateTargets { get; init; } = [];

    public List<AutoPostPostedLocation> RecordedPostedLocations { get; } = [];

    public List<UpdatedPostedLocation> UpdatedPostedLocations { get; } = [];

    public List<int> MarkedReleaseIds { get; } = [];

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

    public Task<IReadOnlyList<AutoPostPostedLocation>> GetPostedLocationsAsync(
        IReadOnlyList<int> releaseIds,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult<IReadOnlyList<AutoPostPostedLocation>>(
            PostedLocations.Where(location => releaseIds.Contains(location.ReleaseId)).ToList()
        );
    }

    public Task<IReadOnlyDictionary<int, DateTime>> GetLatestUploadCompletionTimesAsync(
        IReadOnlyList<int> releaseIds,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult<IReadOnlyDictionary<int, DateTime>>(
            LatestUploadCompletionTimes
                .Where(entry => releaseIds.Contains(entry.Key))
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
        int releaseId,
        int distributionSiteRegistrationId,
        string url,
        int forumPostTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        var postedLocation = new AutoPostPostedLocation(
            PostedLocationId: PostedLocations.Count + 1,
            ReleaseId: releaseId,
            DistributionSiteRegistrationId: distributionSiteRegistrationId,
            ForumPostTemplateId: forumPostTemplateId,
            Url: url,
            CreatedAt: Now,
            ContentUpdatedAt: Now
        );

        RecordedPostedLocations.Add(postedLocation);
        PostedLocations.Add(postedLocation);

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

        var index = PostedLocations.FindIndex(location =>
            location.PostedLocationId == postedLocationId
        );

        PostedLocations[index] = PostedLocations[index] with
        {
            Url = url,
            ForumPostTemplateId = forumPostTemplateId,
            ContentUpdatedAt = Now,
        };

        return Task.CompletedTask;
    }

    public Task MarkReleasePostedAsync(int releaseId, CancellationToken cancellationToken = default)
    {
        MarkedReleaseIds.Add(releaseId);

        return Task.CompletedTask;
    }
}

using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.PostToForums;
using Bearcat.Domain.UseCases.PostToForums.Repositories;

namespace Bearcat.Domain.UnitTest.UseCases.PostToForums;

public sealed class FakeAutoForumPostingRepository : IAutoForumPostingRepository
{
    public List<AutoPostRegistration> Registrations { get; init; } = [];

    public List<Release> Releases { get; init; } = [];

    public List<AutoPostPostedLocation> PostedLocations { get; init; } = [];

    public List<AutoPostPostedLocation> RecordedPostedLocations { get; } = [];

    public List<int> MarkedReleaseIds { get; } = [];

    public int PlanningRoundCount { get; private set; }

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

    public Task RecordPostedLocationAsync(
        int releaseId,
        int distributionSiteRegistrationId,
        string url,
        CancellationToken cancellationToken = default
    )
    {
        var postedLocation = new AutoPostPostedLocation(
            releaseId,
            distributionSiteRegistrationId,
            url
        );

        RecordedPostedLocations.Add(postedLocation);
        PostedLocations.Add(postedLocation);

        return Task.CompletedTask;
    }

    public Task MarkReleasePostedAsync(int releaseId, CancellationToken cancellationToken = default)
    {
        MarkedReleaseIds.Add(releaseId);

        return Task.CompletedTask;
    }
}

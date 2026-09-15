using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.AutoForumPosting;

namespace Bearcat.Domain.UnitTest.Shared.AutoForumPosting;

public sealed class FakeAutoForumPostingRepository : IAutoForumPostingRepository
{
    public List<AutoPostRegistration> Registrations { get; init; } = [];

    public List<Release> Releases { get; init; } = [];

    public List<AutoPostPostedLocation> PostedLocations { get; init; } = [];

    public List<AutoPostPostedLocation> RecordedPostedLocations { get; } = [];

    public List<int> MarkedReleaseIds { get; } = [];

    public Task<IReadOnlyList<AutoPostRegistration>> GetForumRegistrationsWithEnabledRulesAsync(
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult<IReadOnlyList<AutoPostRegistration>>(Registrations);
    }

    public Task<IReadOnlyList<Release>> GetReleasesForRoutingAsync(
        IReadOnlyList<int> releaseIds,
        CancellationToken cancellationToken = default
    )
    {
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

using Bearcat.Domain.Entities;

namespace Bearcat.Domain.Shared.AutoForumPosting;

public interface IAutoForumPostingRepository
{
    Task<IReadOnlyList<AutoPostRegistration>> GetForumRegistrationsWithEnabledRulesAsync(
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<Release>> GetReleasesForRoutingAsync(
        IReadOnlyList<int> releaseIds,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<AutoPostPostedLocation>> GetPostedLocationsAsync(
        IReadOnlyList<int> releaseIds,
        CancellationToken cancellationToken = default
    );

    Task RecordPostedLocationAsync(
        int releaseId,
        int distributionSiteRegistrationId,
        string url,
        CancellationToken cancellationToken = default
    );

    Task MarkReleasePostedAsync(int releaseId, CancellationToken cancellationToken = default);
}

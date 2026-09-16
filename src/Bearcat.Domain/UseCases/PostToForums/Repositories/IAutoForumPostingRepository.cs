using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.PostToForums.Models;

namespace Bearcat.Domain.UseCases.PostToForums.Repositories;

public interface IAutoForumPostingRepository
{
    Task<IReadOnlyList<AutoPostRegistration>> GetForumRegistrationsWithEnabledRulesAsync(
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<int>> GetQueuedReleaseIdsAsync(
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

    Task<IReadOnlyDictionary<int, DateTime>> GetLatestUploadCompletionTimesAsync(
        IReadOnlyList<int> releaseIds,
        CancellationToken cancellationToken = default
    );

    Task<AutoPostUpdateTarget> GetUpdateTargetAsync(
        int postedLocationId,
        CancellationToken cancellationToken = default
    );

    Task RecordPostedLocationAsync(
        int releaseId,
        int distributionSiteRegistrationId,
        string url,
        int forumPostTemplateId,
        CancellationToken cancellationToken = default
    );

    Task MarkPostedLocationUpdatedAsync(
        int postedLocationId,
        string url,
        int forumPostTemplateId,
        CancellationToken cancellationToken = default
    );

    Task MarkReleasePostedAsync(int releaseId, CancellationToken cancellationToken = default);
}

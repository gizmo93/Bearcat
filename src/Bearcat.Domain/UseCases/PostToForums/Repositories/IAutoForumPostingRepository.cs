using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.PostToForums.Models;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.PostToForums.Repositories;

public interface IAutoForumPostingRepository
{
    Task<IReadOnlyList<AutoPostRegistration>> GetForumRegistrationsWithEnabledRulesAsync(
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<int>> GetQueuedReleaseIdsAsync(
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<int>> GetQueuedReleaseCollectionIdsAsync(
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<Release>> GetReleasesForRoutingAsync(
        IReadOnlyList<int> releaseIds,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ReleaseCollection>> GetReleaseCollectionsForRoutingAsync(
        IReadOnlyList<int> releaseCollectionIds,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<AutoPostPostedLocation>> GetPostedLocationsAsync(
        ForumPostTemplateType entityType,
        IReadOnlyList<int> entityIds,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyDictionary<int, DateTime>> GetLatestUploadCompletionTimesAsync(
        ForumPostTemplateType entityType,
        IReadOnlyList<int> entityIds,
        CancellationToken cancellationToken = default
    );

    Task<AutoPostUpdateTarget> GetUpdateTargetAsync(
        int postedLocationId,
        CancellationToken cancellationToken = default
    );

    Task RecordPostedLocationAsync(
        ForumPostTemplateType entityType,
        int entityId,
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

    Task MarkUploadsPostedAsync(
        ForumPostTemplateType entityType,
        int entityId,
        CancellationToken cancellationToken = default
    );
}
